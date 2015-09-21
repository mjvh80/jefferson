using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   /// <summary>
   /// Does nothing but copy the current context as a new scope.
   /// This is not as useless as it seems, e.g. consider
   /// $$#block$$
   ///   $$#let x = 'blah'$$
   ///   $$ $1.x $$
   /// $$/block$$
   /// It would otherwise be impossible to access x in the current scope as let does not introduce a new scope.
   /// </summary>
   public class BlockDirective : IDirective
   {
      public String Name
      {
         get { return "block"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Boolean MayBeEmpty
      {
         get { return false; }
      }

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         var currentContext = parserContext.GetNthContext(0);
         var currentContextAsObj = Expression.Convert(currentContext, typeof(Object));
         parserContext.PushScope(currentContext.Type);
         var result = Expression.Block(
                         Expression.Call(parserContext.RuntimeContexts, Utils.GetMethod<List<Object>>(l => l.Add(null)), currentContextAsObj),
                         Expression.Invoke(parserContext.Parse<Object>(source), currentContextAsObj, parserContext.Output),
                         Expression.Call(parserContext.RuntimeContexts, Utils.GetMethod<List<Object>>(l => l.RemoveAt(0)),
                            Expression.Subtract(Expression.Property(parserContext.RuntimeContexts, "Count"), Expression.Constant(1))));
         parserContext.PopScope();
         return result;
      }
   }
}
