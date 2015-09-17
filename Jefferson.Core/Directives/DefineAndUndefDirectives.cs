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

      public Boolean IsEmptyDirective
      {
         get { return true; }
      }

      public Expression Compile(Parsing.TemplateParserContext parserContext, String arguments, String source)
      {
         // Compile variables.
         var compiledVars = new Dictionary<String, CompiledExpression<Object, Object>>();

         for (var startIdx = 0; ; )
         {
            var varSepIdx = arguments.IndexOf(';', startIdx);

            // ; can only be used for multiple key value pair style definitions.
            var bindingLen = (varSepIdx < 0 ? arguments.Length : varSepIdx) - startIdx;
            if (bindingLen == 0) throw parserContext.SyntaxError(startIdx, "Invalid variable binding found: empty.");

            var binding = arguments.Substring(startIdx, bindingLen);

            var eqIdx = binding.IndexOf('=');
            if (eqIdx < 0)
            {
               throw parserContext.SyntaxError(startIdx, "Invalid variable binding: missing '='.");
            }

            var name = binding.Substring(0, eqIdx).Trim();
            if (!ExpressionParser<Object, Object>.IsValidName(name))
               throw parserContext.SyntaxError(0, "Variable '{0}' has an invalid name.", name);

            var value = binding.Substring(eqIdx + 1).Trim();
            compiledVars.Add(name, parserContext.CompileExpression<Object>(value));

            if (varSepIdx < 0) break;
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

      public Boolean IsEmptyDirective
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
