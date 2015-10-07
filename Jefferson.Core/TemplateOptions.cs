using System;

namespace Jefferson
{
   /// <summary>
   /// Represents various options for the template processor.
   /// </summary>
   public class TemplateOptions
   {
      /// <summary>
      /// If set all case is ignored for things like variable resolution, string comparison etc.
      /// </summary>
      public Boolean IgnoreCase = true;

      /// <summary>
      /// If set, emits tracing calls as part of the compiled parser.
      /// </summary>
      public Boolean EnableTracing = false;

      /// <summary>
      /// If set, uses the culture of the caller for things like ToString.
      /// </summary>
      public Boolean UseCurrentCulture = false;

      /// <summary>
      /// If set, undeclared variables resolve as if "null" was found.
      /// </summary>
      public Boolean AllowUnknownNames = false;
   }
}
