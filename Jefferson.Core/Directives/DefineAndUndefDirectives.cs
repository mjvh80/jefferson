using Jefferson.Output;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

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

      // note: return null not empty
      private String[] _ParseParameters(Parsing.TemplateParserContext parserContext, String input, out String name)
      {
         var idx = input.IndexOf('(');
         if (idx < 0)
         {
            name = input;
            return null;
         }

         var lidx = input.IndexOf(')', idx + 1);
         if (lidx < 0) throw parserContext.SyntaxError(idx, "Missing ')'.");

         name = input.Substring(0, idx);

         if (lidx == idx + 1)
            return null; // () no params

         var @params = new List<String>();
         for (var i = idx + 1; i < input.Length; )
         {
            var endIdx = input.IndexOf(',', i);
            if (endIdx < 0) endIdx = lidx;

            @params.Add(input.Substring(i, endIdx - i).Trim());

            if (endIdx == lidx) break;
            i = endIdx + 1;
         }
         return @params.ToArray();
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

            var @params = _ParseParameters(parserContext, name, out name);
            if (eqIdx >= 0 && @params != null)
               throw parserContext.SyntaxError(0, "Unexpected arguments found in empty define directive.");

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
               var haveParams = @params != null;

               IVariableBinder existingBinder = null;
               _ParameterBinder paramBinder = null;
               if (haveParams)
               {
                  paramBinder = new _ParameterBinder();
                  paramBinder.ParamDecls = new Dictionary<String, ParameterExpression>();
                  foreach (var p in @params)
                     paramBinder.ParamDecls.Add(p, Expression.Variable(typeof(String), p));

                  existingBinder = parserContext.ReplaceCurrentVariableBinder(paramBinder);
               }

               // We have a body.
               // Value in this case is not an expression, but the result of applying the template.
               var parsedDefinition = parserContext.Parse<Object>(source);

               // Add an expression to get and compile at runtime.
               // Todo: this could perhaps cache, if used more than once.
               // Todo(2): can probably be done more efficiently as we don't need the compiledexpression step, so we have some
               // extra lambda invocation overhead.
               var sb = Expression.Variable(typeof(StringBuilderOutputWriter));
               var ctxParam = Expression.Parameter(typeof(Object));

               if (haveParams)
               {
                  Type funcType;
                  switch (@params.Length)
                  {
                     case 1: funcType = typeof(Func<String, String>); break;
                     case 2: funcType = typeof(Func<String, String, String>); break;
                     case 3: funcType = typeof(Func<String, String, String, String>); break;
                     case 4: funcType = typeof(Func<String, String, String, String, String>); break;
                     case 5: funcType = typeof(Func<String, String, String, String, String, String>); break;
                     default:
                        throw parserContext.SyntaxError(0, "#define directives support up to 5 parameters currently.");
                  }

                  compiledVars.Add(name, new CompiledExpression<Object, Object>
                  {
                     Ast = Expression.Lambda<Func<Object, Object>>(
                        // The result *value* here is not a string, but a Func<String, String> (for one parameter).
                        // Thus when the define is used, the Func is evaluated.
                              Expression.Lambda(funcType,
                                 Expression.Block(new[] { sb },
                                    Expression.Assign(sb, Expression.New(typeof(StringBuilderOutputWriter))),
                                    Expression.Invoke(parsedDefinition, ctxParam, sb),
                                       Expression.Call(sb, Utils.GetMethod<StringBuilderOutputWriter>(s => s.GetOutput()))),
                                 paramBinder.ParamDecls.Values),
                              ctxParam),
                     OutputType = funcType
                  });
               }
               else
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

               if (haveParams)
               {
                  parserContext.ReplaceCurrentVariableBinder(existingBinder);
               }
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

      private class _ParameterBinder : IVariableBinder
      {
         public Dictionary<String, ParameterExpression> ParamDecls;
         public IVariableBinder WrappedBinder;

         // Update variable declaration to compile the variable name.
         public Expression BindVariable(Expression currentContext, String name)
         {
            ParameterExpression variable;
            if (ParamDecls.TryGetValue(name, out variable))
               return variable;

            return WrappedBinder == null ? null : WrappedBinder.BindVariable(currentContext, name);
         }

         public Expression BindVariableToValue(Expression currentContext, String name, Expression value)
         {
            // todo: this error sucks, because it's not clear where in the source this is, we need more context
            if (ParamDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot set variable '{0}' because it has been bound to a define parameter.", name);

            return WrappedBinder == null ? null : WrappedBinder.BindVariableToValue(currentContext, name, value);
         }

         public Expression UnbindVariable(Expression currentContext, String name)
         {
            if (ParamDecls.ContainsKey(name))
               throw SyntaxException.Create(null, null, "Cannot unset variable '{0}' because it has been bound to a define parameter.", name);

            return WrappedBinder == null ? null : WrappedBinder.UnbindVariable(currentContext, name);
         }
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
