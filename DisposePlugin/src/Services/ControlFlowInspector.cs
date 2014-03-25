using System.Diagnostics;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace DisposePlugin.Services
{
    public abstract class ControlFlowInspector
    {
        #region Data

        [NotNull]
        protected readonly CSharpControlFlowGraf Graf;
        [NotNull]
        protected readonly ICSharpFunctionDeclaration FunctionDeclaration;
        [NotNull]
        protected readonly ITypeElement DisposableInterface;

        protected readonly ControlFlowElementDataStorage ElementDataStorage;
        protected readonly ITreeNodeHandlerFactory NodeHandlerFactory;

        #endregion

        protected ControlFlowInspector([NotNull] ICSharpFunctionDeclaration functionDeclaration,
            [NotNull] CSharpControlFlowGraf graf, [NotNull] ITypeElement disposableInterface,
            ITreeNodeHandlerFactory nodeHandlerFactory, ControlFlowElementDataStorage elementDataStorage)
        {
            FunctionDeclaration = functionDeclaration;
            Graf = graf;
            DisposableInterface = disposableInterface;
            NodeHandlerFactory = nodeHandlerFactory;
            ElementDataStorage = elementDataStorage;
        }

        protected void DoStep([CanBeNull] IControlFlowElement previous, [NotNull] IControlFlowElement current, bool visitNew)
        {
            if (!current.IsReachable)
                return;

            var changesAre = false;
            if (previous != null) 
               changesAre = ElementDataStorage.Merge(previous, current);

            var newVisited = false;
            if (visitNew)
            {
                var currentData = ElementDataStorage[current];
                Debug.Assert(currentData != null, "currentData != null");
                if (!currentData.IsVisited())
                {
                    currentData.Visit();
                    var node = current.SourceElement;
                    if (node != null)
                    {
                        var handler = NodeHandlerFactory.GetNewTreeNodeHandler(DisposableInterface);
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
                DoStep(current, target, newVisited);
            }
        }
    }
}