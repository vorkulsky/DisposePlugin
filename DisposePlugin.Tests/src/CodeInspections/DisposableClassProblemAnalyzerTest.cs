using JetBrains.ReSharper.Daemon.Test;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using NUnit.Framework;

namespace DisposePlugin.Tests.CodeInspections
{
    public class DisposableClassProblemAnalyzerTest : HighlightingTestBase
    {
        protected override string RelativeTestDataPath
        {
            get { return @"highlighting\class"; }
        }

        protected override PsiLanguageType CompilerIdsLanguage
        {
            get { return CSharpLanguage.Instance; }
        }

        [Test]
        public void Test001()
        {
            DoTestFiles("test001.cs");
        }
    }
}
