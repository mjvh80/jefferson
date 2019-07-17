﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Jefferson
{
   internal static class Utils
   {
      public static Type FindTypeInAppDomain(String fullName, Boolean except = false)
      {
         var result = AppDomain.CurrentDomain.GetAssemblies()
                                             .Select(asm => Type.GetType(fullName + ", " + asm.FullName, throwOnError: false))
                                             .Where(t => t != null);

         if (except)
         {
            if (!result.Any()) throw Error("Could not find type '{0}' in current appdomain.", fullName);
            if (result.Skip(1).Any()) throw Error("Type '{0}' found in multiple assemblies.", fullName);

            return result.First();
         }

         return result.FirstOrDefault();
      }

      public static String ToString(Object o)
      {
         return o == null ? null : o.ToString();
      }

      public static String NullToEmpty(String s)
      {
         return s ?? "";
      }

      public static Exception Error(String msg, params Object[] args)
      {
         return new Exception(String.Format(msg, args));
      }

      public static Exception InvalidOperation(String msg, params Object[] args)
      {
         return new InvalidOperationException(String.Format(msg, args));
      }

      public static MethodInfo GetMethod(Expression<Action> action)
      {
         Ensure.NotNull(action, "action");
         var methodCall = action.Body as MethodCallExpression;
         if (methodCall == null) throw new InvalidOperationException("expected method call as body of given action");
         return methodCall.Method;
      }

      public static MethodInfo GetMethod<TArg>(Expression<Action<TArg>> action)
      {
         Ensure.NotNull(action, "action");
         var methodCall = action.Body as MethodCallExpression;
         if (methodCall == null) throw new InvalidOperationException("expected method call as body of given action");
         return methodCall.Method;
      }

      public static ConstructorInfo GetConstructor(Expression<Action> action)
      {
         Ensure.NotNull(action, "action");
         var newExpr = action.Body as NewExpression;
         if (newExpr == null) throw new InvalidOperationException("expected new object creation expression");
         return newExpr.Constructor;
      }

      public static MethodInfo GetOneArgTraceWriteLine()
      {
         return typeof(Trace).GetMethod("WriteLine", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(String) }, null);
      }

      public static Expression GetSimpleTraceExpr(String msg)
      {
         return Expression.Call(null, Utils.GetOneArgTraceWriteLine(), Expression.Constant(msg));
      }
      public static Expression GetSimpleTraceExpr(String msg, params Object[] args)
      {
         return Utils.GetSimpleTraceExpr(String.Format(msg, args));
      }

      public static readonly Expression NopExpression = Expression.Default(typeof(Object)); // is there anything better?

      /// <summary>
      ///  Return the minimum non-negative index or -1 if empty.
      /// </summary>
      public static Int32 MinNonNeg(IEnumerable<Int32> seq)
      {
         Ensure.NotNull(seq, "seq");
         var nonZs = seq.Where(n => n >= 0);
         return nonZs.Any() ? nonZs.Min() : -1;
      }

      public static Int32 MinNonNeg(params Int32[] seq)
      {
         return MinNonNeg((IEnumerable<Int32>)seq);
      }
   }
}
