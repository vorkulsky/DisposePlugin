using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Services.Invoking
{
    public class TreeNodeHandler : ITreeNodeHandler
    {
        [NotNull]
        private readonly ITypeElement _disposableInterface;

        public TreeNodeHandler([NotNull] ITypeElement disposableInterface)
        {
            _disposableInterface = disposableInterface;
        }
        public void ProcessTreeNode(ITreeNode treeNode, ControlFlowElementData data)
        {
            throw new System.NotImplementedException();
        }
    }
}
