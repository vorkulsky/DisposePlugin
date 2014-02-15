using System.Linq;
using DisposePlugin.CodeInspections.Highlighting;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.ControlFlow.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.src.CodeInspections
{
    [ElementProblemAnalyzer(new[] { typeof(ICSharpFunctionDeclaration) },
        HighlightingTypes = new[] { typeof (LocalVariableNotDisposed)})]
    internal class DisposableFunctionProblemAnalyzer : ElementProblemAnalyzer<ICSharpFunctionDeclaration>
    {
        [NotNull] private ITypeElement _disposableInterface;
        [NotNull] private CSharpControlFlowGraf _myControlFlowGraf;
        [NotNull] private IHighlightingConsumer _consumer;

        protected override void Run(ICSharpFunctionDeclaration element, ElementProblemAnalyzerData data,
            IHighlightingConsumer consumer)
        {
            _consumer = consumer;
            var psiModule = data.Process.PsiModule;
            var resolveContext = data.Process.SourceFile.ResolveContext;
            var disposableInterface = DisposeUtil.GetDisposableInterface(psiModule, resolveContext);
            if (disposableInterface != null) _disposableInterface = disposableInterface;
            else return;

            HighlightParameters(element, data);

            var myControlFlowGraf = CSharpControlFlowBuilder.Build(element) as CSharpControlFlowGraf;
            if (myControlFlowGraf != null) _myControlFlowGraf = myControlFlowGraf;
            else return;

            var ee = _myControlFlowGraf.EntryElement;
            if (ee.IsReachable)
            {
                Go(ee);
            }
        }
        private void Go(IControlFlowElement cfe)
        {
            foreach (IControlFlowRib rib in cfe.Entries)
            {
                ITreeNode tn = rib.GetSourceElement();
                var element = tn as ILocalVariableDeclaration;
                if (element != null)
                {
                    if (!DisposeUtil.IsWrappedInUsing(element) &&
                        DisposeUtil.VariableTypeImplementsDisposable(element, _disposableInterface))
                    {
                        RunAnalysis(element.DeclaredElement);
                        _consumer.AddHighlighting(new LocalVariableNotDisposed(), element.GetNameDocumentRange(),
                            element.GetContainingFile());
                    }
                }
            }
            foreach (IControlFlowRib rib in cfe.Exits)
            {
                var t = rib.Target;
                if (t != null)
                {
                    Go(t);
                }
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
                    _consumer.AddHighlighting(new LocalVariableNotDisposed(), d.GetNameDocumentRange(),
                        element.GetContainingFile());
                }
            }
        }

        private void RunAnalysis(ITypeOwner myVariable)
        {
            IReference[] allReferences = myVariable.GetPsiServices().Finder.FindAllReferences(myVariable);
            IInitializerOwnerDeclaration ownerDeclaration = myVariable.GetDeclarations().OfType<IInitializerOwnerDeclaration>().FirstOrDefault();
            ITreeNode InitializerElement;
            if (ownerDeclaration != null)
                InitializerElement = ownerDeclaration.Initializer;
            var usages = allReferences.Select(reference => reference.GetTreeNode()).ToList();
            foreach (var re in usages)
            {
                var n = re.GetNavigationRange();
                _consumer.AddHighlighting(new LocalVariableNotDisposed(), n, re.GetContainingFile());
            }
        }
    }
}
