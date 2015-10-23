using Jefferson.Directives;
using System;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   namespace FooNamespace
   {
      public enum NsEnum
      {
         Foo = 7
      }
   }

   public class Test_Directive_Using
   {
      [Fact]
      public void Basic_using_directive_works()
      {
         var p = new TemplateParser(new UsingDirective());
         var result = p.Replace(@"
         $$#using Jefferson.Tests.FooNamespace /$$

         $$ NsEnum.Foo $$

         $$ NsEnum.Foo - 3 $$

         ", new TestContext());
         Assert.Contains("Foo", result.Trim());
         Assert.Contains("4", result.Trim());
      }

      [Theory]
      [InlineData("$$#using /$$", "Missing or empty arguments")]
      [InlineData("$$#using Foo.Bar$$ $$/using$$", "#using should be empty")]
      [InlineData("$$#using Foo Bar! /$$", "Given namespace contains invalid characters.")]
      public void Using_directive_syntax_tests(String invalidSyntax, String expectedError)
      {
         var p = new TemplateParser(new UsingDirective());
         var err = Assert.Throws<SyntaxException>(() => p.Replace(invalidSyntax, new TestContext()));
         Assert.Contains(expectedError, err.Message);
      }
   }
}
