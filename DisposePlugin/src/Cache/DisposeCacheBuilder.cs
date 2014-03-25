using System.Collections.Generic;
using DisposePlugin.Services.Invoking;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ControlFlow.CSharp;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Cache
{
    public class DisposeCacheBuilder : IRecursiveElementProcessor
    {
        private readonly List<DisposeMethodStatus> myStatuses = new List<DisposeMethodStatus>();

        public bool InteriorShouldBeProcessed(ITreeNode element)
        {
            return !(element is ICSharpFunctionDeclaration);
        }

        public void ProcessBeforeInterior(ITreeNode element)
        {
            var functionDeclaration = element as ICSharpFunctionDeclaration;
            if (functionDeclaration == null)
                return;
            var psiModule = element.GetPsiModule();
            var sourceFile = element.GetSourceFile();
            if (sourceFile == null)
                return;
            var resolveContext = sourceFile.ResolveContext;
            var disposableInterface = DisposeUtil.GetDisposableInterface(psiModule, resolveContext);
            if (disposableInterface == null)
                return;
            var graf = CSharpControlFlowBuilder.Build(functionDeclaration) as CSharpControlFlowGraf;
            if (graf == null)
                return;

            var name = functionDeclaration.DeclaredName;
            var offset = functionDeclaration.GetNavigationRange().TextRange.StartOffset;

            var grafInspector = new ControlFlowInspector(functionDeclaration, graf, disposableInterface);
            var methodArguments = grafInspector.GetMethodArgumentStatuses();
            myStatuses.Add(new DisposeMethodStatus(name, offset, methodArguments, sourceFile));
        }

        public void ProcessAfterInterior(ITreeNode element)
        {
        }

        public bool ProcessingIsFinished
        {
            get { return false; }
        }

        private IList<DisposeMethodStatus> GetStatuses()
        {
            return myStatuses;
        }

        [CanBeNull]
        public static IList<DisposeMethodStatus> Build(IPsiSourceFile sourceFile)
        {
            var file = sourceFile.GetPsiFile<CSharpLanguage>(new DocumentRange(sourceFile.Document, 0)) as ICSharpFile;
            if (file == null)
                return null;
            return Build(file);
        }

        [CanBeNull]
        public static IList<DisposeMethodStatus> Build(ICSharpFile file)
        {
            var ret = new DisposeCacheBuilder();
            file.ProcessDescendants(ret);
            return ret.GetStatuses();
        }
    }
}
