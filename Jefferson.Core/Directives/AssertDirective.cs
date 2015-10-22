using Jefferson.Extensions;
using System;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   [DebuggerDisplay("#{Name}")]
   public class AssertDirective : IDirective
   {
      public String Name
      {
         get { return "assert"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         if (source != null) throw parserContext.SyntaxError(0, "#assert should be empty");
         if (String.IsNullOrWhiteSpace(arguments)) parserContext.SyntaxError(0, "Missing arguments for assert.");

         var idx = arguments.Trim().IndexOfWhiteSpace();
         if (idx < 0) throw parserContext.SyntaxError(0, "Invalid arguments, should be of the form 'predicate message'.");

         var predicate = arguments.Substring(0, idx);
         var message = arguments.Substring(idx).Trim();

         var oldSetting = parserContext.OverrideAllowUnknownNames;
         parserContext.OverrideAllowUnknownNames = true;

         var compiledPredicate = parserContext.CompileExpression<Boolean>(predicate); // todo < this method should keep track of relative source? switch?

         var result = Expression.IfThen(Expression.Not(
                                           Expression.Invoke(compiledPredicate.Ast, Expression.Convert(parserContext.GetNthContext(0), typeof(Object)))),
                                        Expression.Throw(Expression.New(typeof(Exception).GetConstructor(new[] { typeof(String) }), Expression.Constant("Assertion Failure: " + message))));

         parserContext.OverrideAllowUnknownNames = oldSetting;
         return result;
      }
   }
}
