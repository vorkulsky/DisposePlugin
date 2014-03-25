using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Services.Local
{
    public class TreeNodeHandlerFactory : ITreeNodeHandlerFactory
    {
        public int MaxLevel;

        public TreeNodeHandlerFactory(int maxLevel)
        {
            MaxLevel = maxLevel;
        }

        public ITreeNodeHandler GetNewTreeNodeHandler([NotNull] ITypeElement disposableInterface)
        {
            return new TreeNodeHandler(MaxLevel, disposableInterface);
        }
    }
}
