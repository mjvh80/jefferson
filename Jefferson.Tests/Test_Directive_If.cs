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
