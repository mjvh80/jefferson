using Jefferson.Binders;
using Jefferson.Output;
using Jefferson.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Jefferson.Directives
{
   public sealed class EachDirective : IDirective
   {
      private readonly Boolean _mIsReadonly;

      public EachDirective(Boolean readOnly = false)
      {
         _mIsReadonly = readOnly;
      }

      public String Name
      {
         get { return "each"; }
      }

      public String[] ReservedWords
      {
         get { return new[] { "else" }; }
      }

      public Boolean MayBeEmpty
      {
         get { return false; }
      }

      public Expression Compile(TemplateParserContext parserCtx, String args, String source)
      {
         var closeIdx = 0;

         var endIdx = parserCtx.FindDirectiveEnd(source, closeIdx, "$$#else$$");
         if (endIdx < 0) endIdx = source.Length;

         var body = source.Substring(closeIdx, endIdx - closeIdx);

         String empty = null;
         if (endIdx != source.Length)
         {
            var ifClose = endIdx + "$$#else$$".Length;
            endIdx = source.Length;
            empty = source.Substring(ifClose, endIdx - ifClose);
         }

         var compiledEachExpr = parserCtx.CompileExpression<IEnumerable<Object>>(args);

         // Compile else, but against the *current* context.
         var compiledEachEmpty = empty == null ? (Expression)Expression.Constant(null, typeof(Action<Object, IOutputWriter>)) : parserCtx.Parse<Object>(empty);

         // Push context type. todo: test this is indeed enumerable, handle if not
         var binder = _mIsReadonly ? new ReadOnlyBinder() : null;
         parserCtx.PushScope(compiledEachExpr.OutputType.IsGenericType && compiledEachExpr.OutputType.GetGenericTypeDefinition() == typeof(IEnumerable<>) ?
                                    compiledEachExpr.OutputType.GetGenericArguments()[0] :
                                    compiledEachExpr.OutputType.GetInterface("IEnumerable`1").GetGenericArguments()[0],
                             binder);

         // Compile the body against the pushed type.
         var compiledEachBody = parserCtx.Parse<Object>(body);

         // Ain't she a beauty!
         Action<Object, Action<Object, IOutputWriter>, Action<Object, IOutputWriter>, IOutputWriter, List<Object>, Func<Object, IEnumerable<Object>>> compiledEach = (ctx, outputEnum, outputElse, buffer, contexts, getEnum) =>
         {
            var enumerable = getEnum(ctx) as IEnumerable<Object>;
            if (enumerable != null && enumerable.Any())
               foreach (var x in enumerable)
               {
                  contexts.Add(x);
                  outputEnum(x, buffer);
                  contexts.RemoveAt(contexts.Count - 1);
               }
            else if (outputElse != null)
               outputElse(ctx, buffer);
         };

         var result = Expression.Invoke(Expression.Constant(compiledEach),
                                           Expression.Convert(parserCtx.GetNthContext(1), typeof(Object)),
                                           compiledEachBody,
                                           compiledEachEmpty,
                                           parserCtx.Output,
                                           parserCtx.RuntimeContexts,
                                           compiledEachExpr.Ast);

         parserCtx.PopScope();

         return Expression.Block(result);
      }
   }
}
