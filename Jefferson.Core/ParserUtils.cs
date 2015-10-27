
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

      private static readonly Regex _sDirectiveNameExpr = new Regex("^[a-zA-Z]+$", RegexOptions.CultureInvariant);

      public static Boolean IsValidDirectiveName(String name)
      {
         return name != null && _sDirectiveNameExpr.IsMatch(name);
      }
   }
}
