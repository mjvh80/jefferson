using Jefferson.Directives;
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   public class Test_Directive_Pragma
   {
      TemplateParser replacer;
      TestContext context;

      public Test_Directive_Pragma()
      {
         var ctx = context = new TestContext();
         replacer = new TemplateParser();

         ctx.Add("$$def$$", "$$abc$$ en bah"); // note: abc is not defined
         ctx.Add("$$abc$$", "boe");

         ctx.Add("$$b1$$", "true", true);
         ctx.Add("$$b2$$", "false", true);

         ctx.Add("$$c1$$", "true", false);
         ctx.Add("$$c2$$", "false", false);

         ctx.Add("recursive", "GetRecursive()", true);
         ctx.Add("foobar", "qux");

         ctx.Add("one", "0", true);
      }

      [Fact]
      public void Pragmas_are_ignored_if_not_understood()
      {
         var result = replacer.Replace(@"
         $$#pragma whatever /$$
         foobar
         ", context);

         Assert.Equal("foobar", result.Trim());
      }

      [Theory]
      [InlineData("$$#pragma/$$", "#pragma arguments should not be empty")]
      [InlineData("$$#pragma foobar$$ body $$/pragma$$", "#pragma should not have a body")]
      public void Bad_pragma_syntax(String input, String errorPart)
      {
         var error = Assert.Throws<SyntaxException>(() => replacer.Replace(input, context));
         Assert.Contains(errorPart, error.Message);
      }
   }
}
