using System.Collections.Generic;
using System.Linq;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Daemon.Stages.Dispatcher;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.CSharp.Util;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Tree;


namespace DisposePlugin.CodeInspections
{
    [ElementProblemAnalyzer(new[] { typeof(IClassDeclaration) },
        HighlightingTypes = new[] { typeof(NotDisposableContainsDisposableField) })]
    class DisposeableClassProblemAnalyzer : ElementProblemAnalyzer<IClassDeclaration>
    {
        protected override void Run(IClassDeclaration element, ElementProblemAnalyzerData data, IHighlightingConsumer consumer)
        {
            var psiModule = data.Process.PsiModule;
            var resolveContext = data.Process.SourceFile.ResolveContext;
            var disposableInterface = GetDisposableInterface(psiModule, resolveContext);
            if (disposableInterface == null)
                return;

            var existingDispose = FindDispose(element);
            if (existingDispose != null)
                return;

            var disposableMembers = GetDisposableMembers(element, disposableInterface);
            if (!disposableMembers.Any())
                return;

            var implementsDisposableInterface = HasDisposable(element, disposableInterface);

            consumer.AddHighlighting(new NotDisposableContainsDisposableField(implementsDisposableInterface),
                element.GetNameDocumentRange(), element.GetContainingFile());
        }

        private static IEnumerable<IField> GetDisposableMembers(IClassDeclaration classDeclaration, ITypeElement disposableInterface)
        {
            var fieldDeclarations = classDeclaration.FieldDeclarations;
            var disposableType = TypeFactory.CreateType(disposableInterface);
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

        private static bool HasDisposable(IClassLikeDeclaration classDeclaration, ITypeElement disposableInterface)
        {
            var ownTypeElement = classDeclaration.DeclaredElement;
            if (ownTypeElement == null)
                return false;
            var ownType = TypeFactory.CreateType(ownTypeElement);
            var disposableType = TypeFactory.CreateType(disposableInterface);
            return ownType.IsSubtypeOf(disposableType);
        }

        private static IMethod FindDispose(IClassLikeDeclaration classDeclaration)
        {
            if (classDeclaration.DeclaredElement == null)
                return null;

            return classDeclaration.DeclaredElement.Methods
              .FirstOrDefault(method => method.ShortName == "Dispose"
                                        && method.ReturnType.IsVoid()
                                        && method.Parameters.Count == 0);
        }

        private static ITypeElement GetDisposableInterface(IPsiModule psiModule, IModuleReferenceResolveContext resolveContext)
        {
            return TypeFactory.CreateTypeByCLRName("System.IDisposable", psiModule, resolveContext).GetTypeElement();
        }
    }
}
