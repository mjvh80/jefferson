using System;

namespace Jefferson
{
   [Serializable]
   internal class StopProcessingException : Exception
   {
      public StopProcessingException() : base("Internel exception to stop processing a file.") { }
   }
}
