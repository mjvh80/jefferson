using Jefferson.FileProcessing;
using System.IO;
using System.Linq;
using Xunit;

namespace Jefferson.Tests
{
   public class HierarchicalFileProcessingTests
   {
      public class TestContext : FileScopeContext<TestContext, SimpleFileProcessor<TestContext>> { }

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
         $$foo$$ AA
         ");

         File.WriteAllText(childB, @"
         $$foo$$
         $$#define foo = 'foo from b' /$$
         $$foo$$ BB
         ");

         var grandKids = Path.Combine(kids, "grand_kids");
         Directory.CreateDirectory(grandKids);

         var grandChildA = Path.Combine(grandKids, "grandkid_a.txt");

         File.WriteAllText(grandChildA, @"
         $$foo$$
         $$#define foo = 'foo from grand child' /$$
         $$foo$$ GC
         ");

         var p = new SimpleFileProcessor<TestContext>(new TestContext());
         var h = FileHierarchy.FromDirectory(tempDir);
         p.ProcessFileHierarchy(h);

         Assert.Contains("parent", File.ReadAllText(h.Files.First().TargetFullPath));

         var aContent = File.ReadAllText(h.Children.First().Files.First().TargetFullPath);
         Assert.Contains("foobar", aContent);
         Assert.Contains("foo from a", aContent);

         var bContent = File.ReadAllText(h.Children.First().Files.Skip(1).First().TargetFullPath);
         Assert.DoesNotContain("foobar", bContent);
         Assert.Contains("foo from a", bContent); // same scope level
         Assert.Contains("foo from b", bContent);

         var gcContent = File.ReadAllText(h.Children.First().Children.First().Files.First().TargetFullPath);
         Assert.Contains("foo from b", gcContent); // inherited from b which was last to execute in parent scope
         Assert.Contains("foo from grand child", gcContent);
      }

      [Fact]
      public void Cannot_create_file_item_from_non_existing_file()
      {
         var nonExistingFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
         Assert.False(File.Exists(nonExistingFile));
         Assert.Throws<FileNotFoundException>(() => FileItem.FromFile(nonExistingFile));
      }

      public void Cannot_create_hierarchy_from_non_existing_directory()
      {
         var nonExistingFile = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
         Assert.False(Directory.Exists(nonExistingFile));
         Assert.Throws<DirectoryNotFoundException>(() => FileHierarchy.FromDirectory(nonExistingFile));
      }
   }
}
