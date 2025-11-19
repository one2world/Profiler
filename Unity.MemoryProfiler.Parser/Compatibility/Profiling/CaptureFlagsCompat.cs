using System;

namespace Unity.Profiling.Memory
{
    [Flags]
    public enum CaptureFlags
    {
        ManagedObjects = 1 << 0,
        NativeObjects = 1 << 1,
        NativeAllocations = 1 << 2,
        NativeAllocationSites = 1 << 3,
        NativeStackTraces = 1 << 4,
        ManagedAllocations = 1 << 5,
        ManagedAllocationSites = 1 << 6,
        ManagedStackTraces = 1 << 7,
        GCHandles = 1 << 8,
        Connections = 1 << 9,
        SystemMemoryRegions = 1 << 10,
        MemoryProfilerMetadata = 1 << 11,
        All = ~0
    }
}

