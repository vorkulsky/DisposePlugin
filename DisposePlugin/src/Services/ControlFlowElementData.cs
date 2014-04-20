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
        private readonly Dictionary<IVariableDeclaration, VariableDisposeStatus> _status =
            new Dictionary<IVariableDeclaration, VariableDisposeStatus>();

        public OneToSetMap<IVariableDeclaration, InvokedExpressionData> InvokedExpressions =
            new OneToSetMap<IVariableDeclaration, InvokedExpressionData>();

        public VariableDisposeStatus? ThisStatus;
        public HashSet<InvokedExpressionData> ThisInvokedExpressions = new HashSet<InvokedExpressionData>();
        // Содержит id вершин графа потока управления, в которые ведет несколько путей, не все из которых пройдены.
        public HashSet<int> Crossroads = new HashSet<int>();
        private bool _visited = false;
        private readonly int _id;

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

        public ControlFlowElementData(int id)
        {
            _id = id;
        }

        public Boolean IsVisited()
        {
            return _visited;
        }

        public void Visit()
        {
            _visited = true;
        }

        public int Id
        {
            get { return _id; }
        }
    }
}