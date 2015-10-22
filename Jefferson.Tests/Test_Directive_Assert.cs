using Jefferson.Directives;
using System;
using Xunit;

namespace Jefferson.Tests
{
   public class Test_Directive_Assert
   {
      [Fact]
      public void Assert_works()
      {
         var error = Assert.Throws<Exception>(() => new TemplateParser(new AssertDirective()).Replace("$$#assert false foo bar /$$", new TestContext()));
         Assert.Contains("Assertion Failure", error.Message);
         Assert.Contains("foo bar", error.Message);

         var r = new TemplateParser(new AssertDirective()).Replace("$$#assert (true) foo bar /$$", new TestContext());
      }
   }
}
