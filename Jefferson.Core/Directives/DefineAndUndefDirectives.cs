using Jefferson.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Jefferson.Directives
{
   public class DefineDirective : IDirective
   {
      public String Name
      {
         get { return "define"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Boolean MayBeEmpty
      {
         get { return true; }
      }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         // Compile variables.
         var compiledVars = new Dictionary<String, CompiledExpression<Object, Object>>();

         var haveSource = !String.IsNullOrEmpty(source);

         for (var startIdx = 0; ; )
         {
            var varSepIdx = arguments.IndexOf(';', startIdx);

            // ; can only be used for multiple key value pair style definitions.
            var bindingLen = (varSepIdx < 0 ? arguments.Length : varSepIdx) - startIdx;
            if (bindingLen == 0) throw parserContext.SyntaxError(startIdx, "Invalid variable binding found: empty.");

            var binding = arguments.Substring(startIdx, bindingLen);

            var eqIdx = binding.IndexOf('=');

            var name = eqIdx < 0 ? binding.Substring(0) : binding.Substring(0, eqIdx);
            name = name.Trim();
            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

            if (eqIdx >= 0)
            {
               if (haveSource)
                  throw parserContext.SyntaxError(startIdx, "#define directive is not empty, unexpected '='");

               var value = binding.Substring(eqIdx + 1).Trim();
               compiledVars.Add(name, parserContext.CompileExpression<Object>(value));
            }
            else
            {
               // We have a body.
               // Value in this case is not an expression, but the result of applying the template.
               var parsedDefinition = parserContext.Parse<Object>(source);

               // Add an expression to get and compile at runtime.
               // Todo: this could perhaps cache, if used more than once.
               // Todo(2): can probably be done more efficiently as we don't need the compiledexpression step, so we have some
               // extra lambda invocation overhead.
               var sb = Expression.Variable(typeof(StringBuilderOutputWriter));
               var ctxParam = Expression.Parameter(typeof(Object));
               compiledVars.Add(name, new CompiledExpression<Object, Object>
               {
                  Ast = Expression.Lambda<Func<Object, Object>>(
                           Expression.Block(new[] { sb },
                              Expression.Assign(sb, Expression.New(typeof(StringBuilderOutputWriter))),
                              Expression.Invoke(parsedDefinition, ctxParam, sb),
                              Expression.Convert(
                                 Expression.Call(sb, Utils.GetMethod<StringBuilderOutputWriter>(s => s.GetOutput())),
                                 typeof(Object))),
                           ctxParam),
                  OutputType = typeof(String)
               });
            }

            if (varSepIdx < 0) break;

            if (haveSource)
               throw parserContext.SyntaxError(varSepIdx, "Unexpected ';', #define is not empty.");

            startIdx = varSepIdx + 1;
         }

         var currentContext = parserContext.GetNthContext(0);
         var body = new List<Expression>(compiledVars.Count + 1);

         foreach (var kvp in compiledVars)
         {
            var valExpr = Expression.Convert(Expression.Invoke(compiledVars[kvp.Key].Ast, Expression.Convert(currentContext, typeof(Object))), kvp.Value.OutputType);
            body.Add(parserContext.SetVariable(currentContext, kvp.Key, valExpr));
         }

         return Expression.Block(body);
      }
   }

   public class UndefDirective : IDirective
   {
      public String Name
      {
         get { return "undef"; }
      }

      public String[] ReservedWords
      {
         get { return null; }
      }

      public Boolean MayBeEmpty
      {
         get { return true; }
      }

      public System.Linq.Expressions.Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         var args = arguments.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(n => n.Trim());
         foreach (var name in args)
            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

         var currentContext = parserContext.GetNthContext(0);
         var body = new List<Expression>();

         foreach (var name in args)
            body.Add(parserContext.RemoveVariable(currentContext, name));

         return Expression.Block(body);
      }
   }
}
