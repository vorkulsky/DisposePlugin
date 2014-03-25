using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;

namespace DisposePlugin.Services.Local
{
    public class ControlFlowElementDataStorage : Services.ControlFlowElementDataStorage
    {
        // Вносит изменения в данные о currentElement согласно информации о previousElement.
        // currentElement может быть равен null, если currentElement - вход в граф.
        // Предполагается, что вход в граф только один.
        public override bool Merge([NotNull] IControlFlowElement previousElement,
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
