using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Services
{
    public interface ITreeNodeHandler
    {
        void ProcessTreeNode(ITreeNode treeNode, ControlFlowElementData data);
    }
}