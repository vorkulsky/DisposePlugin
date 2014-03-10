using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using DisposePlugin.CodeInspections.Highlighting;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
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

        private readonly Dictionary<string, IVariableDeclaration> _variableDeclarations =
            new Dictionary<string, IVariableDeclaration>();

        private readonly ControlFlowElementDataStorage _elementDataStorage = new ControlFlowElementDataStorage();

        #endregion

        #region Attributes

        public List<HighlightingInfo> Highlightings
        {
            get { return _myHighlightings; }
        }

        #endregion

        public ControlFlowInspector([NotNull] CSharpControlFlowGraf graf, [NotNull] ITypeElement disposableInterface)
        {
            _graf = graf;
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
                        ProcessTreeNode(node, currentData);
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

        public static void Analyze([NotNull] IControlFlowElement source, [NotNull] IControlFlowElement target,
            [NotNull] ControlFlowElementData targetData)
        {
            var sourceElement1 = source.SourceElement;
            if (sourceElement1 == null)
                return;
            var sourceElement2 = target.SourceElement;
            if (sourceElement2 == null)
                return;
            for (var containingNode = sourceElement1.GetContainingNode<ILocalScope>(true);
                containingNode != null && !containingNode.Contains(sourceElement2);
                containingNode = containingNode.GetContainingNode<ILocalScope>(false))
            {
                var v = containingNode.LocalVariables;
            }

        }

        private void ProcessTreeNode([NotNull] ITreeNode treeNode, ControlFlowElementData data)
        {
            if (treeNode is ILocalVariableDeclaration)
            {
                ProcessLocalVariableDeclaration(treeNode as ILocalVariableDeclaration, data);
            }
        }

        private void ProcessLocalVariableDeclaration([NotNull] ILocalVariableDeclaration variableDeclaration,
            ControlFlowElementData data)
        {
            if (!DisposeUtil.IsWrappedInUsing(variableDeclaration) &&
                DisposeUtil.VariableTypeImplementsDisposable(variableDeclaration, _disposableInterface))
            {
                RunAnalysis(variableDeclaration.DeclaredElement);
                data.Status[variableDeclaration] = VariableDisposeStatus.NotDisposed;
                _variableDeclarations[variableDeclaration.DeclaredName] = variableDeclaration;
            }
        }

        private void HighlightParameters(ICSharpFunctionDeclaration element, ElementProblemAnalyzerData data)
        {
            var args = element.DeclaredElement.Parameters;

            foreach (var param in args)
            {
                RunAnalysis(param);

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
        }

        private void RunAnalysis(ITypeOwner myVariable)
        {
            IReference[] allReferences = myVariable.GetPsiServices().Finder.FindAllReferences(myVariable);
            IInitializerOwnerDeclaration ownerDeclaration =
                myVariable.GetDeclarations().OfType<IInitializerOwnerDeclaration>().FirstOrDefault();
            ITreeNode InitializerElement;
            if (ownerDeclaration != null)
                InitializerElement = ownerDeclaration.Initializer;
            var usages = allReferences.Select(reference => reference.GetTreeNode()).ToList();
            foreach (var re in usages)
            {
                _myHighlightings.Add(new HighlightingInfo(re.GetNavigationRange(), new LocalVariableNotDisposed()));
                MatchingParameters(re);
            }
        }

        private void MatchingParameters(ITreeNode re)
        {
            var qq = re.Parent;
            if (qq == null)
                return;
            var q = qq.Parent as IInvocationExpression;
            if (q == null)
                return;
            var z = q.InvocationExpressionReference;
            var rr = z.CurrentResolveResult;
            if (rr == null)
                return;
            var de = rr.DeclaredElement;
            var e = z.Invocation.Arguments;
            foreach (var j in e)
            {
                var mp = j.MatchingParameter;
                if (mp == null)
                    continue;
                var el = mp.Element;
                var dee = el as IDeclaredElement;
                var decl = dee.GetDeclarations().First();
                _myHighlightings.Add(new HighlightingInfo(decl.GetNameDocumentRange(), new LocalVariableNotDisposed()));
            }
        }
    }
}