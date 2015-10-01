using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Jefferson.FileProcessing
{
   public interface IFileItem
   {
      Encoding Encoding { get; }
      String SourceFullPath { get; }
      String TargetFullPath { get; }
   }

   /// <summary>
   /// Represents a hierarchy of files to be processed. Note that these are still physical files.
   /// </summary>
   public interface IFileHierarchy
   {
      IEnumerable<IFileItem> Files { get; }
      IEnumerable<IFileHierarchy> Children { get; }
   }

   public class FileItem : IFileItem
   {
      private FileItem() { }

      private static String _GetTempTarget(String ignore)
      {
         return System.IO.Path.GetTempFileName();
      }

      public static FileItem FromPath(String path, Encoding encoding = null, Func<String, String> fileToProcessedFile = null)
      {
         if (!System.IO.File.Exists(path))
            throw new System.IO.FileNotFoundException("Could not find file " + path);
         return new FileItem
         {
            SourceFullPath = Path.GetFullPath(path),
            TargetFullPath = (fileToProcessedFile ?? _GetTempTarget)(path),
            Encoding = encoding ?? Encoding.UTF8
         };
      }

      public String SourceFullPath { get; private set; }
      public String TargetFullPath { get; private set; }
      public Encoding Encoding { get; private set; }
   }

   public class FileHierarchy : IFileHierarchy
   {
      private FileHierarchy() { }

      public static FileHierarchy FromDirectory(String directory, Encoding encoding = null, Func<String, Boolean> fileFilter = null, Func<String, String> fileToProcessedFile = null)
      {
         if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException("Could not find directory " + directory);

         if (fileFilter == null) fileFilter = s => true;

         return new FileHierarchy
         {
            Files = Directory.GetFiles(directory).Select(f => Path.Combine(directory, f)).Where(fileFilter).Select(f => FileItem.FromPath(f, encoding, fileToProcessedFile)).ToArray(),
            Children = Directory.GetDirectories(directory).Select(d => Path.Combine(directory, d)).Select(d => FileHierarchy.FromDirectory(d, encoding, fileFilter, fileToProcessedFile)).ToArray()
         };
      }

      public IEnumerable<IFileItem> Files { get; private set; }
      public IEnumerable<IFileHierarchy> Children { get; private set; }
   }
}
