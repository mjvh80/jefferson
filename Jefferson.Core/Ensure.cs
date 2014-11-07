using System;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Jefferson
{
   internal static class Ensure
   {
      [DebuggerHidden]
      public static void NotNull(Object reference, String name = null)
      {
         if (reference == null)
            if (name == null)
               throw new ArgumentNullException("An argument is null.");
            else
               throw new ArgumentNullException(String.Format("Argument '{0}' is null.", name));
      }

      [DebuggerHidden]
      public static void NotNull(params Object[] args)
      {
         if (args.Any(o => o == null))
            throw new ArgumentNullException("Some argument is null.");
      }

      [DebuggerHidden]
      public static void NotNullOrEmpty(String reference, String name = null)
      {
         if (String.IsNullOrEmpty(reference))
            if (name == null)
               throw new ArgumentException("An argument is null or empty.");
            else
               throw new ArgumentException(String.Format("Argument '{0}' is null or empty.", name));
      }

      // dont use
      internal static void NotNull<T>(Expression<Func<T>> expr)
      {
         dynamic fieldAccess = expr.Body;
         MemberInfo member = fieldAccess.Member;
         Object value = fieldAccess.Expression.Value;
         if (((FieldInfo)member).GetValue(value) == null) throw new ArgumentNullException(member.Name);
      }
   }
}
