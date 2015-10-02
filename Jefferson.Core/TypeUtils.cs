using Jefferson.Extensions;
using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using BinOp = System.Func<System.Linq.Expressions.Expression, System.Linq.Expressions.Expression, System.Linq.Expressions.Expression>;

namespace Jefferson
{
   using System.Reflection;
   using BinConversion = Func<BinOp, BinOp>;

   internal static class TypeUtils
   {
      // public static Func<Expression, Expression> GetFunc<TIn, TOut>(Expression<Func<TIn, TOut>> f) { return e => 
      public static Func<Expression, Expression> GetFunc<TIn, TOut>(Expression<Func<TIn, TOut>> @out, Expression @in)
      {
         return e => Expression.Invoke(@out, @in);
      }
      public static Expression Invoke<TIn, TOut>(Expression<Func<TIn, TOut>> expr, Expression arg)
      {
         return Expression.Invoke(expr, arg);
      }
      public static Expression Inline<TInput, TOutput2>(Expression<Func<TInput, TOutput2>> repl, Expression arg)
      {
         return _ReplaceParameterVisitor.ReplaceParamWith(repl.Parameters[0], arg, repl.Body);
      }

      public static Func<Expression, Expression> GetConverter(Type from, Type to, Boolean? ignoreCase = null)
      {
         Contract.Requires(from != null);
         Contract.Requires(to != null);

         if (from == to) return e => e;

         if (to.IsAssignableFrom(from)) return e => Expression.Convert(e, to); // even necessary?

         // Anything converts to Object.
         if (to == typeof(Object))
            return e => Expression.Convert(e, typeof(Object));

         // Anything converts to String.
         if (to == typeof(String))
            return e => Inline<Object, String>(o => o == null ? null : o.ToString(), e.Convert<Object>());

         // Anything converts to Boolean.
         if (to == typeof(Boolean))
            return Convert_ToBool;

         // Strings convert to enums.
         if (to.IsEnum && from == typeof(String))
         {
            Utils.DebugAssert(ignoreCase != null);
            return e =>
            {
               e = Expression.Call(Utils.GetMethod(() => Enum.Parse(null, null, false)), Expression.Constant(to), e, Expression.Constant(ignoreCase));
               return Expression.Convert(e, to);
            };
         }

         // Ints convert to enums.
         if (to.IsEnum && from.IsIntegral())
            return e => Expression.Convert(e, to);

         // Enums convert to ints.
         if (to.IsIntegral() && from.IsEnum)
            return Convert_EnumToUnderlying;

         // Convert numbers, allowing for loss of precision.
         if (to.IsNumeric() && from.IsNumeric())
            return e => Expression.Convert(e, to);

         // We will *not* convert numbers to strings (e.g. '1' -> 1) right now. Not sure it's used so often 
         // and cannot be done without generating run-time errors.

         // This means: we don't provide a conversion, we could attempt Expression.Convert elsewhere though.
         return null;
      }

      public static Expression ResolveMethod(String name, Expression target, Type type, params Expression[] @params)
      {
         Boolean ignore;
         return ResolveMethod(name, target, type, false, out ignore, @params);
      }

      public static Expression ResolveMethod(String name, Expression target, Type type, Boolean ignoreCase, out Boolean ambiguous, params Expression[] @params)
      {
         ambiguous = false;

         var argTypes = @params.Select(p => p.Type);

         var bindingFlags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.InvokeMethod;

         // Optimization: First see if we can find *any* method of the given name, avoids heavier work.
         try
         {
            if (type.GetMethod(name, bindingFlags) == null)
               return null;
         }
         catch (AmbiguousMatchException)
         {
            // ignore, we'll resolve
         }

         var methods = type.GetMethods(bindingFlags);
         var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

         methods = methods.Where(m => m.Name.Equals(name, comparison) && m.GetParameters().Length == @params.Length).ToArray();
         if (methods.Length == 0) return null;

         var candidates = methods.Select(m => new
         {
            Method = m,
            ConvertedParameters = m.GetParameters().Zip(argTypes, (p, arg) => new { p.ParameterType, arg })
                                   .Select(pair => GetConverter(from: pair.arg, to: pair.ParameterType))
                                   .ToArray()
         })
         .Where(c => c.ConvertedParameters.All(param => param != null))
         .ToArray();

         ambiguous = candidates.Length > 1;
         if (candidates.Length != 1) return null;

         // Call the method, converting all parameters to match.
         var method = candidates[0].Method;
         return Expression.Call(method.IsStatic ? null : target, method, @params.Select((p, i) => candidates[0].ConvertedParameters[i](p)));
      }


      #region Spefic Type Conversion Routines

      public static readonly Expression False = Expression.Constant(false);
      public static readonly Expression Null = Expression.Constant(null);

      public static readonly Func<Expression, Expression> Convert_ToBool = e =>
      {
         if (e._is_<Boolean>()) return e;
         if (e.IsNullConstant()) return False; // shortcut

         if (e.Type.IsValueType)
            return Expression.Not(e.CallInline("IsZero"));

         // Strings.
         //if (e._is_<String>()) return _GetExpr<String, Boolean>(e, s => s != null && s.Length != 0, inline: false);
         if (e._is_<String>())
            return Inline<String, Boolean>(s => s != null && s.Length != 0, e);

         // Any reference type, false if null, true otherwise. Could consider collections, not sure. In js !![] === true.
         return e.IfTypeIs<Boolean>(e.DynCast<Boolean>(), Expression.Not(Expression.Equal(e, Null)));
      };

      public static readonly Func<Expression, Expression> Convert_EnumToUnderlying = e => Expression.Convert(e, Enum.GetUnderlyingType(e.Type));

      public static readonly BinConversion WidenNumbers = f => (e1, e2) =>
      {
         if (e1.Type.IsEnum) e1 = Convert_EnumToUnderlying(e1);
         if (e2.Type.IsEnum) e2 = Convert_EnumToUnderlying(e2);

         if (e1.Type == e2.Type) return f(e1, e2);

         // One is not a value type thus not a number. Note we won't attempt to cast etc.
         if (!e1.IsNumeric() || !e2.IsNumeric()) return f(e1, e2);

         if (e1.IsIntegral() && e2.IsIntegral())
         {
            var d = Marshal.SizeOf(e1.Type) - Marshal.SizeOf(e2.Type);
            if (d < 0) // e2 > e1
               return f(Expression.Convert(e1, e2.Type), e2);
            else if (d > 0) // e1 > e2
               return f(e1, Expression.Convert(e2, e1.Type));

            // Types are of the same size, but one is signed and the other isn't.
            switch (Marshal.SizeOf(e1.Type))
            {
               // As C# does, lift to int.
               case 1:
               case 2:
                  return f(Expression.Convert(e1, typeof(Int32)), Expression.Convert(e2, typeof(Int32)));
               case 4:
               case 8: // case not supported by C#, but we'll simply convert to signed
                  return f(Expression.Convert(e1, typeof(Int64)), Expression.Convert(e2, typeof(Int64)));

               default: throw new InvalidOperationException();
            }
         }
         // Convert integral types to floats or doubles, some loss of precision is allowed.
         else if (!e1.IsIntegral())
            return f(e1, Expression.Convert(e2, e1.Type));
         else // e2 is not integral but e1 is
            return f(Expression.Convert(e1, e2.Type), e2);
      };

      public static readonly BinConversion ConvertIfOneBoolean = f => (e1, e2) =>
      {
         Func<Expression, Expression> convertToBool = e => GetConverter(e.Type, typeof(Boolean))(e);

         if (e1._is_<Boolean>() || e2._is_<Boolean>())
            return f(convertToBool(e1), convertToBool(e2));

         if (e1.Type.IsValueType && e2.Type.IsValueType)
            return f(e1, e2);

         // At runtime, see if we have boxed bools.
         // As bools are a type to which we convert *any* other type, we already don't have type safety.
         // It'd then be strange if a boxed true would not evaluate as such.
         return e1.IfTypeIs<Boolean>(f(e1.DynCast<Boolean>(), convertToBool(e2)),
                  e2.IfTypeIs<Boolean>(f(convertToBool(e1), e2.DynCast<Boolean>()),
                     f(e1, e2)));
      };


      #endregion
   }
}
