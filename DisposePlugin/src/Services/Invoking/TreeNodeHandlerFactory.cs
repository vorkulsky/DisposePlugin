using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Services.Invoking
{
    public class TreeNodeHandlerFactory : ITreeNodeHandlerFactory
    {
        [NotNull]
        private readonly ITypeElement _disposableInterface;

        public TreeNodeHandlerFactory([NotNull] ITypeElement disposableInterface)
        {
            _disposableInterface = disposableInterface;

        }
        public ITreeNodeHandler GetNewTreeNodeHandler()
        {
            return new TreeNodeHandler(_disposableInterface);
        }
    }
}