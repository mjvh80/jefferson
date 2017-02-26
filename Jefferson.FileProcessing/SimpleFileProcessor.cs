

using Jefferson.Directives;

namespace Jefferson.FileProcessing
{
    public sealed class SimpleFileProcessor<TContext> : FileProcessor<SimpleFileProcessor<TContext>, TContext> where TContext : FileScopeContext<TContext, SimpleFileProcessor<TContext>>
    {
        private readonly IDirective[] _mMainDirectives;

        public SimpleFileProcessor(TContext context, IDirective[] directives = null) : base(context, directives)
        {
            _mMainDirectives = directives;
        }

        public override SimpleFileProcessor<TContext> CreateChildScope()
        {
            return new SimpleFileProcessor<TContext>((TContext)mContext.CreateChildContext(), _mMainDirectives);
        }
    }
}
