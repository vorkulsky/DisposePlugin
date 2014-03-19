using System.Collections.Generic;
using System.Diagnostics;
using DisposePlugin.CodeInspections.Highlighting;
using DisposePlugin.src.CodeInspections;
using JetBrains.Annotations;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

namespace DisposePlugin.CodeInspections
{
    public class ControlFlowInspector
    {
        #region Data

        [NotNull] private readonly CSharpControlFlowGraf _graf;
        [NotNull] private readonly ITypeElement _disposableInterface;

        private readonly List<HighlightingInfo> _myHighlightings = new List<HighlightingInfo>();

        private readonly ControlFlowElementDataStorage _elementDataStorage = new ControlFlowElementDataStorage();

        private readonly int _maxLevel;

        #endregion

        #region Attributes

        public List<HighlightingInfo> Highlightings
        {
            get { return _myHighlightings; }
        }

        #endregion

        public ControlFlowInspector([NotNull] CSharpControlFlowGraf graf, int maxLevel, [NotNull] ITypeElement disposableInterface)
        {
            _graf = graf;
            _maxLevel = maxLevel;
            _disposableInterface = disposableInterface;
            Inspect();
        }

        private void Inspect()
        {
            _elementDataStorage[_graf.EntryElement] = new ControlFlowElementData();
            DoStep(null, _graf.EntryElement, true);
            AddHighlightings();
        }

        private void AddHighlightings()
        {
            var variables = new HashSet<IVariableDeclaration>();
            _graf.ReachableExits.ForEach(exit =>
            {
                var data = _elementDataStorage[exit.Source];
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
                _myHighlightings.Add(new HighlightingInfo(variableDeclaration.GetNameDocumentRange(),
                    new LocalVariableNotDisposed(variableDeclaration.DeclaredName + " not disposed")));
            });
        }

        private void DoStep([CanBeNull] IControlFlowElement previous, [NotNull] IControlFlowElement current, bool visitNew)
        {
            if (!current.IsReachable)
                return;

            var changesAre = false;
            if (previous != null) 
               changesAre = _elementDataStorage.Merge(previous, current);

            var newVisited = false;
            if (visitNew)
            {
                var currentData = _elementDataStorage[current];
                Debug.Assert(currentData != null, "currentData != null");
                if (!currentData.IsVisited())
                {
                    currentData.Visit();
                    var node = current.SourceElement;
                    if (node != null)
                    {
                        var handler = new TreeNodeHandler(_maxLevel, _disposableInterface);
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

/*        private void HighlightParameters(ICSharpFunctionDeclaration element, ElementProblemAnalyzerData data)
        {
            var args = element.DeclaredElement.Parameters;

            foreach (var param in args)
            {
                var t = param.Type;
                var st = t.GetScalarType();
                if (st == null)
                    continue;
                var dt = st.Resolve().DeclaredElement;

                if (dt == null)
                    continue;
                if (DisposeUtil.HasDisposable(dt, _disposableInterface))
                {
                    var dd = param.GetDeclarationsIn(data.Process.SourceFile);
                    var d = dd.First();
                    _myHighlightings.Add(new HighlightingInfo(d.GetNameDocumentRange(), new LocalVariableNotDisposed()));
                }
            }
        }*/
    }
}