using System;

namespace Jefferson.Directives
{
   public class UsingDirective : IDirective
   {
      public String Name
      {
         get { return "using"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (String.IsNullOrWhiteSpace(arguments)) throw parserContext.SyntaxError(0, "Missing or empty arguments, expected a namespace");
         if (source != null) throw parserContext.SyntaxError(0, "#using should be empty");
         var ns = arguments.Trim();
         if (!ParserUtils.IsValidNamespace(ns)) throw parserContext.SyntaxError(0, "Given namespace contains invalid characters.");
         parserContext.UsingNamespaces.Add(ns);
         return Utils.NopExpression;
      }
   }
}
