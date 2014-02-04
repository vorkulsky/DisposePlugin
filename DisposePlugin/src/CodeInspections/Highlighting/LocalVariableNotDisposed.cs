using JetBrains.ReSharper.Daemon;

namespace DisposePlugin.CodeInspections.Highlighting
{
    [StaticSeverityHighlighting(Severity.WARNING, "CSharpInfo")]
    public class LocalVariableNotDisposed : IHighlighting
    {
        private const string Message = "Local variable is not disposed";

        public string ToolTip
        {
            get { return Message; }
        }

        public string ErrorStripeToolTip
        {
            get { return Message; }
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
