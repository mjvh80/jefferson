using Jefferson.Directives;
using Xunit;

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
   }
}
