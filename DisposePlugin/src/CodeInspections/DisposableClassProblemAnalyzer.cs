using System.Collections.Generic;
using System.Linq;
using DisposePlugin.CodeInspections.Highlighting;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.CodeInspections
{
    [ElementProblemAnalyzer(new[] { typeof(IClassDeclaration) },
        HighlightingTypes = new[] { typeof(NotDisposableContainsDisposableField) })]
    public class DisposableClassProblemAnalyzer : ElementProblemAnalyzer<IClassDeclaration>
    {
        [NotNull] private ITypeElement _disposableInterface;
        protected override void Run(IClassDeclaration element, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            var psiModule = data.Process.PsiModule;
            var resolveContext = data.Process.SourceFile.ResolveContext;
            var disposableInterface = DisposeUtil.GetDisposableInterface(psiModule, resolveContext);
            if (disposableInterface != null) _disposableInterface = disposableInterface;
            else return;

            var existingDispose = DisposeUtil.FindDispose(element);
            if (existingDispose != null)
                return;

            var disposableMembers = GetDisposableMembers(element);
            if (!disposableMembers.Any())
                return;

            var implementsDisposableInterface = DisposeUtil.HasDisposable(element, _disposableInterface);

            consumer.AddHighlighting(new NotDisposableContainsDisposableField(implementsDisposableInterface),
                element.GetNameDocumentRange(), element.GetContainingFile());
        }

        private IEnumerable<IField> GetDisposableMembers(IClassDeclaration classDeclaration)
        {
            var fieldDeclarations = classDeclaration.FieldDeclarations;
            var disposableType = TypeFactory.CreateType(_disposableInterface);
            return from fieldDeclaration in fieldDeclarations
                   let member = fieldDeclaration.DeclaredElement
                   let memberType = fieldDeclaration.Type as IDeclaredType   
                   where !member.IsStatic
                        && !member.IsConstant && !member.IsSynthetic()
                        && memberType != null
                        && memberType.CanUseExplicitly(classDeclaration)
                        && memberType.IsSubtypeOf(disposableType)
                   select member;
        }
    }
}
