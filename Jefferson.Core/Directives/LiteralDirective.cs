using Jefferson.Output;
using System;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   /// <summary>
   /// Implements #literal directive which simply outputs the source it contains.
   /// NOTE: when used with ReplaceDeep, the body of the #literal directive will be evaluated in a second run.
   /// NOTE (ii): cannot be nested currently
   /// </summary>
   public class LiteralDirective : IDirective
   {
      public virtual String Name
      {
         get { return "literal"; }
      }

      public virtual String[] ReservedWords
      {
         get { return null; }
      }

      public virtual Boolean MayBeEmpty
      {
         get { return false; }
      }

      public virtual Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (arguments != null && arguments.Trim().Length > 0) throw parserContext.SyntaxError(0, "#literal directive does not take arguments");
         return Expression.Call(parserContext.Output, Utils.GetMethod<IOutputWriter>(b => b.Write("")), Expression.Constant(source));
      }
   }
}
