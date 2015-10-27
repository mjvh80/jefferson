using Jefferson.Directives;
using System;
using Xunit;
using Xunit.Extensions;

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

      [Fact]
      public void Error_directive_can_have_body()
      {
         var error = Assert.Throws<Exception>(() => new TemplateParser(new DefineDirective(), new ErrorDirective()).Replace(@"
         $$#define foo = 'hello world' /$$         
         $$#error$$
            $$ foo $$
         $$/error$$
         ", new TestContext()));
         Assert.Contains("hello world", error.Message);
      }

      [Theory]
      [InlineData("$$#error /$$", "Either argument or body expected")]
      public void Error_directive_edge_cases(String code, String errorPart)
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new ErrorDirective()).Replace(code, new TestContext()));
         Assert.Contains(errorPart, error.Message);
      }

      [Serializable]
      public class TestException : Exception
      {
         public TestException(String msg): base(msg) {}
      }

      [Serializable]
      public class BadTestException : Exception
      {
         public BadTestException() : base() {} // missing ctor with string arg
      }

      [Fact]
      public void Can_define_custom_exception_type()
      {
         Assert.Throws<TestException>(() => new TemplateParser(new ErrorDirective(typeof(TestException))).Replace("$$#error foobar /$$", new TestContext()));
      }

      [Fact]
      public void Bad_exception_type_is_rejected()
      {
         Assert.Throws<InvalidOperationException>(() => new TemplateParser(new ErrorDirective(typeof(BadTestException))).Replace("$$#error foobar /$$", new TestContext()));
      }
   }
}
