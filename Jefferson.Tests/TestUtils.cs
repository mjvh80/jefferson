using System;
using Xunit;

namespace Jefferson.Tests
{
   internal static class TestUtils
   {
      public static void AssertThrows<T>(Func<T> a)
      {
         AssertThrows(() => { a(); });
      }

      public static void AssertThrows(Action a)
      {
         AssertThrows<Exception>(a);
      }

      public static void AssertThrows(Action a, String msg)
      {
         AssertThrows<Exception>(a, msg);
      }

      public static void AssertThrows<E>(Action a) where E : Exception
      {
         AssertThrows<E>(a, null);
      }

      public static void AssertThrows<E>(Action a, String message) where E : Exception
      {
         try
         {
            a();
         }
         catch (E)
         {
            // OK
            return;
         }

         Assert.True(false, message ?? "Expected exception of type " + typeof(E));
      }
   }
}
