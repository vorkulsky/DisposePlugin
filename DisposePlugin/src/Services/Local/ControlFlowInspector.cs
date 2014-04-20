using System.Collections.Generic;
using DisposePlugin.CodeInspections.Highlighting;
using JetBrains.Annotations;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace DisposePlugin.Services.Local
{
    public class ControlFlowInspector : Services.ControlFlowInspector
    {
        private readonly List<HighlightingInfo> _highlightings = new List<HighlightingInfo>();
        [NotNull] private readonly ITypeElement _disposableInterface;
        private readonly int _maxLevel;
        private ControlFlowElementDataStorage _elementDataStorage;

        public ControlFlowInspector([NotNull] ICSharpFunctionDeclaration functionDeclaration,
            [NotNull] CSharpControlFlowGraf graf, int maxLevel, [NotNull] ITypeElement disposableInterface)
            : base(functionDeclaration, graf)
        {
            _disposableInterface = disposableInterface;
            _maxLevel = maxLevel;
        }

        public IEnumerable<HighlightingInfo> Inspect()
        {
            _elementDataStorage = new ControlFlowElementDataStorage();
            var initialData = new ControlFlowElementData(Graf.EntryElement.Id)
            {
                ThisStatus = VariableDisposeStatus.NotDisposed
            };
            _elementDataStorage[Graf.EntryElement] = initialData;
            var nodeHandlerFactory = new TreeNodeHandlerFactory(_maxLevel, _disposableInterface);
            DoStep(null, Graf.EntryElement, true, nodeHandlerFactory, _elementDataStorage);
            AddHighlightings();
            return _highlightings;
        }

        private void AddHighlightings()
        {
            var variables = new HashSet<IVariableDeclaration>();
            Graf.ReachableExits.ForEach(exit =>
            {
                var data = _elementDataStorage[exit.Source];
                if (data != null)
                {
                    data.Status.ForEach(kvp =>
                    {
                        if (kvp.Value == VariableDisposeStatus.NotDisposed || kvp.Value == VariableDisposeStatus.Both)
                            variables.Add(kvp.Key);
                    });
                }
            });
            variables.ForEach(
                variableDeclaration =>
                    _highlightings.Add(new HighlightingInfo(variableDeclaration.GetNameDocumentRange(),
                        new LocalVariableNotDisposed(
                            "Variable " + variableDeclaration.DeclaredName + "probably is not disposed"))));
        }
    }
}