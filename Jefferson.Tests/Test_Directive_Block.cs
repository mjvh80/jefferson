using Jefferson.Directives;
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   public class Test_Directive_Block
   {
      TemplateParser replacer;
      TestContext context;

      public Test_Directive_Block()
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
      public void Block_directives_work()
      {
         var p = new TemplateParser(new LetDirective(), new BlockDirective());
         var result = p.Replace(
@"
$$#let b1 = 'test'$$
   Can't get parent b1: ($$b1$$).
$$/let$$
", context);

         // Trace.WriteLine(result);

         result = p.Replace(
@"
$$#block$$
$$#let b1 = 'test'$$
   Can't get parent b1: ($$b1$$), now I can: ($$ $1.b1 $$).
$$/let$$
$$/block$$
", context);

         // Trace.WriteLine(result);
      }

      [Theory]
      [InlineData("$$#block/$$")]
      [InlineData("$$#block /$$")]
      public void Block_may_not_be_empty(String src)
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new BlockDirective()).Replace(src, context));
      }

      [Fact]
      public void Can_create_a_block_scope_for_eg_define()
      {
         var p = new TemplateParser(new BlockDirective(), new DefineDirective());
         var r = p.Replace(@"
         start: $$one$$
         $$#define one = 1/$$
         $$#block$$
         before: $$one$$
         $$#define one = 2 /$$
         after: $$one$$
         $$/block$$
         finally: $$one$$
         
         ", context);

         //Trace.WriteLine(r);

         Assert.Contains("start: 0", r);
         Assert.Contains("before: 1", r);
         Assert.Contains("after: 2", r);
         Assert.Contains("finally: 1", r);
      }

      [Fact]
      public void Can_rebind_let_var_in_blockscope()
      {
         var p = new TemplateParser(new BlockDirective(), new LetDirective(), new DefineDirective());
         var r = p.Replace(@"
           $$#let x = 1$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              in: $$x$$
           $$/block$$
           after: $$x$$
           $$/let$$
         ", context);

         Assert.Contains("in: 2", r);
         Assert.Contains("after: 1", r);
      }

      [Fact]
      public void Can_undef_defined_variable_in_block()
      {
         var p = new TemplateParser(new BlockDirective(), new LetDirective(), new DefineDirective(), new UndefDirective());
         var r = p.Replace(@"
           $$#let x = 1$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              $$#undef x/$$ << TEST HERE
              in: $$x$$
           $$/block$$
           after: $$x$$
           $$/let$$
         ", context);

         Assert.Contains("in: 1", r);
      }

      [Fact]
      public void Cannot_undef_outside_of_block_in_let_scope()
      {
         var p = new TemplateParser(new BlockDirective(), new LetDirective(), new DefineDirective(), new UndefDirective());
         var r = Assert.Throws<SyntaxException>(() => p.Replace(@"
           $$#let x = 1$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              $$#undef x/$$
              $$#undef x/$$ << TEST HERE
              in: $$x$$
           $$/block$$
           after: $$x$$
           $$/let$$
         ", context));

         Assert.Contains("Cannot unset variable 'x'", r.Message);
      }

      [Fact]
      public void Can_unbind_outside_of_block()
      {
         var p = new TemplateParser(new BlockDirective(), new LetDirective(), new DefineDirective(), new UndefDirective());
         var r = Assert.Throws<SyntaxException>(() => p.Replace(@"
           $$#define x = 1 /$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              $$#undef x/$$
              $$#undef x/$$ << TEST HERE
              in: $$x$$
           $$/block$$
           after: $$x$$
         ", context));

         Assert.Contains("could not resolve 'x'", r.Message);
      }

      [Fact]
      public void Can_unbind_outside_of_block_2()
      {
         var p = new TemplateParser(new BlockDirective(), new LetDirective(), new DefineDirective(), new UndefDirective());
         var r = p.Replace(@"
           $$#define x = 1 /$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              $$#undef x/$$
              $$#undef x/$$<< TEST HERE
           $$/block$$
         ", context);
      }

      [Fact]
      public void Cannot_unbind_outside_of_block_if_disabled()
      {
         var p = new TemplateParser(new BlockDirective(enableUnbindOutsideOfBlock: false), new LetDirective(), new DefineDirective(), new UndefDirective());
         var r = Assert.Throws<SyntaxException>(() => p.Replace(@"
           $$#define x = 1 /$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              $$#undef x/$$
              $$#undef x/$$<< TEST HERE
           $$/block$$
         ", context));

         Assert.Contains("Cannot unset variable 'x'", r.Message);
      }

      [Fact]
      public void Can_access_outer_scope()
      {
         var p = new TemplateParser(new BlockDirective(), new LetDirective(), new DefineDirective(), new UndefDirective());
         var r = p.Replace(@"
           $$#define x = 1 /$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              result: $$x$$ and $$ $1.x $$
           $$/block$$
         ", context);

         Assert.Contains("result: 2 and 1", r);
      }

      [Fact]
      public void Can_nest_blocks()
      {
         var p = new TemplateParser(new BlockDirective(), new LetDirective(), new DefineDirective(), new UndefDirective());
         var r = p.Replace(@"
           $$#define x = 1 /$$
           before: $$x$$
           $$#block$$
              $$#define x = '2' /$$
              $$#block$$
              $$#define x = '3' /$$
              result: $$x$$ and $$ $1.x $$ and $$ $2.x $$
              $$/block$$
              after: $$x$$
           $$/block$$
         ", context);

         Assert.Contains("result: 3 and 2 and 1", r);
         Assert.Contains("after: 2", r);
      }

      [Fact]
      public void Case_sensitivity_can_be_controlled_in_blocks()
      {
         var p = new TemplateParser(new TemplateOptions { IgnoreCase = true }, new DefineDirective(), new BlockDirective());
         p.Replace("$$FooBAr$$", context);

         context = new TestContext(caseSensitive: true);
         context.Add("foobar", "qux");

         var error = Assert.Throws<SyntaxException>(() => p.Replace("$$FooBAr$$", context));
         Assert.Contains("could not resolve", error.Message);

         p.Replace("$$foobar$$", context);
      }
   }
}
