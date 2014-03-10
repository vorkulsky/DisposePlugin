using DisposePlugin.CodeInspections.Highlighting;
using DisposePlugin.Util;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.ControlFlow.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace DisposePlugin.CodeInspections
{
    [ElementProblemAnalyzer(new[] {typeof (ICSharpFunctionDeclaration)},
        HighlightingTypes = new[] {typeof (LocalVariableNotDisposed)})]
    public class DisposableFunctionProblemAnalyzer : ElementProblemAnalyzer<ICSharpFunctionDeclaration>
    {
        protected override void Run(ICSharpFunctionDeclaration element, ElementProblemAnalyzerData data,
            IHighlightingConsumer consumer)
        {
            var psiModule = data.Process.PsiModule;
            var resolveContext = data.Process.SourceFile.ResolveContext;
            var disposableInterface = DisposeUtil.GetDisposableInterface(psiModule, resolveContext);
            if (disposableInterface == null)
                return;

            var graf = CSharpControlFlowBuilder.Build(element) as CSharpControlFlowGraf;
            if (graf == null)
                return;

/*            var flowGrafInspector = new CSharpControlFlowGrafInspector(graf, ValueAnalysisMode.OPTIMISTIC);
            flowGrafInspector.Inspect();
            var au = flowGrafInspector.AssignmentsUsage;*/
            //FindInfoByExpression
            //FindVariableInfo

            var grafInspector = new ControlFlowInspector(graf, disposableInterface);
            grafInspector.Highlightings.ForEach(consumer.ConsumeHighlighting);
        }
    }
}   