using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Util;

namespace DisposePlugin.CodeInspections
{
    public enum VariableDisposeStatus
    {
        Disposed,
        NotDisposed,
        Both,
        Unknown
    };

    public class ControlFlowElementData
    {
        private readonly Dictionary<IVariableDeclaration, VariableDisposeStatus> _status = new Dictionary<IVariableDeclaration, VariableDisposeStatus>();
        private bool _visited; // = false

        public Dictionary<IVariableDeclaration, VariableDisposeStatus> Status
        {
            get { return _status; }
        }

        #region Indexers

        // Добавляет или возвращают данные о статусе по имени переменной
        // Если элемент с нужным индексом не сущ., возвращается null
        public VariableDisposeStatus? this[IVariableDeclaration index]
        {
            set
            {
                if (value != null)
                    _status[index] = value.GetValueOrDefault();
            }
            get
            {
                VariableDisposeStatus data;
                _status.TryGetValue(index, out data);
                return data;
            }
        }

        #endregion Indexers

        public ControlFlowElementData Clone()
        {
            var clone = new ControlFlowElementData();
            _status.ForEach(kvp => clone[kvp.Key] = kvp.Value);
            return clone;
        }

        public Boolean IsVisited()
        {
            return _visited;
        }

        public void Visit()
        {
            _visited = true;
        }
    }

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
            set {
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
        // currentElement может быть равен null, если currentElement - вход в граф.
        // Предполагается, что вход в граф только один.
        public Boolean Merge([NotNull] IControlFlowElement previousElement,
            [NotNull] IControlFlowElement currentElement)
        {
            var previousData = this[previousElement];
            Debug.Assert(previousData != null, "previousData != null");
            var currentData = this[currentElement];
            Boolean changesAre;
            var newTargetData = ProcessUnknown(currentData ?? previousData.Clone(), currentElement, out changesAre);
            this[currentElement] = newTargetData;
            return changesAre;
        }

        // Пересчитывает Unknown-данные элемента
        // Возвращает true, если произошли изменения
        private ControlFlowElementData ProcessUnknown([NotNull] ControlFlowElementData data,
            [NotNull] IControlFlowElement element, out Boolean changesAre)
        {
            var previousElems = element.Entries.Select(enterRib => this[enterRib.Source]);
            var resultNameAndStatus = (from kvp in data.Status
                                      where kvp.Value == VariableDisposeStatus.Unknown
                                      select new { Name = kvp.Key, ResultStatus = UpdateStatus(GetPreviousElemsStatusSet(previousElems, kvp)) }).ToList();
            changesAre = resultNameAndStatus.Any(e => e.ResultStatus != VariableDisposeStatus.Unknown);
            resultNameAndStatus.ForEach(e => data[e.Name] = e.ResultStatus);
            return data;
        }

        private JetHashSet<VariableDisposeStatus> GetPreviousElemsStatusSet(IEnumerable<ControlFlowElementData> previousElems,
            KeyValuePair<IVariableDeclaration, VariableDisposeStatus> kvp)
        {
            return previousElems.Select(previousElementData => previousElementData[kvp.Key] ?? VariableDisposeStatus.Unknown).ToHashSet();
        }

        private VariableDisposeStatus UpdateStatus(JetHashSet<VariableDisposeStatus> statusSet)
        {
            var bothSet = new List<VariableDisposeStatus> { VariableDisposeStatus.Disposed, VariableDisposeStatus.NotDisposed };
            if (statusSet.Contains(VariableDisposeStatus.Both) || statusSet.IsSupersetOf(bothSet))
                return VariableDisposeStatus.Both;
            if (statusSet.Contains(VariableDisposeStatus.Unknown))
                return VariableDisposeStatus.Unknown;
            if (statusSet.Contains(VariableDisposeStatus.Disposed))
                return VariableDisposeStatus.Disposed;
            return VariableDisposeStatus.NotDisposed;
        }
    }
}
