using System.Collections.Generic;
using DisposePlugin.Services.Local;
using DisposePlugin.Util;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ControlFlow.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.CodeInspections
{
    public class DisposableFunctionElementProcessor : IRecursiveElementProcessor
    {
        private readonly List<HighlightingInfo> _highlightings = new List<HighlightingInfo>();
        private readonly IDaemonProcess _process;
        private readonly int _maxLevel;

        public DisposableFunctionElementProcessor(IDaemonProcess process, int maxLevel)
        {
            _process = process;
            _maxLevel = maxLevel;
        }

        public List<HighlightingInfo> Highlightings
        {
            get { return _highlightings; }
        }

        public bool InteriorShouldBeProcessed(ITreeNode element)
        {
            return !(element is ICSharpFunctionDeclaration);
        }

        public void ProcessBeforeInterior(ITreeNode element)
        {
            var functionDeclaration = element as ICSharpFunctionDeclaration;
            if (functionDeclaration == null)
                return;

            var psiModule = _process.PsiModule;
            var resolveContext = _process.SourceFile.ResolveContext;
            var disposableInterface = DisposeUtil.GetDisposableInterface(psiModule, resolveContext);
            if (disposableInterface == null)
                return;

            var graf = CSharpControlFlowBuilder.Build(functionDeclaration) as CSharpControlFlowGraf;
            if (graf == null)
                return;

            /*            var flowGrafInspector = new CSharpControlFlowGrafInspector(graf, ValueAnalysisMode.OPTIMISTIC);
                        flowGrafInspector.Inspect();
                        var au = flowGrafInspector.AssignmentsUsage;*/
            //FindInfoByExpression
            //FindVariableInfo

            var grafInspector = new ControlFlowInspector(functionDeclaration, graf, _maxLevel, disposableInterface);
            var highlightings = grafInspector.Inspect();
            _highlightings.AddRange(highlightings);
        }

        public void ProcessAfterInterior(ITreeNode element)
        {
        }

        public bool ProcessingIsFinished
        {
            get { return _process.InterruptFlag; }
        }
    }
}