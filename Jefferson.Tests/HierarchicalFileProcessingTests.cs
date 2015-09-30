using Jefferson.FileProcessing;
using System.IO;
using System.Linq;
using Xunit;

namespace Jefferson.Tests
{

   public class HierarchicalFileProcessingTests
   {
      public class TestContext : FileScopeContext<TestContext, SimpleFileProcessor<TestContext>> { }

      // todo
      [Fact]
      public void Can_process_hierarchical_items_correctly()
      {
         var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
         Directory.CreateDirectory(tempDir);

         var parent = Path.Combine(tempDir, "parent.txt");
         File.WriteAllText(parent, @"
         $$#define foo = 'foobar' /$$
         parent
         ");

         var kids = Path.Combine(tempDir, "kids");
         Directory.CreateDirectory(kids);

         var childA = Path.Combine(kids, "child_a.txt");
         var childB = Path.Combine(kids, "child_b.txt");

         File.WriteAllText(childA, @"
         $$foo$$
         $$#define foo = 'foo from a' /$$
         $$foo$$
         ");

         File.WriteAllText(childB, @"
         $$foo$$
         $$#define foo = 'foo from b' /$$
         $$foo$$
         ");

         var p = new SimpleFileProcessor<TestContext>(new TestContext());
         var h = FileHierarchy.FromDirectory(tempDir);
         p.ProcessFileHierarchy(h);

         Assert.Contains("parent", File.ReadAllText(h.Files.First().TargetFullPath));
         Assert.Contains("parent", File.ReadAllText(h.Children.First().Files.First().TargetFullPath));
         Assert.Contains("parent", File.ReadAllText(h.Children.Skip(1).First().Files.First().TargetFullPath));
      }
   }
}
