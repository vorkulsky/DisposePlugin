using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Services.Invoking
{
    public class ControlFlowInspector : Services.ControlFlowInspector
    {
        public ControlFlowInspector([NotNull] ICSharpFunctionDeclaration functionDeclaration,
            [NotNull] CSharpControlFlowGraf graf, [NotNull] ITypeElement disposableInterface)
            : base(functionDeclaration, graf, disposableInterface, new TreeNodeHandlerFactory(), new ControlFlowElementDataStorage())
        {
            
        }

        public IList<MethodArgumentStatus> GetMethodArgumentStatuses()
        {
            var disposableArguments = GetDisposableArguments();
            var processThis = IsThisDisposeable();
            DoStep(null, Graf.EntryElement, true);
            return new List<MethodArgumentStatus>();
        }

        private bool IsThisDisposeable()
        {
            var typeDeclaration = FunctionDeclaration.GetContainingTypeDeclaration();
            return DisposeUtil.HasDisposable(typeDeclaration, DisposableInterface);
        }

        private Dictionary<int, IDeclaration> GetDisposableArguments()
        {
            var allArguments = FunctionDeclaration.DeclaredElement.Parameters;
            var disposableArguments = new Dictionary<int, IDeclaration>();

            var i = 0;
            foreach (var param in allArguments)
            {
                i++;
                var type = param.Type;
                var scalarType = type.GetScalarType();
                if (scalarType == null)
                    continue;
                var declaredElement = scalarType.Resolve().DeclaredElement;
                if (declaredElement == null)
                    continue;
                if (DisposeUtil.HasDisposable(declaredElement, DisposableInterface))
                {
                    var declarations = param.GetDeclarationsIn(FunctionDeclaration.GetSourceFile());
                    var declaration = declarations.FirstOrDefault();
                    if (declaration == null)
                        continue;
                    disposableArguments.Add(i, declaration);
                }
            }
            return disposableArguments;
        }
    }
}
