using Jefferson.Parsing;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   [DebuggerDisplay("#{Name}")]
   public sealed class IfDirective : IDirective
   {
      private readonly Boolean _mAllowUnknownNames;

      /// <summary>
      /// Note if allowUnknownNames is false, the global option takes precedence currently.
      /// </summary>
      public IfDirective(Boolean allowUnknownNames = true) { _mAllowUnknownNames = allowUnknownNames; }

      public String Name
      {
         get { return "if"; }
      }

      public String[] ReservedWords
      {
         get { return new[] { "else", "elif" }; }
      }

      public Expression Compile(TemplateParserContext parserCtx, String arguments, String source)
      {
         if (source == null) throw parserCtx.SyntaxError(0, "#if directive may not be empty");

         var idx = 0;
         var contextParamAsObj = Expression.Convert(parserCtx.GetNthContext(0), typeof(Object));
         var endIfIdx = -1;

         // A list of predicate, body pairs.
         var ifStmt = new List<Tuple<Expression, Expression>>();

         for (; ; )
         {
            // Here we're handling if or elif.
            var expr = arguments;
            var closeIdx = 0;

            if (idx > 0) // elif
            {
               closeIdx = source.IndexOf("$$", idx);
               if (!(closeIdx >= 0))
               {
                  // should have been caught when looking for nested directives
                  throw new ArgumentException("found unmatched $$", "value");
               }

               expr = source.Substring(idx, closeIdx - idx);
               closeIdx += 2;
            }

            if (String.IsNullOrWhiteSpace(expr)) throw parserCtx.SyntaxError(idx, "Empty expression in if or elif.");

            endIfIdx = parserCtx.FindDirectiveEnd(source, closeIdx, "$$#else", "$$#elif");
            if (endIfIdx < 0) endIfIdx = source.Length;

            var oldOverride = parserCtx.OverrideAllowUnknownNames;
            if (_mAllowUnknownNames) parserCtx.OverrideAllowUnknownNames = true;

            var compiledPredicate = parserCtx.CompileExpression<Boolean>(expr);

            parserCtx.OverrideAllowUnknownNames = oldOverride;

            var compiledContents = parserCtx.Parse<Object>(source.Substring(closeIdx, endIfIdx - closeIdx));

            ifStmt.Add(Tuple.Create<Expression, Expression>(Expression.Invoke(compiledPredicate.Ast, contextParamAsObj),
                                                            Expression.Invoke(compiledContents, contextParamAsObj, parserCtx.Output)));

            if (endIfIdx == source.Length)
               break;

            // Else or Else if.
            if (!(endIfIdx <= source.Length - 6))
            {
                throw new ArgumentException("Contract assertion not met: endIfIdx <= source.Length - 6", nameof(source));
            }
            switch (source.Substring(endIfIdx, 6))
            {
               case "$$#els": // else
                  closeIdx = source.IndexOf("$$", endIfIdx + 2) + 2; // note: parser guarantees $$ exists
                  compiledContents = parserCtx.Parse<Object>(source.Substring(closeIdx, source.Length - closeIdx));
                  ifStmt.Add(Tuple.Create<Expression, Expression>(null, Expression.Invoke(compiledContents, contextParamAsObj, parserCtx.Output)));
                  goto EndIf;

               case "$$#eli": // elseif
                  idx = endIfIdx + "$$#elif".Length;
                  continue;

               default:
                  throw new InvalidOperationException();
            }
         }

      EndIf:
         return _MakeIfExpression(ifStmt);
      }

      private static Expression _MakeIfExpression(List<Tuple<Expression, Expression>> stmts, Int32 i = 0)
      {
         if (stmts == null)
         {
             throw new ArgumentNullException(nameof(stmts), "Contract assertion not met: stmts != null");
         }
         if (!(i >= 0 && i < stmts.Count))
         {
             throw new ArgumentException("Contract assertion not met: i >= 0 && i < stmts.Count", nameof(i));
         }

         if (stmts[i].Item1 == null) // else
            return stmts[i].Item2;

         if (i == stmts.Count - 1)
            return Expression.IfThen(stmts[i].Item1, stmts[i].Item2);

         return Expression.IfThenElse(stmts[i].Item1, stmts[i].Item2, _MakeIfExpression(stmts, i + 1));
      }
   }
}
