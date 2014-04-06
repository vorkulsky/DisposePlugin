using System;
using System.Linq;
using System.Diagnostics;
using DisposePlugin.Cache;
using JetBrains.Annotations;
using System.Collections.Generic;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Util;

namespace DisposePlugin.Services
{
    public class ControlFlowElementDataStorage
    {
        private readonly Dictionary<int, ControlFlowElementData> _elementData = new Dictionary<int, ControlFlowElementData>();

        #region Indexers

        // Добавляют или возвращают данные из хранилища по ControlFlowElement.Id
        // Если элемент с нужным Id не сущ., возвращается null
        [CanBeNull]
        public ControlFlowElementData this[int index]
        {
            set
            {
                if (value != null)
                    _elementData[index] = value;
            }
            get
            {
                ControlFlowElementData data;
                _elementData.TryGetValue(index, out data);
                return data;
            }
        }

        [CanBeNull]
        public ControlFlowElementData this[[CanBeNull] IControlFlowElement element]
        {
            set
            {
                if (element != null)
                    this[element.Id] = value;
            }
            get
            {
                return element != null ? this[element.Id] : null;
            }
        }

        #endregion Indexers

        // Вносит изменения в данные о currentElement согласно информации о previousElement.
        // Предполагается, что вход в граф только один.
        public bool Merge([NotNull] IControlFlowElement previousElement, [NotNull] IControlFlowElement currentElement)
        {
            var previousData = this[previousElement];
            if (previousData == null)
            {
                Debug.Fail("previousData == null");
                return false;
            }
            var currentData = this[currentElement];
            Boolean changesAre;
            var previousElems = currentElement.Entries.Select(enterRib => this[enterRib.Source]).ToArray();
            var newTargetData = ProcessUnknown(currentData ?? previousData.Clone(), previousElems, out changesAre);
            changesAre = changesAre || UpdateInvokedExpressions(newTargetData, previousElems);
            this[currentElement] = newTargetData;
            return changesAre;
        }

        // Пересчитывает Unknown-данные элемента
        // Возвращает true, если произошли изменения
        private ControlFlowElementData ProcessUnknown([NotNull] ControlFlowElementData data,
            IEnumerable<ControlFlowElementData> previousElems, out Boolean changesAre)
        {
            var resultNameAndStatus = (from kvp in data.Status
                                       where kvp.Value == VariableDisposeStatus.Unknown
                                       select new { Name = kvp.Key, ResultStatus = UpdateStatus(GetPreviousElemsStatusSet(previousElems, kvp)) }).ToList();
            changesAre = resultNameAndStatus.Any(e => e.ResultStatus != VariableDisposeStatus.Unknown);
            resultNameAndStatus.ForEach(e => data[e.Name] = e.ResultStatus);
            if (data.ThisStatus == VariableDisposeStatus.Unknown)
                changesAre = changesAre || UpdateThisStatus(previousElems, data);
            return data;
        }

        private JetHashSet<VariableDisposeStatus> GetPreviousElemsStatusSet(IEnumerable<ControlFlowElementData> previousElems,
            KeyValuePair<IVariableDeclaration, VariableDisposeStatus> kvp)
        {
            return previousElems.Select(previousElementData => previousElementData[kvp.Key] ?? VariableDisposeStatus.Unknown).ToHashSet();
        }

        private VariableDisposeStatus UpdateStatus(JetHashSet<VariableDisposeStatus> statusSet)
        {
            if (statusSet.Contains(VariableDisposeStatus.DependsOnInvocation))
                return VariableDisposeStatus.DependsOnInvocation;
            var bothSet = new List<VariableDisposeStatus> { VariableDisposeStatus.Disposed, VariableDisposeStatus.NotDisposed };
            if (statusSet.Contains(VariableDisposeStatus.Both) || statusSet.IsSupersetOf(bothSet))
                return VariableDisposeStatus.Both;
            if (statusSet.Contains(VariableDisposeStatus.Unknown))
                return VariableDisposeStatus.Unknown;
            if (statusSet.Contains(VariableDisposeStatus.Disposed))
                return VariableDisposeStatus.Disposed;
            return VariableDisposeStatus.NotDisposed;
        }

        // Веполняет мердж для переменной this
        // Возвращает true, если произошли изменения
        private bool UpdateThisStatus(IEnumerable<ControlFlowElementData> previousElems, [NotNull] ControlFlowElementData data)
        {
            var statusSet = GetPreviousElemsThisStatusSet(previousElems);
            var resultStatus = UpdateStatus(statusSet);
            if (resultStatus != VariableDisposeStatus.Unknown)
            {
                data.ThisStatus = resultStatus;
                return true;
            }
            return false;
        }

        private JetHashSet<VariableDisposeStatus> GetPreviousElemsThisStatusSet(IEnumerable<ControlFlowElementData> previousElems)
        {
            return previousElems.Select(previousElementData => previousElementData.ThisStatus ?? VariableDisposeStatus.Unknown).ToHashSet();
        }

        private bool UpdateInvokedExpressions([NotNull] ControlFlowElementData data, ICollection<ControlFlowElementData> previousElems)
        {
            var changesAre = false;
            var invokedExpressionsResult = new OneToListMap<IVariableDeclaration, InvokedExpression>();
            foreach (var kvp in data.InvokedExpressions)
            {
                var invokedExpressionSet = GetPreviousElemsInvokedExpressionSet(previousElems, kvp.Key);
                var sizeBefore = kvp.Value.Count;
                invokedExpressionSet.UnionWith(kvp.Value);
                var sizeAfter = invokedExpressionSet.Count;
                if (sizeBefore != sizeAfter) // Количество вызванных методов не может уменьшаться
                    changesAre = true;
                invokedExpressionsResult.AddValueRange(kvp.Key, invokedExpressionSet);
            }
            data.InvokedExpressions = invokedExpressionsResult;

            // Обновляем ThisInvokedExpressions
            if (data.ThisStatus != null)
            {
                var invokedExpressionSet = GetPreviousElemsThisInvokedExpressionSet(previousElems);
                var sizeBefore = data.ThisInvokedExpressions.Count;
                invokedExpressionSet.UnionWith(data.ThisInvokedExpressions);
                var sizeAfter = invokedExpressionSet.Count;
                if (sizeBefore != sizeAfter)
                    changesAre = true;
                data.ThisInvokedExpressions = invokedExpressionSet.ToList();
            }

            return changesAre;
        }

        private JetHashSet<InvokedExpression> GetPreviousElemsInvokedExpressionSet(IEnumerable<ControlFlowElementData> previousElems,
            IVariableDeclaration variable)
        {
            return previousElems.SelectMany(previousElementData => previousElementData.InvokedExpressions)
                .Where(kvp => kvp.Key == variable).SelectMany(kvp => kvp.Value).ToHashSet();
        }

        private JetHashSet<InvokedExpression> GetPreviousElemsThisInvokedExpressionSet(IEnumerable<ControlFlowElementData> previousElems)
        {
            return previousElems.SelectMany(previousElementData => previousElementData.ThisInvokedExpressions).ToHashSet();
        }
    }
}
