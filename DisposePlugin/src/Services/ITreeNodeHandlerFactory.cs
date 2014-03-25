using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Services
{
    public interface ITreeNodeHandlerFactory
    {
        ITreeNodeHandler GetNewTreeNodeHandler(ITypeElement disposableInterface);
    }
}