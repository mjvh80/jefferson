using System;
using Xunit;

namespace Jefferson.Tests
{
   public static class TestUtils
   {
      public static Exception AssertThrowsContractException(Action a)
      {
         Assert.NotNull(a);
         try
         {
            a();
            Assert.True(false, "Expected a Code Contract failure.");
            throw new InvalidOperationException();
         }
         catch (Exception e)
         {
            Assert.True(e.GetType().Namespace.StartsWith("System.Diagnostics.Contracts"));
            return e;
         }
      }
   }
}
