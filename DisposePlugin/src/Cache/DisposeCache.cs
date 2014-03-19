using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JetBrains.Application;
using JetBrains.Application.Progress;
using JetBrains.DataFlow;
using JetBrains.DocumentManagers.impl;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Caches;
using JetBrains.ReSharper.Psi.CSharp;
using JetBrains.ReSharper.Psi.Tree;
using JetBrains.ReSharper.Psi.Util.Caches;
using JetBrains.Util;

namespace DisposePlugin.Cache
{
    [PsiComponent]
    public class DisposeCache : ICache
    {
        private const int VERSION = 1;
        private readonly JetHashSet<IPsiSourceFile> myDirtyFiles = new JetHashSet<IPsiSourceFile>();
        private readonly OneToListMap<IPsiSourceFile, DisposeMethodStatus> mySourceFileToDisposeStatus
            = new OneToListMap<IPsiSourceFile, DisposeMethodStatus>();
        private readonly IPsiConfiguration myPsiConfiguration;
        private readonly IShellLocks myShellLocks;
        private readonly IPersistentIndexManager myPersistentIdIndex;
        private DisposePersistentCache<DisposeCacheData> myPersistentCache;

        public DisposeCache(Lifetime lifetime, IShellLocks shellLocks,
            IPsiConfiguration psiConfiguration, IPersistentIndexManager persistentIdIndex)
        {
            myPsiConfiguration = psiConfiguration;
            myPersistentIdIndex = persistentIdIndex;
            myShellLocks = shellLocks;
        }

        public void MarkAsDirty(IPsiSourceFile sourceFile)
        {
            if (Accepts(sourceFile))
            {
                myDirtyFiles.Add(sourceFile);
            }
        }

        public object Load(IProgressIndicator progress, bool enablePersistence)
        {
            if (!enablePersistence)
            {
                return null;
            }

            Assertion.Assert(myPersistentCache == null, "myPersistentCache == null");

            using (ReadLockCookie.Create())
            {
                myPersistentCache = new DisposePersistentCache<DisposeCacheData>(myShellLocks, VERSION, "DisposeCache", myPsiConfiguration);
            }

            var data = new Dictionary<IPsiSourceFile, DisposeCacheData>();

            if (myPersistentCache.Load(progress, myPersistentIdIndex,
                (file, reader) =>
                {
                    using (ReadLockCookie.Create())
                    {
                        return DisposeCacheSerializer.Read(reader, file);
                    }
                },
                (projectFile, psiSymbols) =>
                {
                    if (projectFile != null)
                    {
                        data[projectFile] = psiSymbols;
                    }
                }) != LoadResult.OK)

                return data;
            return null;
        }

        public void MergeLoaded(object data)
        {
            var parts = (Dictionary<IPsiSourceFile, DisposeCacheData>)data;
            foreach (var pair in parts)
            {
                if (pair.Key.IsValid() && !myDirtyFiles.Contains(pair.Key))
                {
                    ((ICache)this).Merge(pair.Key, pair.Value);
                }
            }
        }

        public void Save(IProgressIndicator progress, bool enablePersistence)
        {
            if (!enablePersistence)
                return;

            Assertion.Assert(myPersistentCache != null, "myPersistentCache != null");
            myPersistentCache.Save(progress, myPersistentIdIndex, (writer, file, data) =>
            DisposeCacheSerializer.Write(data, writer));
            myPersistentCache.Dispose();
            myPersistentCache = null;
        }

        public bool UpToDate(IPsiSourceFile sourceFile)
        {
            myShellLocks.AssertReadAccessAllowed();

            if (!Accepts(sourceFile))
                return true;

            return !myDirtyFiles.Contains(sourceFile) && mySourceFileToDisposeStatus.ContainsKey(sourceFile);
        }

        public object Build(IPsiSourceFile sourceFile, bool isStartup)
        {
            return DisposeCacheBuilder.Build(sourceFile);
        }

        public void Merge(IPsiSourceFile sourceFile, object builtPart)
        {
            myShellLocks.AssertWriteAccessAllowed();

            mySourceFileToDisposeStatus.RemoveKey(sourceFile);

            var data = builtPart as IList<DisposeMethodStatus>;
            if (data != null)
                mySourceFileToDisposeStatus.AddValueRange(sourceFile, data);

            myDirtyFiles.Remove(sourceFile);
        }

        public void Drop(IPsiSourceFile sourceFile)
        {
            myShellLocks.AssertWriteAccessAllowed();

            mySourceFileToDisposeStatus.RemoveKey(sourceFile);

            myDirtyFiles.Add(sourceFile);
        }

        public void OnPsiChange(ITreeNode elementContainingChanges, PsiChangedElementType type)
        {
            if (elementContainingChanges == null)
                return;
            myShellLocks.AssertWriteAccessAllowed();
            var projectFile = elementContainingChanges.GetSourceFile();
            if (projectFile != null && Accepts(projectFile))
                myDirtyFiles.Add(projectFile);
        }

        public void OnDocumentChange(IPsiSourceFile sourceFile, ProjectFileDocumentCopyChange change)
        {
            myShellLocks.AssertWriteAccessAllowed();
            if (Accepts(sourceFile))
                myDirtyFiles.Add(sourceFile);
        }

        public void SyncUpdate(bool underTransaction)
        {
            myShellLocks.AssertReadAccessAllowed();

            if (myDirtyFiles.Count > 0)
            {
                foreach (IPsiSourceFile projectFile in new List<IPsiSourceFile>(myDirtyFiles))
                {
                    using (WriteLockCookie.Create())
                    {
                        var ret = DisposeCacheBuilder.Build(projectFile);
                        Merge(projectFile, ret);
                    }
                }
            }
        }

        public bool HasDirtyFiles
        {
            get { return !myDirtyFiles.IsEmpty(); }
        }

        private static bool Accepts(IPsiSourceFile sourceFile)
        {
            return sourceFile.IsLanguageSupported<CSharpLanguage>();
        }

        [CanBeNull]
        public IEnumerable<DisposeMethodStatus> GetDisposeMethodStatusesForFile(IPsiSourceFile sourceFile)
        {
            if (!mySourceFileToDisposeStatus.ContainsKey(sourceFile))
                return null;
            return mySourceFileToDisposeStatus.GetValuesCollection(sourceFile);
        }

        [CanBeNull]
        public DisposeMethodStatus GetDisposeMethodStatusesForMethod(IPsiSourceFile sourceFile, int offset)
        {
            var data = GetDisposeMethodStatusesForFile(sourceFile);
            if (data == null)
                return null;
            return data.FirstOrDefault(status => status.Offset == offset);
        }

        #region Nested type: LexPersistentCache

        private class DisposePersistentCache<T> : SimplePersistentCache<T>
        {
            public DisposePersistentCache(IShellLocks locks, int formatVersion, string cacheDirectoryName, IPsiConfiguration psiConfiguration)
                : base(locks, formatVersion, cacheDirectoryName, psiConfiguration)
            {
            }

            protected override string LoadSaveProgressText
            {
                get { return "Dispose Caches"; }
            }
        }

        #endregion
    }

    public class DisposeCacheData
    {
        private readonly IList<DisposeMethodStatus> myMethodStatuses;

        public DisposeCacheData(IList<DisposeMethodStatus> methodStatuses)
        {
            myMethodStatuses = methodStatuses;
        }

        public IList<DisposeMethodStatus> MethodStatuses
        {
            get { return myMethodStatuses; }
        }
    }
}