using System.Collections.Generic;
using System.IO;
using JetBrains.ReSharper.Psi;

namespace DisposePlugin.Cache
{
    public static class DisposeCacheSerializer
    {
        public static DisposeCacheData Read(BinaryReader reader, IPsiSourceFile sourceFile)
        {
            var count = reader.ReadInt32();
            var statuses = new List<DisposeMethodStatus>(count);
            for (var i = 0; i < count; i++)
                statuses.Add(DisposeMethodStatus.Read(reader, sourceFile));
            return new DisposeCacheData(statuses);
        }

        public static void Write(DisposeCacheData data, BinaryWriter writer)
        {
            var methods = data.MethodStatuses;
            writer.Write(methods.Count);
            foreach (var method in methods)
            {
                method.Write(writer);
            }
        }
    }
}