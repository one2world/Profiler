using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using UnityEngine;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Builds All Memory summary (Committed + Reserved) broken down into high-level categories.
    /// 对应Unity的AllMemorySummaryModelBuilder (Line 14-223)
    /// 100%等价实现
    /// </summary>
    internal class AllMemorySummaryModelBuilder : IMemorySummaryModelBuilder<MemorySummaryModel>
    {
        const string kPlatformIdAndroid = "dalvik";
        static readonly Dictionary<string, (string name, string descr)> k_CategoryPlatformSpecific =
            new Dictionary<string, (string, string)>()
            {
                {
                    kPlatformIdAndroid,
                    ("Android Runtime", "Memory used by the Android Dalvik/ART runtime")
                }
            };

        private readonly CachedSnapshot m_SnapshotA;
        private readonly CachedSnapshot m_SnapshotB;

        public AllMemorySummaryModelBuilder(CachedSnapshot snapshotA, CachedSnapshot snapshotB = null)
        {
            m_SnapshotA = snapshotA;
            m_SnapshotB = snapshotB;
        }

        public MemorySummaryModel Build()
        {
            Summary a, b;
            BuildSummary(m_SnapshotA, out a);
            if (m_SnapshotB != null)
                BuildSummary(m_SnapshotB, out b);
            else
                b = new Summary();

            // Add fixed categories
            var rows = new List<MemorySummaryModel.Row>()
            {
                new MemorySummaryModel.Row(
                    "Native",
                    a.Native,
                    b.Native,
                    "native",
                    "Memory allocated by the Unity engine and game's C++ code",
                    null)
                {
                    CategoryIdEnum = IAnalysisViewSelectable.Category.Native
                },
                new MemorySummaryModel.Row(
                    "Managed",
                    a.Managed,
                    b.Managed,
                    "managed",
                    "Memory allocated by C# managed code (including Unity's scripting backend)",
                    null)
                {
                    CategoryIdEnum = IAnalysisViewSelectable.Category.Managed
                },
                new MemorySummaryModel.Row(
                    "Executables and Mapped",
                    a.ExecutablesAndMapped,
                    b.ExecutablesAndMapped,
                    "executables",
                    "Memory mapped to executable files and DLLs",
                    null)
                {
                    CategoryIdEnum = IAnalysisViewSelectable.Category.ExecutablesAndMapped
                },
                new MemorySummaryModel.Row(
                    "Graphics",
                    a.GraphicsAndDrivers,
                    b.GraphicsAndDrivers,
                    "gfx",
                    "Estimated graphics memory used (textures, render targets, meshes, etc.)",
                    null)
                {
                    CategoryIdEnum = IAnalysisViewSelectable.Category.Graphics,
                    SortPriority = MemorySummaryModel.RowSortPriority.Low,
                    ResidentSizeUnavailable = true
                },
                new MemorySummaryModel.Row(
                    "Untracked (Estimated)",
                    a.Untracked,
                    b.Untracked,
                    "other",
                    "Memory that Unity does not track (system overhead, third-party libraries, etc.)",
                    null)
                {
                    CategoryIdEnum = IAnalysisViewSelectable.Category.Unknown,
                    SortPriority = MemorySummaryModel.RowSortPriority.ShowLast,
                    ResidentSizeUnavailable = true
                },
            };

            // Add platform-specific categories
            // Merge two platform specific containers into table rows
            if ((a.PlatformSpecific != null) || (b.PlatformSpecific != null))
            {
                var keysA = a.PlatformSpecific?.Keys.ToArray() ?? new string[0];
                var keysB = b.PlatformSpecific?.Keys.ToArray() ?? new string[0];
                var keys = keysA.Union(keysB);
                foreach (var key in keys)
                {
                    MemorySize valueA = new MemorySize(), valueB = new MemorySize();
                    a.PlatformSpecific?.TryGetValue(key, out valueA);
                    b.PlatformSpecific?.TryGetValue(key, out valueB);

                    // Don't show zero-sized sections
                    if ((valueA.Committed == 0) && (valueB.Committed == 0))
                        continue;

                    k_CategoryPlatformSpecific.TryGetValue(key, out var info);
                    rows.Add(new MemorySummaryModel.Row(info.name, valueA, valueB, key, info.descr, null));
                }
            }

            bool compareMode = m_SnapshotB != null;
            return new MemorySummaryModel(
                "All Memory",
                HasResidentMemory()
                    ? "Total memory allocated by the process (Committed + Reserved), with Resident sizes where available"
                    : "Total memory allocated by the process (Committed + Reserved)",
                compareMode,
                (long)a.Total.Committed,
                (long)b.Total.Committed,
                rows,
                null
            );
        }

        /// <summary>
        /// 对应Unity AllMemorySummaryModelBuilder.BuildSummary (Line 82-127)
        /// </summary>
        void BuildSummary(CachedSnapshot cs, out Summary summary)
        {
            // Calculate totals based on known objects
            CalculateTotals(cs, out summary);

            var memoryStats = cs.MetaData.TargetMemoryStats;
            if (memoryStats.HasValue)
            {
                // [Legacy] If we don't have SystemMemoryRegionsInfo, take total value from legacy memory stats
                // Nb! If you change this, change similar code in AllTrackedMemoryModelBuilder / UnityObjectsModelBuilder / AllMemorySummaryModelBuilder
                if (!cs.HasSystemMemoryRegionsInfo && (memoryStats.Value.TotalVirtualMemory > 0))
                {
                    summary.Untracked = new MemorySize(
                        memoryStats.Value.TotalVirtualMemory - summary.Total.Committed,
                        0);
                    summary.Total = new MemorySize(memoryStats.Value.TotalVirtualMemory, 0);
                }

                // System regions report less graphics memory than we have estimated through
                // all known graphics resources. In that case we "reassign" untracked category
                // to graphics category
                if (summary.GraphicsAndDrivers.Committed < memoryStats.Value.GraphicsUsedMemory)
                {
                    // We can't increase graphics memory for more than untracked
                    var delta = Math.Min(
                        memoryStats.Value.GraphicsUsedMemory - summary.GraphicsAndDrivers.Committed,
                        summary.Untracked.Committed);

                    summary.Untracked = new MemorySize(summary.Untracked.Committed - delta, 0);
                    summary.GraphicsAndDrivers = new MemorySize(summary.GraphicsAndDrivers.Committed + delta, 0);
                    summary.EstimatedGraphicsAndDrivers = true;
                }
                else if (cs.MetaData.TargetInfo is { RuntimePlatform: not RuntimePlatform.Switch })
                {
                    // Move untracked graphics memory to untracked category.
                    //
                    // We special case to skip Switch here - all its GPU allocations are completely tracked, so we don't need to fudge values.
                    // Certain regions which we'd otherwise mark as "untracked" are actually GPU reserved, which isn't
                    // yet something that the memory profiler has first-class support for.
                    var untrackedGraphics = summary.GraphicsAndDrivers.Committed - memoryStats.Value.GraphicsUsedMemory;
                    summary.Untracked = new MemorySize(summary.Untracked.Committed + untrackedGraphics, 0);
                    summary.GraphicsAndDrivers = new MemorySize(
                        summary.GraphicsAndDrivers.Committed - untrackedGraphics,
                        0);
                    summary.EstimatedGraphicsAndDrivers = true;
                }
            }

            // Add Mono or IL2CPP VM allocations
            var vmRootSize = cs.NativeRootReferences.AccumulatedSizeOfVMRoot;
            ReassignMemoryToAnotherCategory(ref summary.Managed, ref summary.Native, vmRootSize);
        }

        bool HasResidentMemory()
        {
            return m_SnapshotA.HasSystemMemoryResidentPages ||
                   (m_SnapshotB?.HasSystemMemoryResidentPages ?? false);
        }

        /// <summary>
        /// 对应Unity AllMemorySummaryModelBuilder.CalculateTotals (Line 134-189)
        /// 遍历EntriesMemoryMap并按PointType分类统计
        /// </summary>
        static void CalculateTotals(CachedSnapshot cs, out Summary summary)
        {
            var _summary = new Summary();

            cs.EntriesMemoryMap.ForEachFlatWithResidentSize((_, address, size, residentSize, source) =>
            {
                var type = cs.EntriesMemoryMap.GetPointType(source);

                var memorySize = new MemorySize(size, residentSize);

                _summary.Total += memorySize;
                switch (type)
                {
                    case EntriesMemoryMapCache.PointType.Native:
                    case EntriesMemoryMapCache.PointType.NativeReserved:
                        _summary.Native += memorySize;
                        break;

                    case EntriesMemoryMapCache.PointType.Managed:
                    case EntriesMemoryMapCache.PointType.ManagedReserved:
                        _summary.Managed += memorySize;
                        break;

                    case EntriesMemoryMapCache.PointType.Mapped:
                        _summary.ExecutablesAndMapped += memorySize;
                        break;

                    case EntriesMemoryMapCache.PointType.Device:
                        _summary.GraphicsAndDrivers += memorySize;
                        break;

                    case EntriesMemoryMapCache.PointType.Shared:
                    case EntriesMemoryMapCache.PointType.Untracked:
                        _summary.Untracked += memorySize;
                        break;

                    case EntriesMemoryMapCache.PointType.AndroidRuntime:
                    {
                        if (_summary.PlatformSpecific == null)
                            _summary.PlatformSpecific = new Dictionary<string, MemorySize>();
                        if (_summary.PlatformSpecific.TryGetValue(kPlatformIdAndroid, out var value))
                            _summary.PlatformSpecific[kPlatformIdAndroid] = value + memorySize;
                        else
                            _summary.PlatformSpecific[kPlatformIdAndroid] = memorySize;
                        break;
                    }

                    default:
                        Debug.Assert(false, "Unknown point type, please report a bug");
                        break;
                }
            });

            summary = _summary;
        }

        /// <summary>
        /// 对应Unity AllMemorySummaryModelBuilder.ReassignMemoryToAnotherCategory (Line 191-203)
        /// 用于将VM内存从Native重新分配到Managed
        /// </summary>
        static void ReassignMemoryToAnotherCategory(ref MemorySize target, ref MemorySize source, ulong size)
        {
            var committedDelta = Math.Min(source.Committed, size);
            if (committedDelta <= 0)
                return;

            // As we don't know how resident memory is spread, we reassign proportionally
            var residentDelta = source.Resident * committedDelta / source.Committed;

            var deltaSize = new MemorySize(committedDelta, residentDelta);
            source -= deltaSize;
            target += deltaSize;
        }

        /// <summary>
        /// 内部结构体，用于汇总各类别内存
        /// 对应Unity AllMemorySummaryModelBuilder.Summary (Line 205-221)
        /// </summary>
        struct Summary
        {
            // Total
            public MemorySize Total;

            // Breakdown
            public MemorySize Native;
            public MemorySize Managed;
            public MemorySize ExecutablesAndMapped;
            public MemorySize GraphicsAndDrivers;
            public MemorySize Untracked;

            public Dictionary<string, MemorySize> PlatformSpecific;

            // True when platform does not provide graphics device regions information and we use estimated value in the summary view.
            public bool EstimatedGraphicsAndDrivers;
        }
    }
}

