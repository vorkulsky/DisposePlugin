using JetBrains.ReSharper.Daemon;

namespace DisposePlugin.CodeInspections.Highlighting
{
    [StaticSeverityHighlighting(Severity.WARNING, "CSharpInfo")]
    public class NotDisposableContainsDisposableField : IHighlighting
    {
        protected readonly string Message;

        public NotDisposableContainsDisposableField(bool implementsDisposableInterface)
        {
            Message = implementsDisposableInterface
                ? "Class contains disposable fields, but not implements dispose method itself"
                : "Class contains disposable fields, but is not disposable itself";
        }

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