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
        public OneToSetMap<IVariableDeclaration, InvokedExpressionData> InvokedExpressions = new OneToSetMap<IVariableDeclaration, InvokedExpressionData>();
        public VariableDisposeStatus? ThisStatus;
        public HashSet<InvokedExpressionData> ThisInvokedExpressions = new HashSet<InvokedExpressionData>();
        private bool _visited = false;

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
