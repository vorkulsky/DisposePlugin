using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Services.Invoking
{
    public class TreeNodeHandlerFactory : ITreeNodeHandlerFactory
    {
        public ITreeNodeHandler GetNewTreeNodeHandler([NotNull] ITypeElement disposableInterface)
        {
            return new TreeNodeHandler(disposableInterface);
        }
    }
}