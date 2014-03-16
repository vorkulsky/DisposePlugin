using System.Collections.Generic;
using System.Linq;
using DisposePlugin.CodeInspections;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.ControlFlow.CSharp;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.src.CodeInspections
{
    public class TreeNodeHandler
    {
        [NotNull]
        private readonly ITypeElement _disposableInterface;

        public TreeNodeHandler([NotNull] ITypeElement disposableInterface)
        {
            _disposableInterface = disposableInterface;
        }

        public void ProcessTreeNode([NotNull] ITreeNode treeNode, ControlFlowElementData data)
        {
            if (treeNode is ILocalVariableDeclaration)
            {
                ProcessLocalVariableDeclaration(treeNode as ILocalVariableDeclaration, data);
            }
            else if (treeNode is IInvocationExpression)
            {
                ProcessInvocationExpression(treeNode as IInvocationExpression, data);
            }
        }

        private void ProcessLocalVariableDeclaration([NotNull] ILocalVariableDeclaration variableDeclaration,
            ControlFlowElementData data)
        {
            if (!DisposeUtil.IsWrappedInUsing(variableDeclaration) &&
                DisposeUtil.VariableTypeImplementsDisposable(variableDeclaration, _disposableInterface))
            {
                //RunAnalysis(variableDeclaration.DeclaredElement);
                data[variableDeclaration] = VariableDisposeStatus.NotDisposed;
            }
        }

        private void ProcessInvocationExpression([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data)
        {
            var variableDeclaration = GetQualifierVariableDeclaration(invocationExpression, data);
            if (variableDeclaration != null && IsSimpleDisposeInvocation(invocationExpression))
            {
                data[variableDeclaration] = VariableDisposeStatus.Disposed;
                return;
            }
            ProcessSimpleInvocation(invocationExpression, variableDeclaration, data);
        }

        private void ProcessSimpleInvocation([NotNull] IInvocationExpression invocationExpression,
            [CanBeNull] IVariableDeclaration qualifierVariableDeclaration, ControlFlowElementData data)
        {
            var connections = new Dictionary<IVariableDeclaration, IVariableDeclaration>();
            foreach (var argument in invocationExpression.InvocationExpressionReference.Invocation.Arguments)
            {
                var cSharpArgument = argument as ICSharpArgument;
                if (cSharpArgument == null)
                    continue;
                var argumentExpression = cSharpArgument.Value;
                var varDecl = GetVariableDeclarationForExpression(argumentExpression, data);
                if (varDecl == null) // Если переменную не рассматриваем.
                    continue;
                var invocation = cSharpArgument.Invocation;
                if (invocation == null)
                    continue;
                var reference = invocation.Reference as IReference;
                if (reference == null)
                    continue;
                var argumentVariableDeclaration = GetVariableDeclarationByReference(reference);
                if (argumentVariableDeclaration == null)
                    continue;
                if (data[argumentVariableDeclaration] == null)
                    continue;
                var matchingArgument = GetMatchingArgument(argument);
                var matchingVarDecl = matchingArgument as IVariableDeclaration;
                if (matchingVarDecl == null)
                    continue;
                connections.Add(matchingVarDecl, varDecl);
            }

            if (!connections.Any() && qualifierVariableDeclaration == null)
                return;

            var invokedDeclaredElement = invocationExpression.InvocationExpressionReference.Resolve().DeclaredElement;
            if (invokedDeclaredElement == null)
                return;
            var invokedDeclaration = invokedDeclaredElement.GetDeclarations().FirstOrDefault();
            if (invokedDeclaration == null)
                return;
            var invokedFunctionDeclaration = invokedDeclaration as ICSharpFunctionDeclaration;
            if (invokedFunctionDeclaration == null)
                return;

            var graf = CSharpControlFlowBuilder.Build(invokedFunctionDeclaration) as CSharpControlFlowGraf;
            if (graf == null)
                return;

            var grafInspector = new ControlFlowInspector(graf, _disposableInterface);
        }

        [CanBeNull]
        private static IVariableDeclaration GetQualifierVariableDeclaration([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data)
        {
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return null;
            var qualifierExpression = invokedExpression.QualifierExpression;
            if (qualifierExpression == null)
                return null;
            return GetVariableDeclarationForExpression(invokedExpression.QualifierExpression, data);
        }

        [CanBeNull]
        private static IVariableDeclaration GetVariableDeclarationForExpression([NotNull] ICSharpExpression expression,
            ControlFlowElementData data)
        {
            var referenceExpression = expression as IReferenceExpression;
            if (referenceExpression == null)
                return null;
            var variableDeclaration = GetVariableDeclarationByReference(referenceExpression.Reference);
            if (variableDeclaration == null)
                return null;
            return data[variableDeclaration] != null ? variableDeclaration : null;
        }

        [CanBeNull]
        private static IVariableDeclaration GetVariableDeclarationByReference([NotNull] IReference reference)
        {
            var declaredElement = reference.Resolve().DeclaredElement;
            if (declaredElement == null)
                return null;
            var declaration = declaredElement.GetDeclarations().FirstOrDefault();
            if (declaration == null)
                return null;
            var variableDeclaration = declaration as IVariableDeclaration;
            return variableDeclaration;
        }

        private static IDeclaration GetMatchingArgument(ICSharpArgumentInfo argument)
        {
            var matchingParameter = argument.MatchingParameter;
            if (matchingParameter == null)
                return null;
            var declaredElement = matchingParameter.Element as IDeclaredElement;
            var declaration = declaredElement.GetDeclarations().FirstOrDefault();
            return declaration;
        }

        private static bool IsSimpleDisposeInvocation([NotNull] IInvocationExpression invocationExpression)
        {
            if (invocationExpression.Arguments.Count != 0)
                return false;
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return false;
            var name = invokedExpression.NameIdentifier.Name;
            if (!name.Equals("Dispose"))
                return false;
            return true;
        }
    }
}
