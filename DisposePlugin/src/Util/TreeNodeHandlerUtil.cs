using System.Linq;
using DisposePlugin.Services;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Resolve;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.Util
{
    public static class TreeNodeHandlerUtil
    {
        [CanBeNull]
        public static IVariableDeclaration GetQualifierVariableDeclaration(
            [NotNull] IReferenceExpression invokedExpression)
        {
            var qualifierExpression = invokedExpression.QualifierExpression;
            if (qualifierExpression == null || qualifierExpression is IThisExpression)
                return null;
            return GetVariableDeclarationForReferenceExpression(qualifierExpression);
        }

        public static bool IsInvocationOnDisposableThis([NotNull] IReferenceExpression invokedExpression,
            [NotNull] ITypeElement disposableInterface)
        {
            var qualifierExpression = invokedExpression.QualifierExpression;
            if (qualifierExpression == null || qualifierExpression is IThisExpression)
                return IsContainingTypeDisposable(invokedExpression, disposableInterface);
            return false;
        }

        public static bool IsContainingTypeDisposable([NotNull] ICSharpTreeNode node,
            [NotNull] ITypeElement disposableInterface)
        {
            var containingTypeDeclaration = node.GetContainingTypeDeclaration();
            var declaredElement = containingTypeDeclaration.DeclaredElement;
            if (declaredElement == null)
                return false;
            if (DisposeUtil.HasDisposable(declaredElement, disposableInterface))
                return true;
            return false;
        }

        [CanBeNull]
        public static IVariableDeclaration GetVariableDeclarationForReferenceExpression(
            [NotNull] ICSharpExpression expression)
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

        public static bool IsCloseInvocation([NotNull] IInvocationExpression invocationExpression)
        {
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return false;
            var nameIdentifier = invokedExpression.NameIdentifier;
            if (nameIdentifier == null)
                return false;
            var name = nameIdentifier.Name;
            if (!name.Equals("Close"))
                return false;
            return true;
        }

        public static bool IsSimpleDisposeInvocation([NotNull] IInvocationExpression invocationExpression)
        {
            if (invocationExpression.Arguments.Count != 0)
                return false;
            var invokedExpression = invocationExpression.InvokedExpression as IReferenceExpression;
            if (invokedExpression == null)
                return false;
            var nameIdentifier = invokedExpression.NameIdentifier;
            if (nameIdentifier == null)
                return false;
            var name = nameIdentifier.Name;
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
            var index = 0;
            for (var i = 0; i < parameterDeclarations.Count; i++)
            {
                if (parameterDeclarations[i] != parameterDeclaration)
                    continue;
                index = i + 1;
                break;
            }
            if (index == 0)
                return null;
            return index;
        }

        public static bool CheckOnDisposeInvocation(IInvocationExpression invocationExpression,
            ControlFlowElementData data,
            bool isInvocationOnDisposableThis, IVariableDeclaration qualifierDisposableVariableDeclaration)
        {
            if (isInvocationOnDisposableThis)
            {
                if (IsSimpleDisposeInvocation(invocationExpression) || IsCloseInvocation(invocationExpression))
                {
                    data.ThisStatus = VariableDisposeStatus.Disposed;
                    return true;
                }
            }
            else
            {
                if (qualifierDisposableVariableDeclaration != null &&
                    (IsSimpleDisposeInvocation(invocationExpression) || IsCloseInvocation(invocationExpression)))
                {
                    data[qualifierDisposableVariableDeclaration] = VariableDisposeStatus.Disposed;
                    return true;
                }
            }
            return false;
        }

        public static void ProcessUsingStatement([NotNull] IUsingStatement usingStatement, ControlFlowElementData data)
        {
            var expressions = usingStatement.Expressions;
            foreach (var expression in expressions)
            {
                var referenceExpression = expression as IReferenceExpression;
                if (referenceExpression != null)
                {
                    ProcessReferenceExpressionInUsingStatement(referenceExpression, data);
                    continue;
                }
                var thisExpression = expression as IThisExpression;
                if (thisExpression != null)
                {
                    ProcessThisExpressionInUsingStatement(data);
                }
            }
        }

        public static void ProcessReferenceExpressionInUsingStatement([NotNull] IReferenceExpression referenceExpression,
            ControlFlowElementData data)
        {
            var declaration = GetVariableDeclarationForReferenceExpression(referenceExpression);
            if (declaration == null)
                return;
            var variableData = data[declaration];
            if (variableData == null)
                return;
            data[declaration] = VariableDisposeStatus.Disposed;
        }

        public static void ProcessThisExpressionInUsingStatement(ControlFlowElementData data)
        {
            if (data.ThisStatus == null)
                return;
            data.ThisStatus = VariableDisposeStatus.Disposed;
        }

        // Работает только с простейшими присваиваниями.
        public static void ProcessAssignmentExpression([NotNull] IAssignmentExpression assignmentExpression,
            ControlFlowElementData data)
        {
            var destReferenceExpression = assignmentExpression.Dest as IReferenceExpression;
            var sourceReferenceExpression = assignmentExpression.Source as IReferenceExpression;
            var sourceThisExpression = assignmentExpression.Source as IThisExpression;
            if (destReferenceExpression == null || (sourceReferenceExpression == null && sourceThisExpression == null))
                return;
            if (sourceThisExpression != null)
            {
                // Не учитываем операцию присваивания this.
                //ProcessThisAssignmentExpression(destReferenceExpression, data);
                return;
            }
            var destDeclaration = GetVariableDeclarationForReferenceExpression(destReferenceExpression);
            var sourceDeclaration = GetVariableDeclarationForReferenceExpression(sourceReferenceExpression);
            if (destDeclaration == null || sourceDeclaration == null)
                return;
            var destIsFieldDeclaration = destDeclaration is IFieldDeclaration;
            var destIsParameterDeclaration = destDeclaration is IParameterDeclaration;
            var destIsLocalVariableDeclaration = destDeclaration is ILocalVariableDeclaration;
            // В том числе отсекает случай sourceIsFieldDeclaration и случай sourceIsParameterDeclaration,
            // когда за переметрами не следим.
            var sourceStatus = data[sourceDeclaration];
            if (sourceStatus == null || destIsParameterDeclaration)
                return;
            if (destIsFieldDeclaration)
            {
                data[sourceDeclaration] = VariableDisposeStatus.Disposed;
                return;
            }
            // Не учитываем присваивания в аргументы функции
            if (!destIsLocalVariableDeclaration)
                return;
            // Ничего не меняет присвоение своему себе или синониму
            if (destDeclaration == sourceDeclaration ||
                (data.Synonyms.ContainsKey(destDeclaration) && data.Synonyms[destDeclaration].Contains(sourceDeclaration)))
                return;
            // В режиме Invoking значение имеет только статус аргументов функции, что при этом будет содержаться в данных
            // о локальных переменных не существенно
            // Не сообщаем о необходимости задиспозить переменную при присваивании в нее.
            // Удаляем dest из ее синонимов.
            if (data.Synonyms.ContainsKey(destDeclaration))
                foreach (var synonym in data.Synonyms[destDeclaration])
                    data.Synonyms[synonym].Remove(synonym);
            // Перезаписать информацию о dest информацией о source, и установить их синонимами друг другу
            data[destDeclaration] = data[sourceDeclaration];
            if (data.InvokedExpressions.ContainsKey(destDeclaration))
                data.InvokedExpressions.RemoveKey(destDeclaration);
            if (data.InvokedExpressions.ContainsKey(sourceDeclaration))
                data.InvokedExpressions.AddRange(sourceDeclaration, data.InvokedExpressions[sourceDeclaration]);
            if (data.Synonyms.ContainsKey(destDeclaration))
                data.Synonyms.RemoveKey(destDeclaration);
            if (data.Synonyms.ContainsKey(sourceDeclaration))
                data.Synonyms.AddRange(sourceDeclaration, data.Synonyms[sourceDeclaration]);
            data.Synonyms.Add(destDeclaration, sourceDeclaration);
            data.Synonyms.Add(sourceDeclaration, destDeclaration);
        }

        /*public static void ProcessThisAssignmentExpression([NotNull] IReferenceExpression destReferenceExpression,
            ControlFlowElementData data)
        {
            if (data.ThisStatus == null)
                return;
            var destDeclaration = GetVariableDeclarationForReferenceExpression(destReferenceExpression);
            if (destDeclaration == null)
                return;
            var destIsFieldDeclaration = destDeclaration is IFieldDeclaration;
            if (destIsFieldDeclaration)
                data.ThisStatus = VariableDisposeStatus.Disposed;
            //TODO
        }*/
    }
}