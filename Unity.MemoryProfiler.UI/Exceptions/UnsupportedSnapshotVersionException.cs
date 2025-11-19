using System;
using Unity.MemoryProfiler.Editor;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 不支持的快照版本异常
    /// 当快照版本不被当前Model Builder支持时抛出
    /// </summary>
    internal class UnsupportedSnapshotVersionException : Exception
    {
        public CachedSnapshot Snapshot { get; }

        public UnsupportedSnapshotVersionException(CachedSnapshot snapshot)
            : base($"Unsupported snapshot version.")
        {
            Snapshot = snapshot;
        }

        public UnsupportedSnapshotVersionException(CachedSnapshot snapshot, string message)
            : base(message)
        {
            Snapshot = snapshot;
        }

        public UnsupportedSnapshotVersionException(CachedSnapshot snapshot, string message, Exception innerException)
            : base(message, innerException)
        {
            Snapshot = snapshot;
        }
    }
}

