using Jefferson.Directives;
using System;
using Xunit;

namespace Jefferson.Tests
{
   class Test_Directive_Define
   {
      private TestContext context;

      public Test_Directive_Define()
      {
         context = new TestContext();
      }

      [Fact]
      public void Define_directive_works()
      {
         var result = new TemplateParser(new DefineDirective()).Replace(@"

            $$#define foo = 'bar'/$$

            $$foo$$
         ", context);

         Assert.Equal("bar", result.Trim());
      }

      [Fact]
      public void Define_directive_in_let_is_ok_if_different_names()
      {
         var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
         $$#let x = 1$$
            $$#define foo = 'bar' /$$

            $$foo$$
         $$/let$$
         ", context);

         Assert.Equal("bar", result.Trim());
      }

      [Fact]
      public void Define_directive_within_let_directive_does_not_work_if_same_name()
      {
         try
         {
            var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#let foo = 1$$
               $$#define foo = 'bar'/$$

               $$foo$$
            $$/let$$
         ", context);

            Assert.False(true, "expected an error");
         }
         catch (Exception e)
         {
            Assert.Equal("Cannot set variable 'foo' because it has been bound in a let context.", e.Message.Trim());
         }
      }

      [Fact]
      public void Define_directive_within_let_directive_does_not_work_if_same_name_2()
      {
         try
         {
            var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#let foo = 1$$
               $$#let bar = 2$$
                  $$#define foo = 'bar'/$$
               $$/let$$

               $$foo$$
            $$/let$$
         ", context);

            Assert.False(true, "expected an error");
         }
         catch (Exception e)
         {
            Assert.Equal("Cannot set variable 'foo' because it has been bound in a let context.", e.Message.Trim());
         }
      }

      [Fact]
      public void Can_undef_variables()
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new LetDirective(), new UndefDirective(), new DefineDirective()).Replace(@"
         $$#let x = 1$$
            $$#define foo = 'bar'/$$
      
            $$foo$$ - $$#undef foo /$$ - $$foo$$.
         $$/let$$
         ", context));

         Assert.Contains("Expected known name, could not resolve 'foo'", error.Message);
         // Assert.Equal("bar -  -.", result.Trim());
      }

      [Fact]
      public void Can_define_snippets()
      {
         // todo: in general whitespace is not an issue for us because we don't [want to] care in our xml.
         // However, it'd be nice to control this more. I do *not* like handlebar's solution however.
         var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#define foobar$$
               Hello!
            $$/define$$

            $$Trim(foobar)$$-$$Trim(foobar)$$
         ", context);

         Assert.Equal("Hello!-Hello!", result.Trim());
      }
   }
}
