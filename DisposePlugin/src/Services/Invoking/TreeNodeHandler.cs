using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Services.Invoking
{
    public class TreeNodeHandler : ITreeNodeHandler
    {
        [NotNull] private readonly ITypeElement _disposableInterface;

        public TreeNodeHandler([NotNull] ITypeElement disposableInterface)
        {
            _disposableInterface = disposableInterface;
        }

        public void ProcessTreeNode(ITreeNode treeNode, ControlFlowElementData data)
        {
            if (treeNode is IInvocationExpression)
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

            ProcessSimpleInvocation(invocationExpression, data, qualifierDisposableVariableDeclaration,
                isInvocationOnDisposableThis);
        }

        private void ProcessSimpleInvocation([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data,
            [CanBeNull] IVariableDeclaration qualifierDisposableVariableDeclaration, bool isInvocationOnDisposableThis)
        {
            var positions = new Dictionary<IVariableDeclaration, byte>();
            var thisPositions = new List<byte>();
            CalculatePositionOfDisposableVariables(invocationExpression, data, thisPositions, positions);

            if (!positions.Any() && !thisPositions.Any() && qualifierDisposableVariableDeclaration == null &&
                !isInvocationOnDisposableThis)
                return;

            var referenceExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (referenceExpression == null)
                return;
            var nameIdentifier = referenceExpression.NameIdentifier;
            if (nameIdentifier == null)
                return;
            var name = nameIdentifier.Name;
            var offset = InvokedExpressionData.GetOffsetByNode(invocationExpression);
            var sourceFile = invocationExpression.GetSourceFile();

            foreach (var position in positions)
                SaveInvocationData(data, position.Key, position.Value, name, offset, sourceFile);

            //this в качестве аргумента
            if (thisPositions.Any())
                data.ThisStatus = VariableDisposeStatus.DependsOnInvocation;
            foreach (var position in thisPositions)
            {
                var invokedExpression = new InvokedExpressionData(name, offset, position, sourceFile);
                data.ThisInvokedExpressions.Add(invokedExpression);
            }

            //обработка qualifierVariableDeclaration, в том числе this
            if (qualifierDisposableVariableDeclaration != null)
                SaveInvocationData(data, qualifierDisposableVariableDeclaration, 0, name, offset, sourceFile);
            else if (isInvocationOnDisposableThis)
            {
                data.ThisStatus = VariableDisposeStatus.DependsOnInvocation;
                var invokedExpression = new InvokedExpressionData(name, offset, 0, sourceFile);
                data.ThisInvokedExpressions.Add(invokedExpression);
            }
        }

        private static void SaveInvocationData(ControlFlowElementData data, IVariableDeclaration variable,
            byte position, string name, int offset, IPsiSourceFile sourceFile)
        {
            data[variable] = VariableDisposeStatus.DependsOnInvocation;
            var invokedExpression = new InvokedExpressionData(name, offset, position, sourceFile);
            data.InvokedExpressions.Add(variable, invokedExpression);
        }

        private static void CalculatePositionOfDisposableVariables(IInvocationExpression invocationExpression,
            ControlFlowElementData data, List<byte> thisPositions, IDictionary<IVariableDeclaration, byte> positions)
        {
            byte i = 0;
            foreach (var argument in invocationExpression.InvocationExpressionReference.Invocation.Arguments)
            {
                i++;
                var cSharpArgument = argument as ICSharpArgument;
                if (cSharpArgument == null)
                    continue;
                var argumentExpression = cSharpArgument.Value;

                if (argumentExpression is IThisExpression && data.ThisStatus != null)
                {
                    thisPositions.Add(i);
                    continue;
                }
                var varDecl = TreeNodeHandlerUtil.GetVariableDeclarationForReferenceExpression(argumentExpression);
                if (varDecl != null && data[varDecl] != null) // Т.е. если переменную не рассматриваем.
                    positions[varDecl] = i;
            }
        }
    }
}