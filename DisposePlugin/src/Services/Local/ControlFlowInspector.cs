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

        public ControlFlowInspector([NotNull] ICSharpFunctionDeclaration functionDeclaration,
            [NotNull] CSharpControlFlowGraf graf, int maxLevel, [NotNull] ITypeElement disposableInterface)
            : base(functionDeclaration, graf,
            new TreeNodeHandlerFactory(maxLevel, disposableInterface),
            new ControlFlowElementDataStorage())
        {
        }

        public List<HighlightingInfo> Inspect()
        {
            ElementDataStorage[Graf.EntryElement] = new ControlFlowElementData();
            DoStep(null, Graf.EntryElement, true);
            AddHighlightings();
            return _highlightings;
        }

        private void AddHighlightings()
        {
            var variables = new HashSet<IVariableDeclaration>();
            Graf.ReachableExits.ForEach(exit =>
            {
                var data = ElementDataStorage[exit.Source];
                if (data != null)
                {
                    data.Status.ForEach(kvp =>
                    {
                        if (kvp.Value == VariableDisposeStatus.NotDisposed)
                            variables.Add(kvp.Key);
                    });
                }
            });
            variables.ForEach(variableDeclaration =>
            {
                _highlightings.Add(new HighlightingInfo(variableDeclaration.GetNameDocumentRange(),
                    new LocalVariableNotDisposed(variableDeclaration.DeclaredName + " not disposed")));
            });
        }
    }
}
