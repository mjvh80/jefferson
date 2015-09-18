using Jefferson.Directives;
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   class SyntaxTests
   {
      [Theory]
      [InlineData(@"
         $$#let x$$
            $$#let nested$$
            $$#out$$
            $$/let$$
         $$#out$$

         $$/let$$
       ")]
      public void Can_parse_correct_syntax(String input)
      {
         var parser = new TemplateParser(new LetDirective(), new EachDirective()).Parse<TestContext>(input);
      }

      [Theory]
      [InlineData(@"
      $$#let x$$
      $$#out$$..$$/out$$
      $$/let$$
      ")]
      [InlineData(@"
      $$#let x$$
      $$#out$$
      $$#out$$
      $$/let$$
      ")]
      // /if/ is a regular expression, however, this edge case is no longer allowed.
      // Note that it can easily be worked around by using $$ /if/ $$, say.
      [InlineData(@"
      $$#if true$$ blah $$/if/$$ blah $$/if$$
      ")]
      public void Malformed_syntax_is_rejected(String input)
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new LetDirective(), new EachDirective()).Parse<TestContext>(input));

         Trace.WriteLine(error.Message);
      }
   }
}
