using System;
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
using JetBrains.ReSharper.Psi.CSharp;
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

        private void ProcessTreeNode([NotNull] ITreeNode treeNode, ControlFlowElementData data)
        {
            if (treeNode is ILocalVariableDeclaration)
            {
                ProcessLocalVariableDeclaration(treeNode as ILocalVariableDeclaration, data);
            }
            else if (treeNode is IInvocationExpression)
            {
                ProcessInvocationExpression(treeNode as IInvocationExpression, data);
            }
        }

        private void ProcessLocalVariableDeclaration([NotNull] ILocalVariableDeclaration variableDeclaration,
            ControlFlowElementData data)
        {
            if (!DisposeUtil.IsWrappedInUsing(variableDeclaration) &&
                DisposeUtil.VariableTypeImplementsDisposable(variableDeclaration, _disposableInterface))
            {
                //RunAnalysis(variableDeclaration.DeclaredElement);
                data.Status[variableDeclaration] = VariableDisposeStatus.NotDisposed;
            }
        }

        private static void ProcessInvocationExpression([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data)
        {
            var variableDeclaration = GetQualifierVariableDeclaration(invocationExpression, data);
            if (variableDeclaration != null && IsSimpleDisposeInvocation(invocationExpression))
            {
                data.Status[variableDeclaration] = VariableDisposeStatus.Disposed;
                return;
            }
            ProcessSimpleInvocation(invocationExpression, variableDeclaration, data);
        }

        private static void ProcessSimpleInvocation([NotNull] IInvocationExpression invocationExpression,
            [CanBeNull] IVariableDeclaration qualifierVariableDeclaration, ControlFlowElementData data)
        {
            foreach (var argument in invocationExpression.InvocationExpressionReference.Invocation.Arguments)
            {
                var cSharpArgument = argument as ICSharpArgument;
                if (cSharpArgument == null)
                    continue;
                var invocation = cSharpArgument.Invocation;
                if (invocation == null)
                    continue;
                var reference = invocation.Reference as IReference;
                if (reference == null)
                    continue;
                var argumentVariableDeclaration = GetVariableDeclarationByReference(reference);
                if (argumentVariableDeclaration == null)
                    continue;
                if (data.Status[argumentVariableDeclaration] == null)
                    continue;
                var matchingArgument = GetMatchingArgument(argument);
            }
        }

        [CanBeNull]
        private static IVariableDeclaration GetQualifierVariableDeclaration([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data)
        {
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return null;
            var qualifierExpression = invokedExpression.QualifierExpression as IReferenceExpression;
            if (qualifierExpression == null)
                return null;
            var variableDeclaration = GetVariableDeclarationByReference(qualifierExpression.Reference);
            if (variableDeclaration == null)
                return null;
            return data.Status[variableDeclaration] != null ? variableDeclaration : null;
        }

        [CanBeNull]
        private static IVariableDeclaration GetVariableDeclarationByReference([NotNull] IReference reference)
        {
            var declaredElement = reference.Resolve().DeclaredElement;
            if (declaredElement == null)
                return null;
            var declaration = declaredElement.GetDeclarations().FirstOrDefault();
            if (declaration == null)
                return null;
            var variableDeclaration = declaration as IVariableDeclaration;
            return variableDeclaration;
        }

        private static IDeclaration GetMatchingArgument(ICSharpArgumentInfo argument)
        {
            var matchingParameter = argument.MatchingParameter;
            if (matchingParameter == null)
                return null;
            var declaredElement = matchingParameter.Element as IDeclaredElement;
            var declaration = declaredElement.GetDeclarations().FirstOrDefault();
            return declaration;
        }

        private static bool IsSimpleDisposeInvocation([NotNull] IInvocationExpression invocationExpression)
        {
            if (invocationExpression.Arguments.Count != 0)
                return false;
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return false;
            var name = invokedExpression.NameIdentifier.Name;
            if (!name.Equals("Dispose"))
                return false;
            return true;
        }

        private void HighlightParameters(ICSharpFunctionDeclaration element, ElementProblemAnalyzerData data)
        {
            var args = element.DeclaredElement.Parameters;

            foreach (var param in args)
            {
                //RunAnalysis(param);

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
            }
        }
    }
}