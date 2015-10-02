using Jefferson.Directives;
using System;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   public class Test_Directive_Literal
   {
      [Fact]
      public void Can_output_literal_jefferson_source()
      {
         var parser = new TemplateParser(new LiteralDirective());

         var result = parser.Replace(@"

         foo

         $$#literal$$
             $$#$$
             $$ no processing $$

             $$#bad

             $$#$$

             $$#if  /$$
             $$/if$$

             bar
         $$/literal$$

         qux
         ", new TestContext());

         Assert.Contains("$$#", result);
      }

      [Fact]
      public void Replacing_deep_with_literal_directives_runs_body()
      {
         var parser = new TemplateParser(new LiteralDirective());

         var result = parser.ReplaceDeep(@"
         $$#literal$$
             $$FieldOnCtx$$
         $$/literal$$
         ", new TestContext());

         Assert.Contains("fldOnCtx", result);
      }

      [Theory]
      [InlineData("$$#literal arg$$ foobar $$/literal$$", "#literal directive does not take arguments")]
      [InlineData("$$#literal /$$", "may not be empty")]
      [InlineData(@"
      $$#literal$$
         $$#literal$$
             $$ can not be nested $$
         $$/literal$$
      $$/literal$$
      ", "Unexpected '$$/' found")] // < todo this is a somewhat sucky error
      public void Literal_directive_bad_syntax(String source, String errorPart)
      {
         var parser = new TemplateParser(new LiteralDirective());
         var error = Assert.Throws<SyntaxException>(() => parser.Replace(source, new TestContext()));
         Assert.Contains(errorPart, error.Message);
      }

      [Fact]
      public void Comments_are_ignored()
      {
         var parser = new TemplateParser(new CommentDirective());

         var result = parser.Replace(@"

         foo

         $$#comment$$
             $$#$$
             $$ no processing $$

             $$#bad

             $$#$$

             $$#if  /$$
             $$/if$$

             bar
         $$/comment$$

         $$#comment I am also a comment /$$

         qux
         ", new TestContext());

         Assert.DoesNotContain("$$#", result);
         Assert.Contains("foo", result);
         Assert.DoesNotContain("also a", result);
      }
   }
}
