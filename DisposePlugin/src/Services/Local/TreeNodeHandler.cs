using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using DisposePlugin.src.Util;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Files;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;

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
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return;
            var isInvocationOnDisposableThis = TreeNodeHandlerUtil.IsInvocationOnDisposableThis(invokedExpression, _disposableInterface);
            var qualifierVariableDeclaration = TreeNodeHandlerUtil.GetQualifierVariableDeclaration(invokedExpression);
            var qualifierDisposableVariableDeclaration = data[qualifierVariableDeclaration] != null
                ? qualifierVariableDeclaration : null;

            if (TreeNodeHandlerUtil.CheckOnDisposeInvocation(invocationExpression, data, isInvocationOnDisposableThis,
                qualifierDisposableVariableDeclaration))
                return;

            if (_maxLevel <= 0)
                return;

            ProcessSimpleInvocation(invocationExpression, data, qualifierDisposableVariableDeclaration, isInvocationOnDisposableThis, 1);
        }

        private void ProcessSimpleInvocation([NotNull] IInvocationExpression invocationExpression, ControlFlowElementData data,
            [CanBeNull] IVariableDeclaration qualifierDisposableVariableDeclaration, bool isInvocationOnDisposableThis, int level)
        {
            var connections = new Dictionary<IRegularParameterDeclaration, IVariableDeclaration>();
            var thisConnections = new List<IVariableDeclaration>();
            CalculateConnectionOfDisposableVariables(invocationExpression, data, thisConnections, connections);

            if (!connections.Any() && !thisConnections.Any() && qualifierDisposableVariableDeclaration == null && !isInvocationOnDisposableThis)
                return;

            var methodStatus = GetStatusForInvocationExpressionFromCache(invocationExpression);

            //TODO this в качестве аргумента
            //TODO обработка qualifierVariableDeclaration, в том числе this

            var parameterDeclarations = Enumerable.ToArray(connections.Keys);
            foreach (var parameterDeclaration in parameterDeclarations)
            {
                var argumentStatus = GetArgumentStatusForParameterDeclaration(parameterDeclaration, methodStatus);
                if (argumentStatus == null)
                    continue;
                if (argumentStatus.Status == VariableDisposeStatus.Disposed)
                    data[connections[parameterDeclaration]] = VariableDisposeStatus.Disposed;
                if (argumentStatus.Status == VariableDisposeStatus.DependsOnInvocation)
                {
                    if (AnyoneMethodDispose(argumentStatus.InvokedMethods, level))
                        data[connections[parameterDeclaration]] = VariableDisposeStatus.Disposed;
                }
            }
        }

        private static void CalculateConnectionOfDisposableVariables(IInvocationExpression invocationExpression,
            ControlFlowElementData data, List<IVariableDeclaration> thisConnections,
            Dictionary<IRegularParameterDeclaration, IVariableDeclaration> connections)
        {
            foreach (var argument in invocationExpression.InvocationExpressionReference.Invocation.Arguments)
            {
                var cSharpArgument = argument as ICSharpArgument;
                if (cSharpArgument == null)
                    continue;
                var argumentExpression = cSharpArgument.Value;
                IRegularParameterDeclaration matchingVarDecl;
                if (argumentExpression is IThisExpression && data.ThisStatus != null)
                {
                    if (GetMatchingParameterDeclaration(argument, out matchingVarDecl))
                        continue;
                    thisConnections.Add(matchingVarDecl);
                    continue;
                }
                var varDecl = TreeNodeHandlerUtil.GetVariableDeclarationForReferenceExpression(argumentExpression);
                if (data[varDecl] != null) // Если переменную не рассматриваем.
                {
                    if (GetMatchingParameterDeclaration(argument, out matchingVarDecl))
                        continue;
                    connections.Add(matchingVarDecl, varDecl);
                }
            }
        }

        private static bool GetMatchingParameterDeclaration(ICSharpArgumentInfo argument,
            out IRegularParameterDeclaration matchingVarDecl)
        {
            var matchingArgument = TreeNodeHandlerUtil.GetMatchingArgument(argument);
            matchingVarDecl = matchingArgument as IRegularParameterDeclaration;
            if (matchingVarDecl == null)
                return true;
            return false;
        }

        private static MethodArgumentStatus GetArgumentStatusForParameterDeclaration(
            [NotNull] IRegularParameterDeclaration parameterDeclaration, DisposeMethodStatus methodStatus)
        {
            var number = TreeNodeHandlerUtil.GetNumberOfParameter(parameterDeclaration);
            if (number == null)
                return null;
            var argumentStatus = methodStatus.MethodArguments.Where(a => a.Number == number).Select(a => a).FirstOrDefault();
            return argumentStatus;
        }

        private static DisposeMethodStatus GetStatusForInvocationExpressionFromCache(IInvocationExpression invocationExpression)
        {
            var invokedDeclaredElement = invocationExpression.InvocationExpressionReference.Resolve().DeclaredElement;
            if (invokedDeclaredElement == null)
                return null;
            var invokedDeclaration = invokedDeclaredElement.GetDeclarations().FirstOrDefault();
            if (invokedDeclaration == null)
                return null;
            var invokedFunctionDeclaration = invokedDeclaration as ICSharpFunctionDeclaration;
            if (invokedFunctionDeclaration == null)
                return null;

            var offset = invokedFunctionDeclaration.GetNavigationRange().TextRange.StartOffset;
            var cache = invokedFunctionDeclaration.GetPsiServices().Solution.GetComponent<DisposeCache>();
            var invokedFunctionSourceFile = invokedFunctionDeclaration.GetSourceFile();
            if (invokedFunctionSourceFile == null)
                return null;
            var methodStatus = cache.GetDisposeMethodStatusesForMethod(invokedFunctionSourceFile, offset);
            if (methodStatus == null) // Принудительно пересчитываем кэш
            {
                var sourceFile = invocationExpression.GetSourceFile();
                if (DisposeCache.Accepts(invocationExpression.GetSourceFile()))
                {
                    var builtPart = cache.Build(sourceFile, false);
                    cache.Merge(sourceFile, builtPart);
                    methodStatus = cache.GetDisposeMethodStatusesForMethod(invokedFunctionSourceFile, offset);
                }
            }
            return methodStatus;
        }

        private bool ProcessInvocationRecursively([NotNull] InvokedMethod invokedMethod, int level)
        {
            var sourceFile = invokedMethod.PsiSourceFile;
            if (sourceFile == null) // Такого не должно случиться.
                return false;
            var file = sourceFile.GetPsiFile<CSharpLanguage>(new DocumentRange(sourceFile.Document, 0));
            if (file == null)
                return false;
            var elem = file.FindNodeAt(new TreeTextRange(new TreeOffset(invokedMethod.Offset), 1));
            if (elem == null)
                return false;
            var invocationExpression = TreeNodeHandlerUtil.GoUpToNodeWithType<IInvocationExpression>(elem);
            if (invocationExpression == null)
                return false;
            var arguments = invocationExpression.InvocationExpressionReference.Invocation.Arguments;
            if (arguments.Count < invokedMethod.ArgumentPosition)
                return false;
            var argument = arguments.ElementAt(invokedMethod.ArgumentPosition - 1);
            var matchingArgument = TreeNodeHandlerUtil.GetMatchingArgument(argument);
            var matchingVarDecl = matchingArgument as IRegularParameterDeclaration;
            if (matchingVarDecl == null)
                return false;
            var methodStatus = GetStatusForInvocationExpressionFromCache(invocationExpression);
            if (methodStatus == null)
                return false;
            var argumentStatus = GetArgumentStatusForParameterDeclaration(matchingVarDecl, methodStatus);
            if (argumentStatus == null)
                return false;
            if (argumentStatus.Status == VariableDisposeStatus.Disposed)
                return true;
            if (argumentStatus.Status == VariableDisposeStatus.DependsOnInvocation)
            {
                return AnyoneMethodDispose(argumentStatus.InvokedMethods, level);
            }
            return false;
        }

        private bool AnyoneMethodDispose(ICollection<InvokedMethod> invokedMethods, int level)
        {
            if (invokedMethods.Count == 0)
                return false;
            if (level != _maxLevel)
                return invokedMethods.Any(invokedMethod => ProcessInvocationRecursively(invokedMethod, level));
            return false;
        }
    }
}
