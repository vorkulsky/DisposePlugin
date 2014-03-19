using System.Collections.Generic;
using System.Linq;
using DisposePlugin.CodeInspections;
using JetBrains.Annotations;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp.Impl.ControlFlow;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Search;
using JetBrains.ReSharper.Psi.Tree;

namespace DisposePlugin.src.CodeInspections
{
    public class SimpleInspector
    {
        [NotNull] private readonly CSharpControlFlowGraf _graf;
        [NotNull] private readonly ITypeElement _disposableInterface;
        [NotNull] private readonly Dictionary<IVariableDeclaration, IVariableDeclaration> _connections;
        [CanBeNull] private readonly IVariableDeclaration _qualifierVariableDeclaration;
        private readonly int _level;
        public readonly Dictionary<IVariableDeclaration, VariableDisposeStatus> DisposeStatus = new Dictionary<IVariableDeclaration, VariableDisposeStatus>();

        public SimpleInspector(Dictionary<IVariableDeclaration, IVariableDeclaration> connections,
            [CanBeNull] IVariableDeclaration qualifierVariableDeclaration, int level, [NotNull] CSharpControlFlowGraf graf,
            ITypeElement disposableInterface)
        {
            _graf = graf;
            _connections = connections;
            _qualifierVariableDeclaration = qualifierVariableDeclaration;
            _disposableInterface = disposableInterface;
           _level = level;
           Inspect();
        }

        private void Inspect()
        {
            foreach (var variable in _connections.Keys)
            {
                DisposeStatus[variable] = VariableDisposeStatus.Unknown;
                RunAnalysis(variable);
            }
        }

        private void RunAnalysis(IVariableDeclaration variable)
        {
            var decl = variable.DeclaredElement;
            if (decl == null)
                return;
            var allReferences = decl.GetPsiServices().Finder.FindAllReferences(variable.DeclaredElement);
            var usages = allReferences.Select(reference => reference.GetTreeNode()).ToList();
            foreach (var node in usages)
            {
                AnalyseUsage(variable, node);
                if (DisposeStatus[variable] == VariableDisposeStatus.Disposed)
                    return;
            }
        }

        private void AnalyseUsage(IVariableDeclaration variable, ITreeNode node)
        {
            var handler = new TreeNodeHandler(_level, _disposableInterface);
            var data = new ControlFlowElementData();
            data[variable] = VariableDisposeStatus.NotDisposed;
            handler.ProcessTreeNode(node, data);
            if (data[variable] == VariableDisposeStatus.Disposed)
            {
                DisposeStatus[variable] = VariableDisposeStatus.Disposed;
                return;
            }
            if (node.Parent != null)
                AnalyseUsage(variable, node);
        }
    }
}
