using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.Util;
using JetBrains.Util.Special;

namespace DisposePlugin.Services.Local
{
    public class TreeNodeHandler : ITreeNodeHandler
    {
        [NotNull] private readonly ITypeElement _disposableInterface;
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
            else if (treeNode is IUsingStatement)
            {
                TreeNodeHandlerUtil.ProcessUsingStatement(treeNode as IUsingStatement, data);
            }
            else if (treeNode is IAssignmentExpression)
            {
                TreeNodeHandlerUtil.ProcessAssignmentExpression(treeNode as IAssignmentExpression, data);
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
            var isInvocationOnDisposableThis = TreeNodeHandlerUtil.IsInvocationOnDisposableThis(invokedExpression,
                _disposableInterface);
            var qualifierVariableDeclaration = TreeNodeHandlerUtil.GetQualifierVariableDeclaration(invokedExpression);
            var qualifierDisposableVariableDeclaration = data[qualifierVariableDeclaration] != null
                ? qualifierVariableDeclaration
                : null;

            if (TreeNodeHandlerUtil.CheckOnDisposeInvocation(invocationExpression, data, isInvocationOnDisposableThis,
                qualifierDisposableVariableDeclaration))
                return;

            if (_maxLevel <= 0)
                return;

            ProcessSimpleInvocation(invocationExpression, data, qualifierDisposableVariableDeclaration,
                isInvocationOnDisposableThis, 1);
        }

        private void ProcessSimpleInvocation([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data,
            [CanBeNull] IVariableDeclaration qualifierDisposableVariableDeclaration, bool isInvocationOnDisposableThis,
            int level)
        {
            var connections = new Dictionary<IRegularParameterDeclaration, IVariableDeclaration>();
            var thisConnections = new List<IRegularParameterDeclaration>();
            CalculateConnectionOfDisposableVariables(invocationExpression, data, thisConnections, connections);

            if (!connections.Any() && !thisConnections.Any() && qualifierDisposableVariableDeclaration == null &&
                !isInvocationOnDisposableThis)
                return;

            var methodStatus = GetStatusForInvocationExpressionFromCache(invocationExpression);
            if (methodStatus == null)
                return;

            var parameterDeclarations = Enumerable.ToArray(connections.Keys);
            foreach (var parameterDeclaration in parameterDeclarations)
            {
                var argumentStatus = GetArgumentStatusForParameterDeclaration(parameterDeclaration, methodStatus);
                if (argumentStatus == null)
                    continue;
                switch (argumentStatus.Status)
                {
                    case VariableDisposeStatus.Disposed:
                        data[connections[parameterDeclaration]] = VariableDisposeStatus.Disposed;
                        break;
                    case VariableDisposeStatus.DependsOnInvocation:
                        if (AnyoneInvokedExpressionDispose(argumentStatus.InvokedExpressions, level))
                            data[connections[parameterDeclaration]] = VariableDisposeStatus.Disposed;
                        break;
                }
            }

            //this в качестве аргумента
            var thisParameterDeclarations = Enumerable.ToArray(connections.Keys);
            foreach (var parameterDeclaration in thisParameterDeclarations)
            {
                var argumentStatus = GetArgumentStatusForParameterDeclaration(parameterDeclaration, methodStatus);
                if (argumentStatus == null)
                    continue;
                switch (argumentStatus.Status)
                {
                    case VariableDisposeStatus.Disposed:
                        data.ThisStatus = VariableDisposeStatus.Disposed;
                        break;
                    case VariableDisposeStatus.DependsOnInvocation:
                        if (AnyoneInvokedExpressionDispose(argumentStatus.InvokedExpressions, level))
                            data.ThisStatus = VariableDisposeStatus.Disposed;
                        break;
                }
            }

            //обработка qualifierVariableDeclaration, в том числе this
            if (qualifierDisposableVariableDeclaration != null)
            {
                var argumentStatus = GetArgumentStatusByNumber(methodStatus, 0);
                /*TODO: нужно ли перестраивать кэш, если аргумента с нужным номером нет, и что тогда делать?
                Может быть, вести спиок файлов, для которых принудительно перестроен кэш. И перестраивать кэш для файла не более
                одного раза в том случае, если в кэше отсутствует что-то.*/
                if (argumentStatus != null)
                {
                    switch (argumentStatus.Status)
                    {
                        case VariableDisposeStatus.Disposed:
                            data[qualifierDisposableVariableDeclaration] = VariableDisposeStatus.Disposed;
                            break;
                        case VariableDisposeStatus.DependsOnInvocation:
                            if (AnyoneInvokedExpressionDispose(argumentStatus.InvokedExpressions, level))
                                data[qualifierDisposableVariableDeclaration] = VariableDisposeStatus.Disposed;
                            break;
                    }
                }
            }
            else if (isInvocationOnDisposableThis)
            {
                var argumentStatus = GetArgumentStatusByNumber(methodStatus, 0);
                if (argumentStatus != null)
                {
                    switch (argumentStatus.Status)
                    {
                        case VariableDisposeStatus.Disposed:
                            data.ThisStatus = VariableDisposeStatus.Disposed;
                            break;
                        case VariableDisposeStatus.DependsOnInvocation:
                            if (AnyoneInvokedExpressionDispose(argumentStatus.InvokedExpressions, level))
                                data.ThisStatus = VariableDisposeStatus.Disposed;
                            break;
                    }
                }
            }
        }

        private static void CalculateConnectionOfDisposableVariables(IInvocationExpression invocationExpression,
            ControlFlowElementData data, List<IRegularParameterDeclaration> thisConnections,
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
                    connections[matchingVarDecl] = varDecl;
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
            var argumentStatus = GetArgumentStatusByNumber(methodStatus, number.Value);
            return argumentStatus;
        }

        private static MethodArgumentStatus GetArgumentStatusByNumber(DisposeMethodStatus methodStatus, int number)
        {
            return methodStatus.MethodArguments.IfNull(() => new List<MethodArgumentStatus>())
                .Where(a => a.Number == number).Select(a => a).FirstOrDefault();
        }

        [CanBeNull]
        private static DisposeMethodStatus GetStatusForInvocationExpressionFromCache(
            IInvocationExpression invocationExpression)
        {
            var invokedDeclaredElement = invocationExpression.InvocationExpressionReference.Resolve().DeclaredElement;
            if (invokedDeclaredElement == null)
                return null;
            var invokedDeclaration = invokedDeclaredElement.GetDeclarations().FirstOrDefault();
            if (invokedDeclaration == null)
                return null;
            var invokedMethodDeclaration = invokedDeclaration as ICSharpFunctionDeclaration;
            if (invokedMethodDeclaration == null)
                return null;

            var offset = InvokedExpressionData.GetOffsetByNode(invokedMethodDeclaration);
            var cache = invokedMethodDeclaration.GetPsiServices().Solution.GetComponent<DisposeCache>();
            var invokedMethodSourceFile = invokedMethodDeclaration.GetSourceFile();
            if (invokedMethodSourceFile == null)
                return null;
            var methodStatus = cache.GetDisposeMethodStatusesForMethod(invokedMethodSourceFile, offset);
            if (methodStatus == null) // Принудительно пересчитываем кэш
            {
                var sourceFile = invocationExpression.GetSourceFile();
                if (DisposeCache.Accepts(invocationExpression.GetSourceFile()))
                {
                    var builtPart = cache.Build(sourceFile, false);
                    cache.Merge(sourceFile, builtPart);
                    methodStatus = cache.GetDisposeMethodStatusesForMethod(invokedMethodSourceFile, offset);
                }
            }
            return methodStatus;
        }

        private bool ProcessInvocationRecursively([NotNull] InvokedExpressionData invokedExpressionData, int level)
        {
            var invocationExpression = GetExpressionByInvokedExpressionData(invokedExpressionData);
            if (invocationExpression == null)
                return false;
            var methodStatus = GetStatusForInvocationExpressionFromCache(invocationExpression);
            if (methodStatus == null)
                return false;

            // SimpleDisposeInvocation здесь не может, т.к. в этом случае статус не был бы DependsOnInvocation
            var argumentStatus = invokedExpressionData.ArgumentPosition == 0
                ? GetArgumentStatusByNumber(methodStatus, 0)
                : GetMethodArgumentStatusByInvokedExpressionData(invokedExpressionData, invocationExpression,
                    methodStatus);
            if (argumentStatus == null)
                return false;
            switch (argumentStatus.Status)
            {
                case VariableDisposeStatus.Disposed:
                    return true;
                case VariableDisposeStatus.DependsOnInvocation:
                    return AnyoneInvokedExpressionDispose(argumentStatus.InvokedExpressions, level);
                default:
                    return false;
            }
        }

        [CanBeNull]
        private static MethodArgumentStatus GetMethodArgumentStatusByInvokedExpressionData(
            [NotNull] InvokedExpressionData invokedExpressionData, [NotNull] IInvocationExpression invocationExpression,
            [NotNull] DisposeMethodStatus methodStatus)
        {
            var arguments = invocationExpression.InvocationExpressionReference.Invocation.Arguments;
            if (arguments.Count < invokedExpressionData.ArgumentPosition)
                return null;
            var argument = arguments.ElementAt(invokedExpressionData.ArgumentPosition - 1);
            var matchingArgument = TreeNodeHandlerUtil.GetMatchingArgument(argument);
            var matchingVarDecl = matchingArgument as IRegularParameterDeclaration;
            if (matchingVarDecl == null)
                return null;
            var argumentStatus = GetArgumentStatusForParameterDeclaration(matchingVarDecl, methodStatus);
            return argumentStatus;
        }

        [CanBeNull]
        private static IInvocationExpression GetExpressionByInvokedExpressionData(
            InvokedExpressionData invokedExpressionData)
        {
            var sourceFile = invokedExpressionData.PsiSourceFile;
            if (sourceFile == null)
                return null;
            return InvokedExpressionData.GetNodeByOffset<IInvocationExpression>(sourceFile, invokedExpressionData.Offset);
        }

        private bool AnyoneInvokedExpressionDispose(ICollection<InvokedExpressionData> invokedExpressions, int level)
        {
            if (invokedExpressions.Count == 0)
                return false;
            if (level != _maxLevel)
                return invokedExpressions.Any(invokedExpression =>
                    ProcessInvocationRecursively(invokedExpression, level + 1));
            return false;
        }
    }
}