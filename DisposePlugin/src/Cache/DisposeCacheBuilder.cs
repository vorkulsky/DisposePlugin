using System.Collections.Generic;
using JetBrains.Annotations;
using JetBrains.DocumentModel;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.CSharp.Tree;
using JetBrains.ReSharper.Psi.Files;

namespace DisposePlugin.Cache
{
    public static class DisposeCacheBuilder
    {
        [CanBeNull]
        public static IList<DisposeMethodStatus> Build(IPsiSourceFile sourceFile)
        {
            var file = sourceFile.GetPsiFile<CSharpLanguage>(new DocumentRange(sourceFile.Document, 0)) as ICSharpFile;
            if (file == null)
                return null;
            return Build(file);
        }

        [CanBeNull]
        public static IList<DisposeMethodStatus> Build(ICSharpFile file)
        {
            
        }
    }
}
