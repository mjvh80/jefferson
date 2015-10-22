using Jefferson.Directives;
using System;
using Xunit;

namespace Jefferson.Tests
{
   public class Test_Directive_Error
   {
      [Fact]
      public void Error_directive_throws_an_error()
      {
         var error = Assert.Throws<Exception>(() => new TemplateParser(new ErrorDirective()).Replace("$$#error foo bar! /$$", new TestContext()));
         Assert.Contains("foo bar!", error.Message);
      }

      // todo: custom exception type

      // todo: bad syntax
   }
}
