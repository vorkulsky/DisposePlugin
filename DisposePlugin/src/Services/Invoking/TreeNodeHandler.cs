using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using DisposePlugin.src.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Services.Invoking
{
    public class TreeNodeHandler : ITreeNodeHandler
    {
        [NotNull]
        private readonly ITypeElement _disposableInterface;

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
        }

        private void ProcessInvocationExpression([NotNull] IInvocationExpression invocationExpression,
            ControlFlowElementData data)
        {
            var qualifierVariableDeclaration = TreeNodeHandlerUtil.GetQualifierVariableDeclaration(invocationExpression);
            if (data[qualifierVariableDeclaration] != null && TreeNodeHandlerUtil.IsSimpleDisposeInvocation(invocationExpression))
            {
                data[qualifierVariableDeclaration] = VariableDisposeStatus.Disposed;
                return;
            }
            ProcessSimpleInvocation(invocationExpression, qualifierVariableDeclaration, data);
        }

        private void ProcessSimpleInvocation([NotNull] IInvocationExpression invocationExpression,
            [CanBeNull] IVariableDeclaration qualifierVariableDeclaration, ControlFlowElementData data)
        {
            var positions = new Dictionary<IVariableDeclaration, byte>();
            byte i = 0;
            foreach (var argument in invocationExpression.InvocationExpressionReference.Invocation.Arguments)
            {
                i++;
                var cSharpArgument = argument as ICSharpArgument;
                if (cSharpArgument == null)
                    continue;
                var argumentExpression = cSharpArgument.Value;
                var varDecl = TreeNodeHandlerUtil.GetVariableDeclarationForExpression(argumentExpression);
                if (varDecl == null) // Если переменную не рассматриваем.
                    continue;
                if (data[varDecl] == null)
                    continue;
                positions.Add(varDecl, i);
            }

            if (!positions.Any() && qualifierVariableDeclaration == null)
                return;

            var referenceExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (referenceExpression == null)
                return;
            var name = referenceExpression.NameIdentifier.Name;
            var offset = invocationExpression.InvokedExpression.GetNavigationRange().TextRange.StartOffset;

            foreach (var position in positions)
            {
                data[position.Key] = VariableDisposeStatus.DependsOnInvocation;
                var invokedMethod = new InvokedMethod(name, offset, position.Value, position.Key.GetSourceFile());
                data.InvokedMethods.Add(position.Key, invokedMethod);
            }
        }
    }
}
