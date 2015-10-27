using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   /// <summary>
   /// A pragma is a processing instruction to the parser or the caller of the parser. They can be handled by hooking the <see cref="Jefferson.TemplateParser.PragmaSeen"/>  event
   /// of the parser.
   /// </summary>
   [DebuggerDisplay("#{Name}")]
   public sealed class PragmaDirective : IDirective
   {
      public String Name
      {
         get { return "pragma"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (source != null) throw parserContext.SyntaxError(0, "#pragma should not have a body");
         if (String.IsNullOrWhiteSpace(arguments)) throw parserContext.SyntaxError(0, "#pragma arguments should not be empty");
         try
         {
            parserContext.Parser.OnPragmaSeen(parserContext, arguments);
         }
         catch (StopProcessingException) { throw; }
         catch (Exception e)
         {
            throw parserContext.SyntaxError(0, e, "Unhandled exception processing #pragma '{0}'", arguments);
         }
         return Utils.NopExpression;
      }
   }
}
