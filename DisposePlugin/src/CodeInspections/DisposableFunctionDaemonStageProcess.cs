using System;
using JetBrains.Application.Progress;
using JetBrains.ReSharper.Daemon;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Files;

namespace DisposePlugin.CodeInspections
{
    public class DisposableFunctionDaemonStageProcess : IDaemonStageProcess
    {
        private readonly IDaemonProcess _daemonProcess;

        public DisposableFunctionDaemonStageProcess(IDaemonProcess daemonProcess)
        {
            _daemonProcess = daemonProcess;
        }

        public void Execute(Action<DaemonStageResult> committer)
        {
            var sourceFile = _daemonProcess.SourceFile;
            var file = sourceFile.GetPsiServices().Files.GetDominantPsiFile<CSharpLanguage>(sourceFile) as ICSharpFile;
            if (file == null)
                return;

            // Running visitor against the PSI
            var elementProcessor = new DisposableFunctionElementProcessor(_daemonProcess);
            file.ProcessDescendants(elementProcessor);

            // Checking if the daemon is interrupted by user activity
            if (_daemonProcess.InterruptFlag)
                throw new ProcessCancelledException();

            // Commit the result into document
            committer(new DaemonStageResult(elementProcessor.Highlightings));
        }

        public IDaemonProcess DaemonProcess
        {
            get { return _daemonProcess; }
        }
    }
}
