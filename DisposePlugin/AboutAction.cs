using System.Windows.Forms;
using JetBrains.ActionManagement;
using JetBrains.Application.DataContext;

namespace DisposePlugin
{
    [ActionHandler("DisposePlugin.About")]
    public class AboutAction : IActionHandler
    {
        public bool Update(IDataContext context, ActionPresentation presentation, DelegateUpdate nextUpdate)
        {
            // return true or false to enable/disable this action
            return true;
        }

        public void Execute(IDataContext context, DelegateExecute nextExecute)
        {
            MessageBox.Show(
              "DisposePlugin\nAnton Fedorov (vorkulsky@gmail.com)\n\nDisposePlugin",
              "About DisposePlugin",
              MessageBoxButtons.OK,
              MessageBoxIcon.Information);
        }
    }
}