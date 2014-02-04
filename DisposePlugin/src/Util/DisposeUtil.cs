using System.Linq;
using JetBrains.Annotations;
using JetBrains.Metadata.Reader.API;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Modules;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Util
{
    public class DisposeUtil
    {
        public static bool HasDisposable([NotNull] IDeclaredElement declaredElement, [NotNull] ITypeElement disposableInterface)
        {
            var ownTypeElement = declaredElement as ITypeElement;
            return HasDisposable(ownTypeElement, disposableInterface);
        }

        public static bool HasDisposable([NotNull] ITypeDeclaration declaration, [NotNull] ITypeElement disposableInterface)
        {
            var ownTypeElement = declaration.DeclaredElement;
            return HasDisposable(ownTypeElement, disposableInterface);
        }

        private static bool HasDisposable([CanBeNull] ITypeElement ownTypeElement, [NotNull] ITypeElement disposableInterface)
        {
            if (ownTypeElement == null)
                return false;
            var ownType = TypeFactory.CreateType(ownTypeElement);
            var disposableType = TypeFactory.CreateType(disposableInterface);
            return ownType.IsSubtypeOf(disposableType);
        }

        public static IMethod FindDispose([NotNull] ITypeDeclaration declaration)
        {
            if (declaration.DeclaredElement == null)
                return null;

            return declaration.DeclaredElement.Methods
              .FirstOrDefault(method => method.ShortName == "Dispose"
                                        && method.ReturnType.IsVoid()
                                        && method.Parameters.Count == 0);
        }

        public static ITypeElement GetDisposableInterface([NotNull] IPsiModule psiModule,
            [NotNull] IModuleReferenceResolveContext resolveContext)
        {
            return TypeFactory.CreateTypeByCLRName("System.IDisposable", psiModule, resolveContext).GetTypeElement();
        }
    }
}
