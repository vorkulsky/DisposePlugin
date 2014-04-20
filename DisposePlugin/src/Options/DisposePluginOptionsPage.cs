using DisposePlugin.resources;
using JetBrains.Application.Settings;
using JetBrains.DataFlow;
using JetBrains.ReSharper.Features.Environment.Options.Inspections;
using JetBrains.UI.Application;
using JetBrains.UI.Options;
using JetBrains.UI.Options.Helpers;

namespace DisposePlugin.Options
{
    [OptionsPage(PID, "Dispose Analysis", null /*Icon*/, ParentId = CodeInspectionPage.PID)]
    public class DisposePluginOptionsPage : AStackPanelOptionsPage
    {
        private readonly Lifetime _lifetime;
        private readonly OptionsSettingsSmartContext _settings;
        private const string PID = "DisposePlugin";

        public DisposePluginOptionsPage(Lifetime lifetime, UIApplication environment,
            OptionsSettingsSmartContext settings)
            : base(lifetime, environment, PID)
        {
            _lifetime = lifetime;
            _settings = settings;
            InitControls();
        }

        private void InitControls()
        {
            Controls.Spin spin;
            Controls.HorzStackPanel stack;

            Controls.Add(new Controls.Label(StringTable.Options_Banner));

            Controls.Add(JetBrains.UI.Options.Helpers.Controls.Separator.DefaultHeight);

            Controls.Add(stack = new Controls.HorzStackPanel(Environment));
            stack.Controls.Add(new Controls.Label(StringTable.Options_MaxLevelLabel));
            stack.Controls.Add(spin = new Controls.Spin());

            spin.Maximum = new decimal(new[] {50, 0, 0, 0});
            spin.Minimum = new decimal(new[] {0, 0, 0, 0});
            spin.Value = new decimal(new[] {1, 0, 0, 0});

            _settings.SetBinding(_lifetime, (DisposePluginSettings s) => s.MaxLevel, spin.IntegerValue);
        }
    }
}