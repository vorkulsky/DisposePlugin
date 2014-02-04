using DisposePlugin.CodeInspections.Highlighting;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.CodeInspections
{
    [ElementProblemAnalyzer(new[] { typeof(ILocalVariableDeclaration) },
        HighlightingTypes = new[] { typeof(LocalVariableNotDisposed) })]
    public class DisposableVariableProblemAnalyzer : ElementProblemAnalyzer<ILocalVariableDeclaration>
    {
        [NotNull] private ITypeElement _disposableInterface;
        protected override void Run(ILocalVariableDeclaration element, ElementProblemAnalyzerData data,
            IHighlightingConsumer consumer)
        {
            var psiModule = data.Process.PsiModule;
            var resolveContext = data.Process.SourceFile.ResolveContext;
            _disposableInterface = DisposeUtil.GetDisposableInterface(psiModule, resolveContext);
            if (_disposableInterface == null)
                return;

            if (!(VariableTypeImplementsDisposable(element) || VariableInitByTypeImplementsDisposable(element)))
                return;

            consumer.AddHighlighting(new LocalVariableNotDisposed(), element.GetNameDocumentRange(), element.GetContainingFile());
        }

        private bool VariableTypeImplementsDisposable([NotNull] ILocalVariableDeclaration element)
        {
            var variableReferenceName = element.ScalarTypeName;
            if (variableReferenceName == null)
                return false;
            var variableTypeDeclaredElement = variableReferenceName.Reference.Resolve().DeclaredElement;
            if (variableTypeDeclaredElement == null)
                return false;

            var implementsDisposableInterface = DisposeUtil.HasDisposable(variableTypeDeclaredElement, _disposableInterface);
            return implementsDisposableInterface;
        }

        private bool VariableInitByTypeImplementsDisposable([NotNull] ILocalVariableDeclaration element)
        {
            var expressionInitializer = element.Initial as IExpressionInitializer;
            if (expressionInitializer == null)
                return false;
            var expression = expressionInitializer.Value as IObjectCreationExpression;
            if (expression == null)
                return false;
            var initializerTypeReference = expression.TypeReference;
            if (initializerTypeReference == null)
                return false;
            var initializerTypeDeclaredElement = initializerTypeReference.Resolve().DeclaredElement;
            if (initializerTypeDeclaredElement == null)
                return false;

            var implementsDisposableInterface = DisposeUtil.HasDisposable(initializerTypeDeclaredElement, _disposableInterface);
            return implementsDisposableInterface;
        }
    }
}
