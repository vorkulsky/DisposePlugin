using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Services.Local
{
    public class TreeNodeHandlerFactory : ITreeNodeHandlerFactory
    {
        private readonly int _maxLevel;
        [NotNull]
        private readonly ITypeElement _disposableInterface;

        public TreeNodeHandlerFactory(int maxLevel, [NotNull] ITypeElement disposableInterface)
        {
            _maxLevel = maxLevel;
            _disposableInterface = disposableInterface;

        }

        public ITreeNodeHandler GetNewTreeNodeHandler()
        {
            return new TreeNodeHandler(_maxLevel, _disposableInterface);
        }
    }
}
