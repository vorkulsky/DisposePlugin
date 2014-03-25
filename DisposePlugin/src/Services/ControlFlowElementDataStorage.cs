using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi.ControlFlow;

namespace DisposePlugin.Services
{
    public abstract class ControlFlowElementDataStorage
    {
        protected readonly Dictionary<int, ControlFlowElementData> _elementData = new Dictionary<int, ControlFlowElementData>();

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

        public abstract bool Merge(IControlFlowElement previousElement, IControlFlowElement currentElement);
    }
}
