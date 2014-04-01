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
        private readonly JetHashSet<IPsiSourceFile> _dirtyFiles = new JetHashSet<IPsiSourceFile>();
        private readonly OneToListMap<IPsiSourceFile, DisposeMethodStatus> _sourceFileToDisposeStatus
            = new OneToListMap<IPsiSourceFile, DisposeMethodStatus>();
        private readonly IPsiConfiguration _psiConfiguration;
        private readonly IShellLocks _shellLocks;
        private readonly IPersistentIndexManager _persistentIdIndex;
        private DisposePersistentCache<DisposeCacheData> _persistentCache;

        public DisposeCache(Lifetime lifetime, IShellLocks shellLocks,
            IPsiConfiguration psiConfiguration, IPersistentIndexManager persistentIdIndex)
        {
            _psiConfiguration = psiConfiguration;
            _persistentIdIndex = persistentIdIndex;
            _shellLocks = shellLocks;
        }

        public void MarkAsDirty(IPsiSourceFile sourceFile)
        {
            if (Accepts(sourceFile))
            {
                _dirtyFiles.Add(sourceFile);
            }
        }

        public object Load(IProgressIndicator progress, bool enablePersistence)
        {
            if (!enablePersistence)
            {
                return null;
            }

            Assertion.Assert(_persistentCache == null, "_persistentCache == null");

            using (ReadLockCookie.Create())
            {
                _persistentCache = new DisposePersistentCache<DisposeCacheData>(_shellLocks, VERSION, "DisposeCache", _psiConfiguration);
            }

            var data = new Dictionary<IPsiSourceFile, DisposeCacheData>();

            if (_persistentCache.Load(progress, _persistentIdIndex,
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
                if (pair.Key.IsValid() && !_dirtyFiles.Contains(pair.Key))
                {
                    ((ICache)this).Merge(pair.Key, pair.Value);
                }
            }
        }

        public void Save(IProgressIndicator progress, bool enablePersistence)
        {
            if (!enablePersistence)
                return;

            Assertion.Assert(_persistentCache != null, "_persistentCache != null");
            _persistentCache.Save(progress, _persistentIdIndex, (writer, file, data) =>
            DisposeCacheSerializer.Write(data, writer));
            _persistentCache.Dispose();
            _persistentCache = null;
        }

        public bool UpToDate(IPsiSourceFile sourceFile)
        {
            _shellLocks.AssertReadAccessAllowed();

            if (!Accepts(sourceFile))
                return true;

            return !_dirtyFiles.Contains(sourceFile) && _sourceFileToDisposeStatus.ContainsKey(sourceFile);
        }

        public object Build(IPsiSourceFile sourceFile, bool isStartup)
        {
            return DisposeCacheBuilder.Build(sourceFile);
        }

        public void Merge(IPsiSourceFile sourceFile, object builtPart)
        {
            _shellLocks.AssertWriteAccessAllowed();

            _sourceFileToDisposeStatus.RemoveKey(sourceFile);

            var data = builtPart as IList<DisposeMethodStatus>;
            if (data != null)
                _sourceFileToDisposeStatus.AddValueRange(sourceFile, data);

            _dirtyFiles.Remove(sourceFile);
        }

        public void Drop(IPsiSourceFile sourceFile)
        {
            _shellLocks.AssertWriteAccessAllowed();

            _sourceFileToDisposeStatus.RemoveKey(sourceFile);

            _dirtyFiles.Add(sourceFile);
        }

        public void OnPsiChange(ITreeNode elementContainingChanges, PsiChangedElementType type)
        {
            if (elementContainingChanges == null)
                return;
            _shellLocks.AssertWriteAccessAllowed();
            var projectFile = elementContainingChanges.GetSourceFile();
            if (projectFile != null && Accepts(projectFile))
                _dirtyFiles.Add(projectFile);
        }

        public void OnDocumentChange(IPsiSourceFile sourceFile, ProjectFileDocumentCopyChange change)
        {
            _shellLocks.AssertWriteAccessAllowed();
            if (Accepts(sourceFile))
                _dirtyFiles.Add(sourceFile);
        }

        public void SyncUpdate(bool underTransaction)
        {
            _shellLocks.AssertReadAccessAllowed();

            if (_dirtyFiles.Count > 0)
            {
                foreach (IPsiSourceFile projectFile in new List<IPsiSourceFile>(_dirtyFiles))
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
            get { return !_dirtyFiles.IsEmpty(); }
        }

        private static bool Accepts(IPsiSourceFile sourceFile)
        {
            return sourceFile.IsLanguageSupported<CSharpLanguage>();
        }

        [CanBeNull]
        public IEnumerable<DisposeMethodStatus> GetDisposeMethodStatusesForFile([NotNull] IPsiSourceFile sourceFile)
        {
            if (!_sourceFileToDisposeStatus.ContainsKey(sourceFile))
                return null;
            return _sourceFileToDisposeStatus.GetValuesCollection(sourceFile);
        }

        [CanBeNull]
        public DisposeMethodStatus GetDisposeMethodStatusesForMethod([NotNull] IPsiSourceFile sourceFile, int offset)
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
        private readonly IList<DisposeMethodStatus> _methodStatuses;

        public DisposeCacheData(IList<DisposeMethodStatus> methodStatuses)
        {
            _methodStatuses = methodStatuses;
        }

        public IList<DisposeMethodStatus> MethodStatuses
        {
            get { return _methodStatuses; }
        }
    }
}