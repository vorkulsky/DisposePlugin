using JetBrains.Annotations;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Util;

namespace DisposePlugin.Services
{
    public abstract class ControlFlowInspector
    {
        #region Data

        [NotNull] protected readonly CSharpControlFlowGraf Graf;
        [NotNull] protected readonly ICSharpFunctionDeclaration FunctionDeclaration;

        #endregion

        protected ControlFlowInspector([NotNull] ICSharpFunctionDeclaration functionDeclaration,
            [NotNull] CSharpControlFlowGraf graf)
        {
            FunctionDeclaration = functionDeclaration;
            Graf = graf;
        }

        protected void DoStep([CanBeNull] IControlFlowElement previous, [NotNull] IControlFlowElement current,
            bool visitNew,
            ITreeNodeHandlerFactory nodeHandlerFactory, ControlFlowElementDataStorage elementDataStorage)
        {
            if (!current.IsReachable)
                return;

            var changesAre = false;
            if (previous != null)
                changesAre = elementDataStorage.Merge(previous, current);

            var newVisited = false;
            if (visitNew)
            {
                var currentData = elementDataStorage[current];
                Assertion.Assert(currentData != null, "currentData != null");
                if (!currentData.IsVisited())
                {
                    currentData.Visit();
                    var node = current.SourceElement;
                    if (node != null)
                    {
                        var handler = nodeHandlerFactory.GetNewTreeNodeHandler();
                        handler.ProcessTreeNode(node, currentData);
                    }
                    newVisited = true;
                }
            }
            if (!newVisited && !changesAre)
                return;
            foreach (var rib in current.Exits)
            {
                var target = rib.Target;
                if (target == null)
                    continue;
                DoStep(current, target, newVisited, nodeHandlerFactory, elementDataStorage);
            }
        }
    }
}