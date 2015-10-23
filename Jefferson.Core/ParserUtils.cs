
using System;
using System.Text.RegularExpressions;

namespace Jefferson
{
   internal static class ParserUtils
   {
      // todo: this should follow something more ... formal
      private static readonly Regex _sValidNamespaceRegex = new Regex(@"^\w+(.\w+)+$", RegexOptions.CultureInvariant);

      public static Boolean IsValidNamespace(String arg)
      {
         return arg != null && _sValidNamespaceRegex.IsMatch(arg);
      }
   }
}
