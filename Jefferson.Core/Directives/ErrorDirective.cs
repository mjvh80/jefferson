using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   /// <summary>
   /// Throws an exception when run. Useful for enforcing certain invariants.
   /// </summary>
   [DebuggerDisplay("#{Name}")]
   public sealed class ErrorDirective : IDirective
   {
      public String Name
      {
         get { return "error"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (String.IsNullOrWhiteSpace(arguments)) throw parserContext.SyntaxError(0, "Expected arguments for #error directive.");
         if (source != null) throw parserContext.SyntaxError(0, "#error directive should be empty");
         return Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(String) }), Expression.Constant("Intentional #error: " + arguments)));
      }
   }
}
