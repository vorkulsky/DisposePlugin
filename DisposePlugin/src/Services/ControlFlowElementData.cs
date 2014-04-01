using System;
using System.Collections.Generic;
using DisposePlugin.Cache;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.Util;

namespace DisposePlugin.Services
{
    public enum VariableDisposeStatus
    {
        Disposed,
        NotDisposed,
        Both,
        Unknown,
        DependsOnInvocation, // Используется только при построении кэша, оптимистичный подход
    };

    public class ControlFlowElementData
    {
        private readonly Dictionary<IVariableDeclaration, VariableDisposeStatus> _status = new Dictionary<IVariableDeclaration, VariableDisposeStatus>();
        public OneToListMap<IVariableDeclaration, InvokedMethod> InvokedMethods = new OneToListMap<IVariableDeclaration, InvokedMethod>();
        public VariableDisposeStatus? ThisStatus;
        public IList<InvokedMethod> ThisInvokedMethods = new List<InvokedMethod>();
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
            InvokedMethods.ForEach(kvl => clone.InvokedMethods.AddValueRange(kvl.Key, kvl.Value));
            clone.ThisStatus = ThisStatus;
            ThisInvokedMethods.ForEach(m => clone.ThisInvokedMethods.Add(m));
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
}
