using System.Linq;
using JetBrains.Annotations;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Util
{
    public static class DisposeUtil
    {
        public static bool HasDisposable([NotNull] IDeclaredElement declaredElement,
            [NotNull] ITypeElement disposableInterface)
        {
            var typeElement = declaredElement as ITypeElement;
            return HasDisposable(typeElement, disposableInterface);
        }

        public static bool HasDisposable([NotNull] ITypeDeclaration declaration,
            [NotNull] ITypeElement disposableInterface)
        {
            var ownTypeElement = declaration.DeclaredElement;
            return HasDisposable(ownTypeElement, disposableInterface);
        }

        private static bool HasDisposable([CanBeNull] ITypeElement typeElement,
            [NotNull] ITypeElement disposableInterface)
        {
            if (typeElement == null)
                return false;
            var type = TypeFactory.CreateType(typeElement);
            var disposableType = TypeFactory.CreateType(disposableInterface);
            return type.IsSubtypeOf(disposableType);
        }

        [CanBeNull]
        public static IMethod FindDispose([NotNull] ITypeDeclaration declaration)
        {
            if (declaration.DeclaredElement == null)
                return null;

            return declaration.DeclaredElement.Methods
                .FirstOrDefault(method => method.ShortName == "Dispose"
                                          && method.ReturnType.IsVoid()
                                          && method.Parameters.Count == 0);
        }

        [CanBeNull]
        public static ITypeElement GetDisposableInterface([NotNull] IPsiModule psiModule,
            [NotNull] IModuleReferenceResolveContext resolveContext)
        {
            return TypeFactory.CreateTypeByCLRName("System.IDisposable", psiModule, resolveContext).GetTypeElement();
        }

        [CanBeNull]
        public static IType CalculateExplicitType([NotNull] ILocalVariableDeclaration localVariableDeclaration)
        {
            if (!localVariableDeclaration.IsVar)
                return null;
            var multipleLocalVariableDeclaration = localVariableDeclaration.Parent as IMultipleLocalVariableDeclaration;
            if (multipleLocalVariableDeclaration == null)
                return null;
            if (multipleLocalVariableDeclaration.Declarators.Count != 1)
                return null;
            var csharpExpression = InitializerToExpression(localVariableDeclaration.Initial);
            if (csharpExpression == null)
                return null;
            return csharpExpression.Type();
        }

        [CanBeNull]
        private static ICSharpExpression InitializerToExpression([CanBeNull] IVariableInitializer initializer)
        {
            if (initializer == null)
                return null;
            var expressionInitializer = initializer as IExpressionInitializer;
            if (expressionInitializer != null)
                return expressionInitializer.Value;
            return null;
        }

        public static bool VariableTypeImplementsDisposable([NotNull] ILocalVariableDeclaration element,
            [NotNull] ITypeElement disposableInterface)
        {
            IDeclaredElement variableTypeDeclaredElement;
            var variableReferenceName = element.ScalarTypeName;
            if (variableReferenceName != null)
            {
                variableTypeDeclaredElement = variableReferenceName.Reference.Resolve().DeclaredElement;
            }
            else
            {
                var type = CalculateExplicitType(element);
                var declaredType = type as IDeclaredType;
                if (declaredType == null)
                    return false;
                variableTypeDeclaredElement = declaredType.GetTypeElement();
            }

            if (variableTypeDeclaredElement == null)
                return false;

            var implementsDisposableInterface = HasDisposable(variableTypeDeclaredElement, disposableInterface);
            return implementsDisposableInterface;
        }

        public static bool IsWrappedInUsing([NotNull] ILocalVariableDeclaration localVariableDeclaration)
        {
            var multipleLocalVariableDeclaration = localVariableDeclaration.Parent as IMultipleLocalVariableDeclaration;
            if (multipleLocalVariableDeclaration == null)
                return false;
            return multipleLocalVariableDeclaration.Parent is IUsingStatement;
        }
    }
}