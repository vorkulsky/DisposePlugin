using System;
using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using DisposePlugin.Util;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Util;

namespace DisposePlugin.Services.Invoking
{
    public class ControlFlowInspector : Services.ControlFlowInspector
    {
        [CanBeNull] private readonly Dictionary<IRegularParameterDeclaration, byte> _disposableArguments;
        private readonly bool _processThis;
        [NotNull] private readonly ITypeElement _disposableInterface;
        private readonly ControlFlowElementDataStorage _elementDataStorage;
        private readonly IPsiSourceFile _psiSourceFile;

        public ControlFlowInspector([NotNull] ICSharpFunctionDeclaration functionDeclaration,
            [NotNull] CSharpControlFlowGraf graf, [NotNull] ITypeElement disposableInterface)
            : base(functionDeclaration, graf)
        {
            _disposableInterface = disposableInterface;
            _disposableArguments = GetDisposableArguments();
            if (_disposableArguments != null)
            {
                _processThis = IsThisDisposable();
                _elementDataStorage = InitElementDataStorage();
                _psiSourceFile = functionDeclaration.GetSourceFile();
            }
        }

        [CanBeNull]
        public IList<MethodArgumentStatus> GetMethodArgumentStatuses()
        {
            if (_disposableArguments == null)
                return null;
            if (_disposableArguments.Count == 0 && !_processThis)
                return new List<MethodArgumentStatus>();
            var nodeHandlerFactory = new TreeNodeHandlerFactory(_disposableInterface);

            //_disposableArguments и _processThis фактически передаются внутри первичного elementDataStorage
            DoStep(null, Graf.EntryElement, true, nodeHandlerFactory, _elementDataStorage);
            var argumentStatuses = CalculateArgumentStatuses();
            return argumentStatuses;
        }

        [CanBeNull]
        private IList<MethodArgumentStatus> CalculateArgumentStatuses()
        {
            var argumentStatuses = new List<MethodArgumentStatus>();
            var allInvokedExpressions = new OneToSetMap<IVariableDeclaration, InvokedExpressionData>();
            var allStatuses = new OneToSetMap<IVariableDeclaration, VariableDisposeStatus>();
            DoForEachExit(data =>
            {
                data.InvokedExpressions.ForEach(kvp => allInvokedExpressions.AddRange(kvp.Key, kvp.Value));
                data.Status.ForEach(kvp => allStatuses.Add(kvp.Key, kvp.Value));
            });
            var generalStatuses = new Dictionary<IVariableDeclaration, VariableDisposeStatus>();
            allStatuses.ForEach(kvp => generalStatuses[kvp.Key] = GetGeneralStatus(kvp.Value));
            if (_disposableArguments == null)
                return null;
            _disposableArguments.ForEach(
                kvp => argumentStatuses.Add(new MethodArgumentStatus(kvp.Value, generalStatuses[kvp.Key],
                    allInvokedExpressions[kvp.Key].OrderBy(im => im.Offset).ToList(), _psiSourceFile)));

            if (_processThis)
            {
                var allThisInvokedExpressions = new HashSet<InvokedExpressionData>();
                var allThisStatuses = new HashSet<VariableDisposeStatus>();
                DoForEachExit(data =>
                {
                    allThisInvokedExpressions.UnionWith(data.ThisInvokedExpressions);
                    if (data.ThisStatus.HasValue)
                        allThisStatuses.Add(data.ThisStatus.Value);
                });
                argumentStatuses.Add(new MethodArgumentStatus(0, GetGeneralStatus(allThisStatuses),
                    allThisInvokedExpressions.OrderBy(im => im.Offset).ToList(), _psiSourceFile));
            }

            return argumentStatuses.OrderBy(mas => mas.Number).ToList();
        }

        private void DoForEachExit(Action<ControlFlowElementData> action)
        {
            foreach (var exit in Graf.ReachableExits)
            {
                var data = _elementDataStorage[exit.Source];
                if (data != null)
                    action(data);
            }
        }

        //Оптимистичный
        private VariableDisposeStatus GetGeneralStatus(ICollection<VariableDisposeStatus> statusSet)
        {
            if (statusSet.Contains(VariableDisposeStatus.Disposed) || statusSet.Contains(VariableDisposeStatus.Both))
                return VariableDisposeStatus.Disposed;
            if (statusSet.Contains(VariableDisposeStatus.DependsOnInvocation))
                return VariableDisposeStatus.DependsOnInvocation;
            return VariableDisposeStatus.NotDisposed;
        }

        private ControlFlowElementDataStorage InitElementDataStorage()
        {
            var elementDataStorage = new ControlFlowElementDataStorage();
            var initialData = new ControlFlowElementData(Graf.EntryElement.Id);
            if (_disposableArguments != null)
                _disposableArguments.ForEach(kvp => initialData[kvp.Key] = VariableDisposeStatus.NotDisposed);
            if (_processThis)
                initialData.ThisStatus = VariableDisposeStatus.NotDisposed;
            elementDataStorage[Graf.EntryElement] = initialData;
            return elementDataStorage;
        }

        private bool IsThisDisposable()
        {
            var typeDeclaration = FunctionDeclaration.GetContainingTypeDeclaration();
            if (typeDeclaration == null)
                return false;
            return DisposeUtil.HasDisposable(typeDeclaration, _disposableInterface);
        }

        [CanBeNull]
        private Dictionary<IRegularParameterDeclaration, byte> GetDisposableArguments()
        {
            var functionDeclaredElement = FunctionDeclaration.DeclaredElement;
            if (functionDeclaredElement == null)
                return null;
            var allArguments = FunctionDeclaration.DeclaredElement.Parameters;
            var disposableArguments = new Dictionary<IRegularParameterDeclaration, byte>();

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
                    var regularParameterDeclaration = declaration as IRegularParameterDeclaration;
                    if (regularParameterDeclaration == null)
                        continue;
                    disposableArguments[regularParameterDeclaration] = Convert.ToByte(i);
                }
            }
            return disposableArguments;
        }
    }
}