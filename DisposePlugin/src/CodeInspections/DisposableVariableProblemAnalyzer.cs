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
            var disposableInterface = DisposeUtil.GetDisposableInterface(psiModule, resolveContext);
            if (disposableInterface != null) _disposableInterface = disposableInterface;
            else return;

            if (IsWrappedInUsing(element) || !VariableTypeImplementsDisposable(element))
                return;

            consumer.AddHighlighting(new LocalVariableNotDisposed(), element.GetNameDocumentRange(), element.GetContainingFile());
        }

        private bool VariableTypeImplementsDisposable([NotNull] ILocalVariableDeclaration element)
        {
            IDeclaredElement variableTypeDeclaredElement;
            var variableReferenceName = element.ScalarTypeName;
            if (variableReferenceName != null)
            {
                variableTypeDeclaredElement = variableReferenceName.Reference.Resolve().DeclaredElement;
            }
            else
            {
                var type = DisposeUtil.CalculateExplicitType(element);
                var declaredType = type as IDeclaredType;
                if (declaredType == null)
                    return false;
                variableTypeDeclaredElement = declaredType.GetTypeElement();
            }

            if (variableTypeDeclaredElement == null)
                return false;

            var implementsDisposableInterface = DisposeUtil.HasDisposable(variableTypeDeclaredElement, _disposableInterface);
            return implementsDisposableInterface;
        }

        private static bool IsWrappedInUsing([NotNull] ILocalVariableDeclaration localVariableDeclaration)
        {
            var multipleLocalVariableDeclaration = localVariableDeclaration.Parent as IMultipleLocalVariableDeclaration;
            if (multipleLocalVariableDeclaration == null)
                return false;
            return multipleLocalVariableDeclaration.Parent is IUsingStatement;
        }
    }
}
