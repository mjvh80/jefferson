using Jefferson.Parsing;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public class IfDirective : IDirective
   {
      public String Name
      {
         get { return "if"; }
      }

      public String[] ReservedWords
      {
         get { return new[] { "else", "elif" }; }
      }

      public Boolean MayBeEmpty
      {
         get { return false; }
      }

      public Expression Compile(TemplateParserContext parserCtx, String args, String source)
      {
         var idx = 0;
         var contextParamAsObj = Expression.Convert(parserCtx.GetNthContext(0), typeof(Object));

         var endIfIdx = -1;

         // A list of predicate, body pairs.
         var ifStmt = new List<Tuple<Expression, Expression>>();

         for (; ; )
         {
            // Here we're handling if or elif.
            var expr = args;
            var closeIdx = 0;

            if (idx > 0) // elif
            {
               closeIdx = source.IndexOf("$$", idx);
               if (closeIdx < 0) throw parserCtx.SyntaxError(idx, "Unmatched $$ found."); // todo: incorrect global error, must provide service in context

               expr = source.Substring(idx, closeIdx - idx);
               closeIdx += 2;
            }

            if (String.IsNullOrEmpty(expr) || expr.Trim().Length == 0) throw parserCtx.SyntaxError(idx, "Empty expression in if or elif.");

            endIfIdx = parserCtx.FindDirectiveEnd(source, closeIdx, "$$#else$$", "$$#elif ");
            if (endIfIdx < 0) endIfIdx = source.Length;

            var contents = source.Substring(closeIdx, endIfIdx - closeIdx);
            var compiledContents = parserCtx.Parse<Object>(contents);

            ifStmt.Add(Tuple.Create<Expression, Expression>(Expression.Invoke(parserCtx.CompileExpression<Boolean>(expr).Ast, contextParamAsObj),
                                                            Expression.Invoke(compiledContents, contextParamAsObj, parserCtx.Output)));

            if (endIfIdx == source.Length)
               break;

            // Else or Else if.
            switch (source.Substring(endIfIdx, 6))
            {
               case "$$#els": // else
                  closeIdx = endIfIdx + "$$#else$$".Length;
                  endIfIdx = source.Length;

                  contents = source.Substring(closeIdx, endIfIdx - closeIdx);
                  compiledContents = parserCtx.Parse<Object>(contents);

                  ifStmt.Add(Tuple.Create<Expression, Expression>(null, Expression.Invoke(compiledContents, contextParamAsObj, parserCtx.Output)));
                  goto EndIf;

               case "$$#eli": // elseif
                  idx = endIfIdx + "$$#elif ".Length;
                  continue;

               default:
                  throw new InvalidOperationException();
            }
         }
      EndIf:
         return _MakeIfExpression(ifStmt);
      }

      protected static Expression _MakeIfExpression(List<Tuple<Expression, Expression>> stmts, Int32 i = 0)
      {
         if (stmts[i].Item1 == null) // else
            return stmts[i].Item2;

         if (i == stmts.Count - 1)
            return Expression.IfThen(stmts[i].Item1, stmts[i].Item2);

         return Expression.IfThenElse(stmts[i].Item1, stmts[i].Item2, _MakeIfExpression(stmts, i + 1));
      }
   }
}
