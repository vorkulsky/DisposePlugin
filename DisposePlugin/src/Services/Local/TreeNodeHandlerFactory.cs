using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Services.Local
{
    public class TreeNodeHandlerFactory : ITreeNodeHandlerFactory
    {
        public int MaxLevel;
        [NotNull]
        private readonly ITypeElement _disposableInterface;

        public TreeNodeHandlerFactory(int maxLevel, [NotNull] ITypeElement disposableInterface)
        {
            MaxLevel = maxLevel;
            _disposableInterface = disposableInterface;

        }

        public ITreeNodeHandler GetNewTreeNodeHandler()
        {
            return new TreeNodeHandler(MaxLevel, _disposableInterface);
        }
    }
}
