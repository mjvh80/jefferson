

namespace Jefferson.FileProcessing
{
   public sealed class SimpleFileProcessor<TContext> : FileProcessor<SimpleFileProcessor<TContext>, TContext> where TContext : FileScopeContext<TContext, SimpleFileProcessor<TContext>>
   {
      public SimpleFileProcessor(TContext context) : base(context) { }

      public override SimpleFileProcessor<TContext> CreateChildScope()
      {
         return new SimpleFileProcessor<TContext>((TContext)mContext.CreateChildContext());
      }
   }
}
