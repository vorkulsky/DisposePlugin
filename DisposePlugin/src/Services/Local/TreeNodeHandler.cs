using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Services.Local
{
    public class TreeNodeHandler : ITreeNodeHandler
    {
        [NotNull]
        private readonly ITypeElement _disposableInterface;
        private readonly int _maxLevel;

        public TreeNodeHandler(int maxLevel, [NotNull] ITypeElement disposableInterface)
        {
            _disposableInterface = disposableInterface;
            _maxLevel = maxLevel;
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
            var connections = new Dictionary<IRegularParameterDeclaration, IVariableDeclaration>();
            foreach (var argument in invocationExpression.InvocationExpressionReference.Invocation.Arguments)
            {
                var cSharpArgument = argument as ICSharpArgument;
                if (cSharpArgument == null)
                    continue;
                var argumentExpression = cSharpArgument.Value;
                var varDecl = GetVariableDeclarationForExpression(argumentExpression, data);
                if (varDecl == null) // Если переменную не рассматриваем.
                    continue;
                if (data[varDecl] == null)
                    continue;
                var matchingArgument = GetMatchingArgument(argument);
                var matchingVarDecl = matchingArgument as IRegularParameterDeclaration;
                if (matchingVarDecl == null)
                    continue;
                connections.Add(matchingVarDecl, varDecl);
            }

            if (!connections.Any() && qualifierVariableDeclaration == null)
                return;

            if (_maxLevel <= 0)
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

            var offset = invokedFunctionDeclaration.GetNavigationRange().TextRange.StartOffset;
            var cache = invokedFunctionDeclaration.GetPsiServices().Solution.GetComponent<DisposeCache>();
            var methodStatus = cache.GetDisposeMethodStatusesForMethod(invokedFunctionDeclaration.GetSourceFile(), offset);
            if (methodStatus == null)
                return; //TODO пересчет хэша

            var parameterDeclarations = connections.Keys.ToArray();
            foreach (var parameterDeclaration in parameterDeclarations)
            {
                var number = GetNumberOfParameter(parameterDeclaration);
                if (number == null)
                    continue;
                var argumentStatus = methodStatus.MethodArguments.Where(a => a.Number == number).Select(a => a).FirstOrDefault();
                if (argumentStatus == null)
                    continue;
                if (argumentStatus.Status == VariableDisposeStatus.Disposed)
                    data[connections[parameterDeclaration]] = VariableDisposeStatus.Disposed;
                if (argumentStatus.Status == VariableDisposeStatus.Unknown)
                {
                   if (AnyoneMethodDispose(argumentStatus.InvokedMethods))
                       data[connections[parameterDeclaration]] = VariableDisposeStatus.Disposed;
                }
            }
        }

        private static bool AnyoneMethodDispose(IList<InvokedMethod> invokedMethods)
        {
            //TODO
        }

        private static int? GetNumberOfParameter(IRegularParameterDeclaration parameterDeclaration)
        {
            var parent = parameterDeclaration.Parent;
            var formalParameterList = parent as IFormalParameterList;
            if (formalParameterList == null)
                return null;
            var parameterDeclarations = formalParameterList.ParameterDeclarations;
            var index = parameterDeclarations.Where(p => p == parameterDeclaration).Select((p, i) => i+1).FirstOrDefault();
            if (index == 0)
                return null;
            return index;
        }

        [CanBeNull]
        private static IVariableDeclaration GetQualifierVariableDeclaration([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data)
        {
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return null;
            var qualifierExpression = invokedExpression.QualifierExpression;
            if (qualifierExpression == null || qualifierExpression is IThisExpression)
                return null; //TODO: this
            return GetVariableDeclarationForExpression(qualifierExpression, data);
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
            var variableDeclaration = declaration as ILocalVariableDeclaration;
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
