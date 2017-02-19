using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;

namespace Jefferson.Build.Extensions
{
    internal static class GeneralExtensions
    {
        public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T> @enum)
        {
            return @enum ?? new T[0]; // could do better
        }

       // public static Diagnostic AddLines(this Diagnostic d, Int32 number)
    }
}
