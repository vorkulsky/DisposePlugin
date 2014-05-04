using JetBrains.ReSharper.Daemon;

namespace DisposePlugin.CodeInspections.Highlighting
{
    [StaticSeverityHighlighting(Severity.WARNING, "CSharpInfo")]
    public class LocalVariableNotDisposed : IHighlighting
    {
        private readonly string _message = "Variable probably is not disposed";

        public LocalVariableNotDisposed()
        {
        }

        public LocalVariableNotDisposed(string name)
        {
            _message = "Variable " + name + " probably is not disposed";
        }

        public string ToolTip
        {
            get { return _message; }
        }

        public string ErrorStripeToolTip
        {
            get { return _message; }
        }

        public int NavigationOffsetPatch
        {
            get { return 0; }
        }

        public bool IsValid()
        {
            return true;
        }
    }
}