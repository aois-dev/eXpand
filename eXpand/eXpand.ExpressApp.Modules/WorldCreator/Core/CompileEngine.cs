﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using DevExpress.ExpressApp;
using eXpand.Persistent.Base.PersistentMetaData;
using Microsoft.CSharp;
using Microsoft.JScript;
using Microsoft.VisualBasic;
using CodeDomProvider = eXpand.Persistent.Base.PersistentMetaData.CodeDomProvider;

namespace eXpand.ExpressApp.WorldCreator.Core {
    public class CompileEngine
    {
        private const string STR_StrongKeys = "StrongKeys";
        static readonly List<Assembly> CompiledAssemblies=new List<Assembly>();
        

        public Type CompileModule(IPersistentAssemblyInfo persistentAssemblyInfo,Action<CompilerParameters> action) {
            var generateCode = GetVersionCode(persistentAssemblyInfo);
            generateCode += CodeEngine.GenerateCode(persistentAssemblyInfo);
            generateCode += getModuleCode(persistentAssemblyInfo.Name) + Environment.NewLine;
            var codeProvider = getCodeDomProvider(persistentAssemblyInfo.CodeDomProvider);
            var compilerParams = new CompilerParameters
            {
                CompilerOptions = @"/target:library /lib:" + GetReferenceLocations() + GetStorngKeyParams(persistentAssemblyInfo),
                GenerateExecutable = false,
                GenerateInMemory = true,
                IncludeDebugInformation = false,
                OutputAssembly = persistentAssemblyInfo.Name
            };
            if (action!= null)
                action.Invoke(compilerParams);                
            addReferences(compilerParams);
            if (File.Exists(compilerParams.OutputAssembly))
                File.Delete(compilerParams.OutputAssembly);
            return compile(persistentAssemblyInfo, generateCode, compilerParams, codeProvider);

        }
        public Type CompileModule(IPersistentAssemblyInfo persistentAssemblyInfo)
        {
            return CompileModule(persistentAssemblyInfo, null);
        }

        string GetVersionCode(IPersistentAssemblyInfo persistentAssemblyInfo) {
            var version = persistentAssemblyInfo.Version;
            if (!string.IsNullOrEmpty(version))
                return string.Format(@"[assembly: System.Reflection.AssemblyVersionAttribute(""{0}"")]", version)+Environment.NewLine;
            return null;
        }

//        void setAssemblyVersion(System.CodeDom.Compiler.CodeDomProvider codeProvider, string version) {
//            var unit = new CodeCompileUnit();
//            var attr = new CodeTypeReference(typeof(AssemblyVersionAttribute));
//            var decl = new CodeAttributeDeclaration(attr,
//              new CodeAttributeArgument(new CodePrimitiveExpression(version)));
//            unit.AssemblyCustomAttributes.Add(decl);
//            using (var sw = new StringWriter()) {
//                codeProvider.GenerateCodeFromCompileUnit(unit, sw, new CodeGeneratorOptions());
//                var s = sw.ToString();
//                Debug.Print("");
//            }
//        }

        string GetStorngKeyParams(IPersistentAssemblyInfo persistentAssemblyInfo) {
            if (persistentAssemblyInfo.FileData!= null) {
                if (!Directory.Exists(STR_StrongKeys))
                    Directory.CreateDirectory(STR_StrongKeys);

                var newGuid = Guid.NewGuid();
                using (var fileStream = new FileStream(@"StrongKeys\" + newGuid + ".snk", FileMode.Create)) {
                    persistentAssemblyInfo.FileData.SaveToStream(fileStream);
                }
                return @" /keyfile:StrongKeys\"+newGuid+".snk";
            }
            return null;
        }

        static System.CodeDom.Compiler.CodeDomProvider getCodeDomProvider(CodeDomProvider codeDomProvider)
        {
            if (codeDomProvider==CodeDomProvider.CSharp)
                return new CSharpCodeProvider();
            if (codeDomProvider==CodeDomProvider.VB)
                return new VBCodeProvider();
            return new JScriptCodeProvider();
        }

        static Type compile(IPersistentAssemblyInfo persistentAssemblyInfo, string generateCode, CompilerParameters compilerParams, System.CodeDom.Compiler.CodeDomProvider codeProvider) {
            CompilerResults compileAssemblyFromSource = null;
            try{
                compileAssemblyFromSource = codeProvider.CompileAssemblyFromSource(compilerParams, generateCode);
                if (compilerParams.GenerateInMemory) {
                    Assembly compiledAssembly = compileAssemblyFromSource.CompiledAssembly;
                    CompiledAssemblies.Add(compiledAssembly);
                    return compiledAssembly.GetTypes().Where(type => typeof(ModuleBase).IsAssignableFrom(type)).Single();
                }
                return null;
            }
            catch (Exception){
            }
            finally {
                if (compileAssemblyFromSource != null){
                    SetErrors(compileAssemblyFromSource, persistentAssemblyInfo);
                }
                if (Directory.Exists(STR_StrongKeys))
                    Directory.Delete(STR_StrongKeys,true);
            }
            return null;
        }

        static void SetErrors(CompilerResults compileAssemblyFromSource, IPersistentAssemblyInfo persistentAssemblyInfo) {
            persistentAssemblyInfo.CompileErrors = null;
            persistentAssemblyInfo.CompileErrors =
                compileAssemblyFromSource.Errors.Cast<CompilerError>().Aggregate(
                    persistentAssemblyInfo.CompileErrors, (current, error) => current +Environment.NewLine+ error.ToString());
        }

        static void addReferences(CompilerParameters compilerParams) {
            Func<Assembly, bool> isNotDynamic = assembly1 => !(assembly1 is AssemblyBuilder) && !CompiledAssemblies.Contains(assembly1)&&assembly1.EntryPoint==null;
            Func<Assembly, string> assemblyNameSelector = assembly => new AssemblyName(assembly.FullName + "").Name + ".dll";
            compilerParams.ReferencedAssemblies.AddRange(AppDomain.CurrentDomain.GetAssemblies().Where(isNotDynamic).Select(assemblyNameSelector).ToArray());
            
        }


        static string GetReferenceLocations() {
            Func<Assembly, string> locationSelector =assembly =>getAssemblyLocation(assembly);
            Func<string, bool> pathIsValid = s => s.Length > 2;
            string referenceLocations = AppDomain.CurrentDomain.GetAssemblies().Select(locationSelector).Distinct().
                Where(pathIsValid).Aggregate<string, string>(null, (current, type) => current + (type + ",")).TrimEnd(',');
            return referenceLocations;
        }

        static string getAssemblyLocation(Assembly assembly) {
            return @"""" +((assembly is AssemblyBuilder)? null: (!string.IsNullOrEmpty(assembly.Location) ? Path.GetDirectoryName(assembly.Location) : null)) +@"""";
        }

        internal static string getModuleCode(string assemblyName)
        {
            return "namespace " +assemblyName+ "{public class Dynamic" + assemblyName + "Module:DevExpress.ExpressApp.ModuleBase{}}";
        }

        public List<Type> CompileModules(IList<IPersistentAssemblyInfo> persistentAssemblyInfos) {

            var definedModules = new List<Type>();
            
            foreach (IPersistentAssemblyInfo persistentAssemblyInfo in persistentAssemblyInfos) {
                string path = Path.Combine(Path.GetDirectoryName(System.Windows.Forms.Application.ExecutablePath),persistentAssemblyInfo.Name);
                if (File.Exists(path+".dll"))
                    File.Delete(path+".dll");
                persistentAssemblyInfo.CompileErrors = null;
                Type compileModule = CompileModule(persistentAssemblyInfo);    
                
                if (compileModule != null) {
                    definedModules.Add(compileModule);
                }
                else if (File.Exists(path)) {
                    var fileInfo=new FileInfo(path);
                    fileInfo.CopyTo(path+".dll");
                    Assembly assembly = Assembly.LoadFile(path+".dll");
                    Type single = assembly.GetTypes().Where(type => typeof(ModuleBase).IsAssignableFrom(type)).Single();
                    definedModules.Add(single);
                }
            }
            return definedModules;
        }
    }

}