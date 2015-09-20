using Jefferson.Directives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Jefferson.Tests
{
   class Test_Directive_Block
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
   }
}
