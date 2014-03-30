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
        private readonly Dictionary<int, IDeclaration> _disposableArguments;
        private readonly bool _processThis;
        [NotNull]
        private readonly ITypeElement _disposableInterface;

        public ControlFlowInspector([NotNull] ICSharpFunctionDeclaration functionDeclaration,
            [NotNull] CSharpControlFlowGraf graf, [NotNull] ITypeElement disposableInterface)
            : base(functionDeclaration, graf, new TreeNodeHandlerFactory(disposableInterface), new ControlFlowElementDataStorage())
        {
            _disposableInterface = disposableInterface;
            _disposableArguments = GetDisposableArguments();
            _processThis = IsThisDisposable();
        }

        public IList<MethodArgumentStatus> GetMethodArgumentStatuses()
        {
            if (_disposableArguments.Count == 0 && !_processThis)
                return new List<MethodArgumentStatus>();
            //DoStep(null, Graf.EntryElement, true);
            return new List<MethodArgumentStatus>();
        }

        private bool IsThisDisposable()
        {
            var typeDeclaration = FunctionDeclaration.GetContainingTypeDeclaration();
            return DisposeUtil.HasDisposable(typeDeclaration, _disposableInterface);
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
                if (DisposeUtil.HasDisposable(declaredElement, _disposableInterface))
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
