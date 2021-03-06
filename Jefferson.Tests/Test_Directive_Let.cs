﻿using Jefferson.Directives;
using System;
using System.Diagnostics;
using Xunit;
using Xunit.Extensions;

namespace Jefferson.Tests
{
   public class Test_Directive_Let
   {
      TemplateParser replacer;
      TestContext context;

      public Test_Directive_Let()
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
      public void Let_directives_work()
      {
         var p = new TemplateParser(new LetDirective());
         var result = p.Replace(
@"Before: $$b1$$
$$#let b1 = 'foo'$$
  Now I have $$b1$$.
$$/let$$
And after: $$b1$$.
", context);

         result = p.Replace(
@"$$#let foobar$$
  blah blah
$$#out$$
$$foobar$$ and $$foobar$$
$$/let$$
", context);

         // Yes, the whitespace is not pretty.
         Assert.Equal(
@"blah blah
 and 
  blah blah", result.Trim());

         Trace.WriteLine(result);
      }

      [Fact]
      public void Can_access_variables_outside_of_let_bindings()
      {
         // This blew due to bug in which case the scope in which let sits was not accessed, i.e. b1 would not be known.
         new TemplateParser(new LetDirective()).Replace(@"
            $$#let x = 'y'$$
               $$b1$$
            $$/let$$", context);
      }

      [Fact]
      public void Cannot_set_let_parameter()
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new LetDirective(), new DefineDirective()).Replace(@"
         $$#let a(b)$$
            $$#define b = 'blah' /$$
         $$#out$$
            foobar
         $$/let$$
         ", context));
         Assert.Contains("Cannot set variable 'b' because it is the name of a let parameter.", error.Message);
      }

      [Fact]
      public void Can_bind_variable_from_context()
      {
         // FieldOnCtx
         var result = new TemplateParser(new LetDirective()).Replace(@"
            $$FieldOnCtx$$
            $$#let FieldOnCtx = 'blah'$$
               $$FieldOnCtx$$
            $$/let$$", context);

         Assert.Contains("blah", result);
         Assert.Contains("fldOnCtx", result);
      }

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

         Assert.Contains("Cannot set variable 'a' because it has been bound in a let context.", error.Message.Trim());
      }

      [Theory(Skip = "todo")]
      // [InlineData("$$#let a='foo';$$ $$/let$$")] // todo this throws because empty, make consistent with #define which allows empties...
      public void Let_good_syntax_facts(String input)
      {
         var result = new TemplateParser(new LetDirective()).Replace(input, new TestContext());
      }

      [Theory]
      [InlineData("$$#let a= 'book'/$$", "Missing body")]
      [InlineData("$$#let a = 'boo'//$$", "Missing body")]
      [InlineData("$$#let   $$   $$/let$$", "Expected a name to bind to something")]
      [InlineData("$$#let$$$$/let$$", "Expected a name to bind to something")]
      [InlineData("$$#let$$ $$/let$$", "Expected a name to bind to something")]
      [InlineData("$$#let $$$$/let$$", "Expected a name to bind to something")]
      [InlineData("$$#let a='b'$$", "Failed to find directive end")]
      [InlineData("$$#let 1a='b'$$$$/let$$", "Variable '1a' has an invalid name")]
      [InlineData("$$#let b=1; a$$ $$/let$$", "Expected end of directive argument input")] // todo this error is crap
      [InlineData("$$#let a$$ $$/let$$", "Missing $$#out$$")]
      public void Let_bad_syntax_facts(String input, String errorPart)
      {
         var error = Assert.Throws<SyntaxException>(() => new TemplateParser(new LetDirective()).Replace(input, new TestContext()));
         Assert.Contains(errorPart, error.Message);
      }

      [Theory]
      [InlineData("$$#let a=$$ $$/let$$")] // OK since we allowed empty expressions as empty string
      public void Let_syntax_edge_case(String edgeCase)
      {
         new TemplateParser(new LetDirective()).Replace(edgeCase, new TestContext());
      }

      [Fact]
      public void Let_bound_names_can_be_case_managed()
      {
         // By default case sensitive.

         var result = replacer.Replace("$$#let a = 'hi'$$ $$A$$ $$/let$$", context);
         Assert.Equal("hi", result.Trim());

         var p = new TemplateParser(new TemplateOptions { IgnoreCase = false }, new LetDirective());
         var error = Assert.Throws<SyntaxException>(() => p.Replace("$$#let a = 'hi'$$ $$A$$ $$/let$$", context));

         Assert.Contains("could not resolve", error.Message);

         result = p.Replace("$$#let a = 'hi'$$ $$a$$ $$/let$$", context);
         Assert.Equal("hi", result.Trim());
      }
   }
}
