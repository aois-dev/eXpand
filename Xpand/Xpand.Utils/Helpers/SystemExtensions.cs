﻿using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;

namespace Xpand.Utils.Helpers{
    public static class SystemExtensions{
        public enum SizeDefinition{
            Byte = 1,
            Kilobyte = 2,
            Megabyte = 3,
            Gigabyte = 4
        }

        public static bool IsDefault<T> (this T value) where T : struct {
            return (EqualityComparer<T>.Default.Equals(value, default));
        }

        public static double Convert(long amount, SizeDefinition from, SizeDefinition to){
            return ConvertCore(amount, from, to);
        }

        public static double Convert(this int amount, SizeDefinition from, SizeDefinition to){
            return ConvertCore(amount, from, to);
        }
        
        public static double Convert(this double amount, SizeDefinition from, SizeDefinition to){
            return ConvertCore(amount, from, to);
        }

        private static double ConvertCore(double amount, SizeDefinition from, SizeDefinition to){
            int difference;
            if ((int) from > (int) to){
                difference = (int) from - (int) to;
                return amount * (1024d * difference);
            }
            difference = (int) to - (int) from;
            return amount / (1024d * difference);
        }

        public static string AggregateErrors(this CompilerResults compilerResults){
            return string.Join(Environment.NewLine, compilerResults.Errors.Cast<CompilerError>().Select(error =>
                $"({error.Line},{error.Column}): {error.ErrorText}"));
        }
    }
}