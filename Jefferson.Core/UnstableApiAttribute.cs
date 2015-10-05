using System;

namespace Jefferson
{
   /// <summary>
   /// If this attribute is used on a class, method or otherwise, it indicates that its API may change and is not stable.
   /// </summary>
   [AttributeUsage(AttributeTargets.All, Inherited = false, AllowMultiple = false)]
   public sealed class UnstableApiAttribute : Attribute
   {
      public readonly String Reason;

      public UnstableApiAttribute(String reason)
      {
         Reason = reason;
      }
   }
}
