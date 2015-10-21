using Jefferson.Directives;
using System;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   public class Test_Directive_If
   {
      TemplateParser replacer;
      TestContext context;

      public Test_Directive_If()
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
      }

      [Fact]
      public void If_directives_work()
      {
         Assert.Equal("Foo is the new bar!", replacer.Replace("Foo is the new $$#if b1$$bar$$/if$$!", context));
         Assert.Equal("Foo is the new !", replacer.Replace("Foo is the new $$#if !b1$$bar$$/if$$!", context));

         // Test nested if:
         Assert.Equal("Foo is the new baa qux aarrr!", replacer.Replace("Foo is the new $$#if b1$$b$$#if !b2$$aa $$foobar$$ aa$$/if$$rrr$$/if$$!", context));

         // If/Else tests
         Assert.Equal("Foo is the new ELSE !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#else$$ ELSE $$/if$$!", context));
         Assert.Equal("Foo is the new ELIF !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#elif (b1)$$ ELIF $$#else$$ ELSE $$/if$$!", context));
         Assert.Equal("Foo is the new  NESTED  ELIF !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#elif b1$$ $$#if b1$$ NESTED $$/if$$ ELIF $$#else$$ ELSE $$/if$$!", context));
         Assert.Equal("Foo is the new  NESTED2  ELIF !", replacer.Replace("Foo is the new$$#if !b1$$ IF $$#elif b1$$ $$#if !b1$$ HAHA $$#else$$ NESTED2 $$/if$$ ELIF $$#else$$ ELSE $$/if$$!", context));
      }

      [Fact]
      public void If_directive_allows_unknown_names_by_default()
      {
         Assert.Equal("Foo is blah", replacer.Replace("Foo is$$#if FOO_DONT_EXIST$$ xxx $$#else$$ blah$$/if$$", context).Trim());
      }

      [Fact]
      public void Can_make_if_directive_sensitive_to_unknown_names()
      {
         var p = new TemplateParser(new IfDirective(allowUnknownNames: false));
         var error = Assert.Throws<SyntaxException>(() => p.Replace("$$#if FOO_DONT_EXIST $$ blah $$/if$$", context));
         Assert.Contains("Expected known name", error.Message);
      }

      [Fact]
      public void If_does_not_allow_unknown_names_in_body_by_default()
      {
         var error = Assert.Throws<SyntaxException>(() => replacer.Replace("Foo is$$#if true$$ x = $$IDONTEXIST$$ $$#else$$ blah$$/if$$", context).Trim());
         Assert.Contains("Expected known name", error.Message);
      }

      [Fact]
      public void Cannot_have_elif_after_else()
      {
         var error = Assert.Throws<SyntaxException>(() => replacer.Replace(@"
         $$#if true$$

         $$#else$$

         $$#elif$$

         $$/if$$
         ", context));

         // todo: error should be better perhaps
         Assert.Contains("Could not find directive 'elif'", error.Message);
      }

      [Fact]
      public void Can_have_arbitrary_space_in_if_expressions()
      {
         var result = replacer.Replace(@"
         $$#if
            true

         $$

         $$#elif
               false  $$

         $$/if$$
         ", context);
      }

      [Fact]
      public void If_directive_ignores_unknown_names_if_that_is_global_default()
      {
         var p = new TemplateParser(new TemplateOptions { AllowUnknownNames = true }, new IfDirective(allowUnknownNames: false));
         var result = p.Replace("$$#if FOO_UNKONW$$ xx $$#else$$ yy $$/if$$", context);
         Assert.Equal("yy", result.Trim());
      }

      [Theory]
      [InlineData("$$#if$$ $$/if$$")][InlineData("$$#if$$")][InlineData("$$#if/$$")]
      [InlineData("$$#if true /$$")][InlineData("$$#if true$$ . $$#else$$ . $$#else$$ . $$/if$$")]
      public void Detect_bad_if_syntax(String input)
      {
         Assert.Throws<SyntaxException>(() => replacer.Replace(input, context));
      }

      [Theory]
      [InlineData(@"
      $$#if true$$
      $$#elif false

      $$#/endif$$
      ", "Failed to find directive end")]
      public void Detect_bad_if_syntaxt(String input, String errorPart)
      {
         var error = Assert.Throws<SyntaxException>(() => replacer.Replace(input, context));
         Assert.Contains(errorPart, error.Message);
      }
   }
}
