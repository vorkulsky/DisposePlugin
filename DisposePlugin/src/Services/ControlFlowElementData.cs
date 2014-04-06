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
        public OneToListMap<IVariableDeclaration, InvokedExpression> InvokedExpressions = new OneToListMap<IVariableDeclaration, InvokedExpression>();
        public VariableDisposeStatus? ThisStatus;
        public IList<InvokedExpression> ThisInvokedExpressions = new List<InvokedExpression>();
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
                if (index == null)
                    return null;
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
            InvokedExpressions.ForEach(kvl => clone.InvokedExpressions.AddValueRange(kvl.Key, kvl.Value));
            clone.ThisStatus = ThisStatus;
            ThisInvokedExpressions.ForEach(m => clone.ThisInvokedExpressions.Add(m));
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
