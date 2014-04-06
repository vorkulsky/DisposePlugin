﻿using System.Linq;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.src.Util
{
    public static class TreeNodeHandlerUtil
    {
        [CanBeNull]
        public static IVariableDeclaration GetQualifierVariableDeclaration([NotNull] IInvocationExpression invocationExpression)
        {
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return null;
            var qualifierExpression = invokedExpression.QualifierExpression;
            if (qualifierExpression == null || qualifierExpression is IThisExpression)
                return null; //TODO: this
            return GetVariableDeclarationForExpression(qualifierExpression);
        }

        [CanBeNull]
        public static IVariableDeclaration GetVariableDeclarationForExpression([NotNull] ICSharpExpression expression)
        {
            var referenceExpression = expression as IReferenceExpression;
            if (referenceExpression == null)
                return null;
            var variableDeclaration = GetVariableDeclarationByReference(referenceExpression.Reference);
            return variableDeclaration;
        }

        [CanBeNull]
        private static IVariableDeclaration GetVariableDeclarationByReference([NotNull] IReference reference)
        {
            var declaration = GetDeclarationByReference(reference);
            if (declaration == null)
                return null;
            var variableDeclaration = declaration as IVariableDeclaration;
            return variableDeclaration;
        }

        [CanBeNull]
        private static IDeclaration GetDeclarationByReference([NotNull] IReference reference)
        {
            var declaredElement = reference.Resolve().DeclaredElement;
            if (declaredElement == null)
                return null;
            var declaration = declaredElement.GetDeclarations().FirstOrDefault();
            return declaration;
        }

        public static bool IsSimpleDisposeInvocation([NotNull] IInvocationExpression invocationExpression)
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

        public static IDeclaration GetMatchingArgument(ICSharpArgumentInfo argument)
        {
            var matchingParameter = argument.MatchingParameter;
            if (matchingParameter == null)
                return null;
            var declaredElement = matchingParameter.Element as IDeclaredElement;
            var declaration = declaredElement.GetDeclarations().FirstOrDefault();
            return declaration;
        }

        public static int? GetNumberOfParameter([NotNull] IRegularParameterDeclaration parameterDeclaration)
        {
            var parent = parameterDeclaration.Parent;
            var formalParameterList = parent as IFormalParameterList;
            if (formalParameterList == null)
                return null;
            var parameterDeclarations = formalParameterList.ParameterDeclarations;
            var index = parameterDeclarations.Where(p => p == parameterDeclaration).Select((p, i) => i + 1).FirstOrDefault();
            if (index == 0)
                return null;
            return index;
        }

        [CanBeNull]
        public static T GoUpToNodeWithType<T>([NotNull] ITreeNode node) where T : class, ITreeNode
        {
            var typedNode = node as T;
            if (typedNode != null)
                return typedNode;
            var parent = node.Parent;
            if (parent == null)
                return null;
            return GoUpToNodeWithType<T>(parent);
        }
    }
}