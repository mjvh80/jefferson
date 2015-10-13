using System;

namespace Jefferson.FileProcessing
{
   [Serializable]
   public sealed class DontProcessException : Exception
   {
      internal DontProcessException() { }
   }
}
