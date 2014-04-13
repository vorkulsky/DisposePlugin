using DisposePlugin.Options;
using JetBrains.Application.Settings;
using JetBrains.DataFlow;
using JetBrains.ProjectModel;
using JetBrains.ReSharper.Daemon;

namespace DisposePlugin.CodeInspections
{
    [SolutionComponent]
    public class DisposableFunctionAnalysisInvalidateOnSettingsChange
    {
        public DisposableFunctionAnalysisInvalidateOnSettingsChange(Lifetime lifetime, Daemon daemon,
            ISettingsStore settingsStore)
        {
            var thresholdEntry = settingsStore.Schema.GetScalarEntry((DisposePluginSettings s) => s.MaxLevel);
            settingsStore.AdviseChange(lifetime, thresholdEntry, daemon.Invalidate);
        }
    }
}