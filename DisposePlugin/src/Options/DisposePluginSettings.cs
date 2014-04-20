using JetBrains.Application.Settings;
using JetBrains.ReSharper.Settings;

namespace DisposePlugin.Options
{
    [SettingsKey(typeof (CodeInspectionSettings), "Dispose")]
    public class DisposePluginSettings
    {
        [SettingsEntry(5, "MaxLevel")] public readonly int MaxLevel;
    }
}