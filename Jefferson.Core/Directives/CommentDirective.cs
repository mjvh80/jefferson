using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   [DebuggerDisplay("#{Name}")]
   public sealed class CommentDirective : LiteralDirective
   {
      public override String Name
      {
         get
         {
            return "comment";
         }
      }

      public override Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         return Utils.NopExpression;
      }
   }
}
