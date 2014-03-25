using JetBrains.ReSharper.Psi.ControlFlow;

namespace DisposePlugin.Services.Invoking
{
    public class ControlFlowElementDataStorage : Services.ControlFlowElementDataStorage
    {
        public override bool Merge(IControlFlowElement previousElement, IControlFlowElement currentElement)
        {
            throw new System.NotImplementedException();
        }
    }
}
