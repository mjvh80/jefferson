using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Jefferson.Build.Extensions
{
    internal static class GeneralExtensions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> @enum)
        {
            return @enum ?? new T[0]; // could do better
        }

        public static String OptAddNewline(this String str)
        {
            return String.IsNullOrEmpty(str) ? str : str + Environment.NewLine;
        }
    }
}
