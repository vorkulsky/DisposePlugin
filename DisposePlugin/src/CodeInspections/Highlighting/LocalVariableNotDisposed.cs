using JetBrains.ReSharper.Daemon;

namespace DisposePlugin.CodeInspections.Highlighting
{
    [StaticSeverityHighlighting(Severity.WARNING, "CSharpInfo")]
    public class LocalVariableNotDisposed : IHighlighting
    {
        private readonly string _message = "Local variable is not disposed";

        public LocalVariableNotDisposed()
        {
            
        }

        public LocalVariableNotDisposed(string message)
        {
            _message = message;
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
