using System;

namespace Jefferson
{
   /// <summary>
   /// Represents various options for the template processor.
   /// </summary>
   public class TemplateOptions
   {
      public TemplateOptions Clone() { return (TemplateOptions)this.MemberwiseClone(); }

      public Boolean IgnoreCase = true;

      public Boolean EnableTracing = false;

      public Boolean UseCurrentCulture = false;
   }
}
