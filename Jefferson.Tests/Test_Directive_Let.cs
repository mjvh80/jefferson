using Jefferson.Directives;
using System;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   class Test_Directive_Let
   {
      [Fact]
      public void Can_use_let_within_define_directive()
      {
         var result = new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
         $$#define foo$$
            $$#let foo = 'bar'$$
               $$foo$$
            $$/let$$
         $$/define$$
         $$foo$$
         ", new TestContext());

         Assert.Equal("bar", result.Trim());
      }

      [Fact]
      public void Cannot_redefine_let_bound_variables()
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
         $$#let a = 'a'$$
            $$#let b = 'b'$$
               $$#define a = 'not a!' /$$
            $$/let$$
         $$/let$$
         ", new TestContext()));

         Assert.Equal("Cannot set variable 'a' because it has been bound in a let context.", error.Message.Trim());
      }

      [Theory]
      // [InlineData("$$#let a='foo';$$ $$/let$$")] // todo this throws because empty, make consistent with #define which allows empties...
      public void Let_good_syntax_facts(String input)
      {
         var result = new TemplateParser(new LetDirective()).Replace(input, new TestContext());
      }

      [Theory]
      [InlineData("$$#let a= 'book'/$$"), InlineData("$$#let a = 'boo'//$$")]
      [InlineData("$$#let$$$$/let$$"), InlineData("$$#let a=$$ $$/let$$")]
      [InlineData("$$#let a='b'$$")][InlineData("$$#let 1a='b'$$$$/let$$")]
      public void Let_bad_syntax_facts(String input)
      {
         Assert.Throws<SyntaxException>(() => new TemplateParser(new LetDirective()).Replace(input, new TestContext()));
      }
   }
}
