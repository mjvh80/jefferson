using System;

namespace Jefferson.Directives
{
   public sealed class CommentDirective : LiteralDirective
   {
      public override String Name
      {
         get
         {
            return "comment";
         }
      }

      public override System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         return Utils.NopExpression;
      }
   }
}
