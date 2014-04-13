﻿using System;
using System.Collections.Generic;
using JetBrains.Application.Settings;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.CodeInspections
{
    [DaemonStage]
    public class DisposableFunctionDaemonStage : IDaemonStage
    {
        public IEnumerable<IDaemonStageProcess> CreateProcess(IDaemonProcess process, IContextBoundSettingsStore settings, DaemonProcessKind processKind)
        {
            if (process == null)
                throw new ArgumentNullException("process");
            return new[]
               {
                 new DisposableFunctionDaemonStageProcess(process)
               };
        }

        public ErrorStripeRequest NeedsErrorStripe(IPsiSourceFile sourceFile, IContextBoundSettingsStore settingsStore)
        {
            return ErrorStripeRequest.STRIPE_AND_ERRORS;
        }
    }
}
