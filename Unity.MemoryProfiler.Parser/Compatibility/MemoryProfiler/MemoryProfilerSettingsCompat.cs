namespace Unity.MemoryProfiler.Editor
{
    internal static class MemoryProfilerSettings
    {
        internal static class FeatureFlags
        {
            public static bool GenerateTransformTreesForByStatusTable_2022_09 { get; set; } = false;
            public static bool EnableDynamicAllocationBreakdown_2024_10 { get; set; } = false;
            public static bool EnableUnknownUnknownAllocationBreakdown_2024_10 { get; set; } = false;
            public static bool ShowFoundReferencesForNativeAllocations_2024_10 { get; set; } = true;

            public const bool ManagedCrawlerConsidersPointersToManagedObjects = false;
            public const bool ManagedCrawlerConsidersPotentialPointersToManagedObjects = false;
            public const bool ManagedCrawlerConsidersPointersToGCHandleHeldManagedObjects = true;
        }
    }
}

