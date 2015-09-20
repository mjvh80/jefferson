﻿using Jefferson.Directives;
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Extensions;

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
      public void Undef_directive_within_let_directive_does_not_work_if_same_name()
      {
         try
         {
            var result = new TemplateParser(new LetDirective(), new UndefDirective()).Replace(@"
            $$#let foo = 1$$
               $$#let bar = 2$$
                  $$#undef foo /$$
               $$/let$$

               $$foo$$
            $$/let$$
         ", context);

            Assert.False(true, "expected an error");
         }
         catch (Exception e)
         {
            Assert.Equal("Cannot unset variable 'foo' because it has been bound in a let context.", e.Message.Trim());
         }
      }

      [Theory]
      [InlineData("$$#undef/$$"), InlineData("$$#undef /$$")]
      public void Undef_withouth_arguments_fails(String source)
      {
         var error = Assert.Throws<SyntaxException>(() =>
         new TemplateParser(new UndefDirective()).Replace(source, context));
         Trace.WriteLine(error.Message);
      }

      [Fact]
      public void Undef_allows_trailing_semicolon()
      {
         // Will just allow this, don't really see the point to be too strict.
         var result = new TemplateParser(new DefineDirective(), new UndefDirective()).Replace(@"
             $$#define foo = 'bar' /$$
             $$#undef ;;;foo;; /$$
         ", context);
      }

      [Fact]
      public void Cannot_undef_twice_by_default()
      {
         // Will just allow this, don't really see the point to be too strict.
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new DefineDirective(), new UndefDirective()).Replace(@"
             $$#define foo = 'bar' /$$
             $$#undef foo;foo /$$
             $$#undef bar /$$
         ", context));

         Assert.Contains("Failed to unbind variable 'foo'", error.Message);
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
      public void Cannot_declare_define_with_more_than_5_params()
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new DefineDirective()).Replace(@"
         $$#define a(b,c,d,e,f,g,h)$$
         $$/define$$
         ", context));

         // Trace.WriteLine(error.Message);
      }

      [Fact]
      public void Define_with_parameters_must_have_body()
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new DefineDirective()).Replace(@"
         $$#define a(b,c,d)/$$
         ", context));
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

      [Fact]
      public void Define_can_accept_arguments()
      {
         var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#define foobar(x)$$
               Hello $$x$$
            $$/define$$

            $$foobar('Marcus')$$
         ", context);

         Assert.Equal("Hello Marcus", result.Trim());
         Trace.WriteLine(result);

         result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#define blah(x, y)$$
               Blah = $$x$$ and $$y$$!
            $$/define$$

            $$blah('xxx', 'yyy')$$
         ", context);

         Assert.Equal("Blah = xxx and yyy!", result.Trim());
         Trace.WriteLine(result);
      }

      [Fact]
      public void Can_access_let_binding_within_define()
      {
         var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#let foo = 'foobar'$$
               $$#define blah(x, y)$$
                  Blah = $$x$$ and $$y$$ and $$foo$$.
               $$/define$$

               $$blah('xxx', 'yyy')$$
            $$/let$$
         ", context);

         Assert.Equal("Blah = xxx and yyy and foobar.", result.Trim());
      }

      [Fact]
      public void Cannot_redefine_a_let_binding()
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#let foo = 'foobar'$$
               $$#define foo(x, y)$$
                  Blah = $$x$$ and $$y$$ and $$foo$$.
               $$/define$$

               $$blah('xxx', 'yyy')$$
            $$/let$$
         ", context));

         Assert.Contains("Cannot set variable 'foo' because it has been bound in a let context.", error.Message.Trim());
      }

      [Fact]
      public void Can_access_defined_variable_outside_let_scope()
      {
         var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
            $$#let foo = 'foobar'$$
               $$#define bar = 'bar' /$$
            $$/let$$
            bar = $$bar$$
         ", context);

         Assert.Equal("bar = bar", result.Trim());
      }
   }
}