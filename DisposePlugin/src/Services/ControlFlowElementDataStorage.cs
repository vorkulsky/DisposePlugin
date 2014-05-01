using System.Collections.Generic;
using System.Linq;
using DisposePlugin.Cache;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Util;

namespace DisposePlugin.Services
{
    public class ControlFlowElementDataStorage
    {
        private readonly Dictionary<int, ControlFlowElementData> _elementData =
            new Dictionary<int, ControlFlowElementData>();

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
            get { return element != null ? this[element.Id] : null; }
        }

        #endregion Indexers

        // Вносит изменения в данные о currentElement согласно информации о previousElement.
        // Предполагается, что вход в граф только один.
        public bool Merge([NotNull] IControlFlowElement previousElement, [NotNull] IControlFlowElement currentElement)
        {
            var previousData = this[previousElement];
            if (previousData == null)
            {
                Assertion.Fail("previousData == null");
                return false;
            }
            var currentData = this[currentElement];
            var changesAre = false;
            var previousElems = currentElement.Entries.Where(enterRib => enterRib.Source != null && enterRib.Source.IsReachable)
                .Select(enterRib => this[enterRib.Source]).ToArray();
            var newTargetData = Update(currentData ?? new ControlFlowElementData(currentElement.Id), previousElems,
                ref changesAre);
            this[currentElement] = newTargetData;
            return changesAre;
        }

        // Пересчитывает данные элемента
        // Возвращает true, если найдены любые изменения
        // previousElems должен содержать null для элементов, у которых нет еще СontrolFlowElementData
        private ControlFlowElementData Update([NotNull] ControlFlowElementData data,
            ICollection<ControlFlowElementData> previousElems, ref bool changesAre)
        {
            var hasCrossroads = UpdateCrossroads(data, previousElems, ref changesAre);
            var previousElemsStatusSetsDictionary = GetPreviousElemsStatusSetsList(previousElems);
            var previousElemsUnionStatusDictionary = UniteStatuses(previousElemsStatusSetsDictionary, hasCrossroads);
            var resultStatusDictionary = CombinePreviousAndCurrent(previousElemsUnionStatusDictionary, data);
            var invokedExpressions = GetInvokedExpressions(previousElems, resultStatusDictionary,
                data.InvokedExpressions);
            Apply(data, ref changesAre, resultStatusDictionary, invokedExpressions);
            var changesAreThis = UpdateThisStatus(previousElems, data, hasCrossroads);
            changesAre = changesAre || changesAreThis;
            return data;
        }

        #region Crossroads

        // Добавляет текущий элемент в список его перекрестков, если в него можно перейти из хотя бы одного предыдущего элемента,
        // который еще не посещался.
        // Убирает текущий элемент из списка его перекрестков, если если все его предыдущие элементы посещались.
        // При любом изменении устанавливает changesAre в true.
        // Возвращает, завивисит ли элемент хотя бы от одного перекрестка. Т.е. существует ли путь в текущий элемент, по которому
        // мы еще не успели пройти.
        private bool UpdateCrossroads(ControlFlowElementData data, ICollection<ControlFlowElementData> previousElems,
            ref bool changesAre)
        {
            data.Crossroads = GetPreviousCrossroads(previousElems);
            var isCrossroad = IsCrossroad(previousElems);
            if (isCrossroad)
            {
                if (!data.Crossroads.Contains(data.Id))
                {
                    changesAre = true;
                    data.Crossroads.Add(data.Id);
                }
            }
            else
            {
                if (data.Crossroads.Contains(data.Id))
                {
                    changesAre = true;
                    data.Crossroads.Remove(data.Id);
                }
            }
            var hasCrossroads = data.Crossroads.Any();
            return hasCrossroads;
        }

        // Возвращает список перекрестков всех предыдущих элементов.
        private HashSet<int> GetPreviousCrossroads(IEnumerable<ControlFlowElementData> previousElems)
        {
            var result = new HashSet<int>();
            foreach (var data in previousElems)
                if (data != null && data.IsVisited())
                    result.AddRange(data.Crossroads);
            return result;
        }

        // Проверяет, является ли текущий элемент перекрестком для самого себя, т.е. посещались ли все его предыдущие элементы.
        private bool IsCrossroad(IEnumerable<ControlFlowElementData> previousElems)
        {
            return (previousElems.Count(e => e == null || !e.IsVisited()) > 0);
        }

        #endregion Crossroads

        // Применяем результат к data и заодно проверяем были ли изменения
        private static void Apply(ControlFlowElementData data, ref bool changesAre,
            IDictionary<IVariableDeclaration, VariableDisposeStatus> resultStatusDictionary,
            OneToSetMap<IVariableDeclaration, InvokedExpressionData> invokedExpressions)
        {
            foreach (var resultStatus in resultStatusDictionary)
            {
                var status = data[resultStatus.Key];
                if (status != resultStatus.Value)
                {
                    changesAre = true;
                    data[resultStatus.Key] = resultStatus.Value;
                }
            }
            changesAre = changesAre || invokedExpressions.Keys.Except(data.InvokedExpressions.Keys).Any();
                // или вычисляется лениво
            data.InvokedExpressions = invokedExpressions;
        }

        #region status

        // Возвращает множество существующих статусов для каждой переменной, определенной хотя бы в одном прердыдущей элементе.
        // Если хотя бы один предыдущий элемент еще не посещался, то множество статусов для всех переменных будет содержать
        // еще и статус Unknown.
        // previousElems должен содержать null для элементов, у которых нет еще СontrolFlowElementData или которые не посещались.
        private IDictionary<IVariableDeclaration, JetHashSet<VariableDisposeStatus>> GetPreviousElemsStatusSetsList
            (ICollection<ControlFlowElementData> previousElems)
        {
            var previousWithoutNotVisitedElements = previousElems.Where(e => e != null && e.IsVisited()).ToList();
            var result = new Dictionary<IVariableDeclaration, JetHashSet<VariableDisposeStatus>>();
            foreach (var status in previousWithoutNotVisitedElements.SelectMany(data => data.Status))
            {
                JetHashSet<VariableDisposeStatus> statusSet;
                var hasValue = result.TryGetValue(status.Key, out statusSet);
                if (hasValue)
                    statusSet.Add(status.Value);
                else
                {
                    statusSet = new JetHashSet<VariableDisposeStatus> {status.Value};
                    result[status.Key] = statusSet;
                }
            }
            var hasNotVisitedElements = previousElems.Count != previousWithoutNotVisitedElements.Count;
            if (hasNotVisitedElements)
                result.ForEach(kvp => kvp.Value.Add(VariableDisposeStatus.Unknown));
            return result;
        }

        // Изменяет входную переменную previousElemsStatusSetsDictionary в соответствии с статусами, точно известными для
        // текущего элемента
        private IDictionary<IVariableDeclaration, VariableDisposeStatus> CombinePreviousAndCurrent
            (IDictionary<IVariableDeclaration, VariableDisposeStatus> previousElemsStatusSetsDictionary,
                ControlFlowElementData currentElemData)
        {
            var result = new Dictionary<IVariableDeclaration, VariableDisposeStatus>(previousElemsStatusSetsDictionary);
            if (currentElemData == null || !currentElemData.IsVisited())
                return result;
            var currentElementStatus = currentElemData.Status;
            foreach (var status in currentElementStatus)
            {
                VariableDisposeStatus previousStatus;
                var hasValue = previousElemsStatusSetsDictionary.TryGetValue(status.Key, out previousStatus);
                var resultStatus = hasValue ? CombinePreviousAndCurrent(previousStatus, status.Value) : status.Value;
                result[status.Key] = resultStatus;
            }
            return result;
        }

        // Вычисляет статус на основе предыдущего объединенного статуса и текущего статуса.
        private VariableDisposeStatus CombinePreviousAndCurrent(VariableDisposeStatus previousStatus,
            VariableDisposeStatus status)
        {
            switch (status)
            {
                case VariableDisposeStatus.Disposed:
                    return VariableDisposeStatus.Disposed;
                case VariableDisposeStatus.NotDisposed:
                    return VariableDisposeStatus.NotDisposed;
                case VariableDisposeStatus.Both:
                    return VariableDisposeStatus.Both;
                case VariableDisposeStatus.Unknown:
                    return previousStatus;
                case VariableDisposeStatus.DependsOnInvocation:
                    switch (previousStatus)
                    {
                        case VariableDisposeStatus.Disposed:
                            return VariableDisposeStatus.Disposed;
                        case VariableDisposeStatus.Both:
                            return VariableDisposeStatus.Both;
                        default:
                            return VariableDisposeStatus.DependsOnInvocation;
                    }
                default:
                    Assertion.Fail("Unaccounted VariableDisposeStatus");
                    return VariableDisposeStatus.Unknown;
            }
        }

        // Для каждой переменной на основе множества статусов вычисляет один.
        // (Для группы равноправных элементов, например, всех предыдущих элементов)
        private IDictionary<IVariableDeclaration, VariableDisposeStatus> UniteStatuses
            (IDictionary<IVariableDeclaration, JetHashSet<VariableDisposeStatus>> statusSetsDictionary,
                bool hasCrossroads)
        {
            var result = new Dictionary<IVariableDeclaration, VariableDisposeStatus>();
            foreach (var statusSet in statusSetsDictionary)
            {
                var uniteStatus = UniteStatus(statusSet.Value, hasCrossroads);
                result[statusSet.Key] = uniteStatus;
            }
            return result;
        }

        //На основе множества статусов вычисляет один.
        // (Для группы равноправных элементов, например, всех предыдущих элементов)
        private VariableDisposeStatus UniteStatus(JetHashSet<VariableDisposeStatus> statusSet, bool hasCrossroads)
        {
            var disposedAndInvocationSet = new List<VariableDisposeStatus>
            {
                VariableDisposeStatus.Disposed,
                VariableDisposeStatus.DependsOnInvocation
            };
            if (statusSet.IsSupersetOf(disposedAndInvocationSet))
                return VariableDisposeStatus.Disposed;
            if (statusSet.Contains(VariableDisposeStatus.DependsOnInvocation))
                return VariableDisposeStatus.DependsOnInvocation;
            var bothSet = new List<VariableDisposeStatus>
            {
                VariableDisposeStatus.Disposed,
                VariableDisposeStatus.NotDisposed
            };
            if (statusSet.Contains(VariableDisposeStatus.Both) || statusSet.IsSupersetOf(bothSet))
                return VariableDisposeStatus.Both;
            if (!hasCrossroads)
            {
                if (statusSet.Contains(VariableDisposeStatus.Disposed))
                    return VariableDisposeStatus.Disposed;
                if (statusSet.Contains(VariableDisposeStatus.NotDisposed))
                    return VariableDisposeStatus.NotDisposed;
                Assertion.Fail("Unknown status");
                return VariableDisposeStatus.Unknown;
            }
            if (statusSet.Contains(VariableDisposeStatus.Unknown))
                return VariableDisposeStatus.Unknown;
            if (statusSet.Contains(VariableDisposeStatus.Disposed))
                return VariableDisposeStatus.Disposed;
            return VariableDisposeStatus.NotDisposed;
        }

        #endregion status

        #region invokedExpressions

        // Обновляет список вызванных методов для переменных со статусом DependsOnInvocation.
        // Удаляет список вызванных методов для переменных, лешившихся этого статуса.
        private OneToSetMap<IVariableDeclaration, InvokedExpressionData> GetInvokedExpressions
            (ICollection<ControlFlowElementData> previousElems,
                IDictionary<IVariableDeclaration, VariableDisposeStatus> statusDictionary,
                OneToSetMap<IVariableDeclaration, InvokedExpressionData> invokedExpressions)
        {
            var result = new OneToSetMap<IVariableDeclaration, InvokedExpressionData>(invokedExpressions);
            foreach (var status in statusDictionary)
            {
                if (status.Value != VariableDisposeStatus.DependsOnInvocation)
                {
                    if (invokedExpressions.ContainsKey(status.Key))
                        result.RemoveKey(status.Key);
                    continue;
                }
                foreach (var previousElem in previousElems)
                {
                    if (previousElem == null || !previousElem.IsVisited())
                        continue;
                    var previousStatus = previousElem[status.Key];
                    if (previousStatus == null)
                        continue;
                    if (previousStatus != VariableDisposeStatus.DependsOnInvocation)
                        continue;
                    if (previousElem.InvokedExpressions.ContainsKey(status.Key))
                        result.AddRange(status.Key, previousElem.InvokedExpressions[status.Key]);
                }
            }
            return result;
        }

        #endregion invokedExpressions

        #region this

        // Веполняет мердж для переменной this.
        // Возвращает true, если произошли изменения.
        // Cразу применяет все действия.
        // Возвращает были ли изменения.
        private bool UpdateThisStatus(ICollection<ControlFlowElementData> previousElems,
            [NotNull] ControlFlowElementData data, bool hasCrossroads)
        {
            var statusSet = GetPreviousElemsThisStatusSet(previousElems);
            if (statusSet == null)
                return false;
            var resultStatus = UniteStatus(statusSet, hasCrossroads);
            var combinedStatus = data.ThisStatus != null
                ? CombinePreviousAndCurrent(resultStatus, data.ThisStatus.Value)
                : resultStatus;
            var changesAre = UpdateInvokedExpressionsForThis(previousElems, data, combinedStatus);
            if (combinedStatus != data.ThisStatus)
            {
                data.ThisStatus = combinedStatus;
                changesAre = true;
            }
            return changesAre;
        }

        // Возвращает множество статусов предыдущих переменной this для предыдущих элементов.
        // Если предыдущий элемент не был посещен или не содержит данных, в множество добавляется VariableDisposeStatus.Unknown.
        // Если хотя бы один предыдущий элемент был посещен и содержит данные, но не содержит статуса для this, возвращается null.
        [CanBeNull]
        private JetHashSet<VariableDisposeStatus> GetPreviousElemsThisStatusSet(
            IEnumerable<ControlFlowElementData> previousElems)
        {
            var result = new JetHashSet<VariableDisposeStatus>();
            foreach (var previousElementData in previousElems)
            {
                if (previousElementData == null || !previousElementData.IsVisited())
                    result.Add(VariableDisposeStatus.Unknown);
                else
                {
                    if (previousElementData.ThisStatus == null)
                        return null;
                    result.Add(previousElementData.ThisStatus.Value);
                }
            }
            return result;
        }

        // Обновляет список вызванных методов для this, если это имеет статус DependsOnInvocation.
        // Иначе удаляет список вызванных методов.
        // Cразу применяет все действия.
        // Возвращает были ли изменения.
        private bool UpdateInvokedExpressionsForThis(ICollection<ControlFlowElementData> previousElems,
            ControlFlowElementData data, VariableDisposeStatus resultStatus)
        {
            if (resultStatus != VariableDisposeStatus.DependsOnInvocation)
            {
                if (data.ThisInvokedExpressions.Any())
                {
                    data.ThisInvokedExpressions.Clear();
                    return true;
                }
            }
            var result = new HashSet<InvokedExpressionData>(data.ThisInvokedExpressions);
            foreach (var previousElem in previousElems)
            {
                if (previousElem == null || !previousElem.IsVisited())
                    continue;
                var previousStatus = previousElem.ThisStatus;
                if (previousStatus == null)
                    continue;
                if (previousStatus != VariableDisposeStatus.DependsOnInvocation)
                    continue;
                result.AddRange(previousElem.ThisInvokedExpressions);
            }
            var changesAre = result.Except(data.ThisInvokedExpressions).Any(); // или вычисляется лениво
            data.ThisInvokedExpressions = result;
            return changesAre;
        }

        #endregion this
    }
}