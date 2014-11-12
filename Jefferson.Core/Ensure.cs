using System;
using System.Diagnostics;

namespace Jefferson
{
   internal static class Ensure
   {
      [DebuggerHidden]
      public static void NotNull(Object reference, String name)
      {
         Ensure.NotNullOrEmpty(name, "name");

         if (reference == null)
            throw new ArgumentNullException(String.Format("Argument '{0}' is null.", name));
      }

      [DebuggerHidden]
      public static void NotNullOrEmpty(String reference, String name)
      {
         if (String.IsNullOrEmpty(name)) throw new ArgumentException("Argument 'name' is null or empty.");

         if (String.IsNullOrEmpty(reference))
            throw new ArgumentException(String.Format("Argument '{0}' is null or empty.", name));
      }
   }
}
