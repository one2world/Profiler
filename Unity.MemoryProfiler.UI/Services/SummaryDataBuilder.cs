using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Format;
using Unity.MemoryProfiler.Editor.Format.QueriedSnapshot;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// Summary 数据构建器 - 基于 Unity 官方逻辑
    /// 参考: com.unity.memoryprofiler@1.1.6/Editor/UI/Analysis/Breakdowns/Summary/Data/
    /// </summary>
    internal class SummaryDataBuilder
    {
        private readonly CachedSnapshot _snapshotA;
        private readonly CachedSnapshot? _snapshotB;
        private readonly bool _compareMode;

        public SummaryDataBuilder(CachedSnapshot snapshotA, CachedSnapshot? snapshotB = null)
        {
            _snapshotA = snapshotA ?? throw new ArgumentNullException(nameof(snapshotA));
            _snapshotB = snapshotB;
            _compareMode = snapshotB != null;
        }

        public SummaryData Build()
        {
            return new SummaryData
            {
                MemoryUsage = BuildMemoryUsageOnDevice(),
                MemoryDistribution = BuildAllocatedMemoryDistribution(),
                HeapUtilization = BuildManagedHeapUtilization(),
                TopCategories = BuildTopUnityObjectsCategories(),
                CompareMode = _compareMode
            };
        }

        /// <summary>
        /// 构建设备内存使用情况
        /// 参考: ResidentMemorySummaryModelBuilder
        /// </summary>
        private MemoryUsageOnDevice BuildMemoryUsageOnDevice()
        {
            var result = new MemoryUsageOnDevice();

            // Snapshot A
            if (_snapshotA.MetaData.TargetMemoryStats.HasValue)
            {
                var stats = _snapshotA.MetaData.TargetMemoryStats.Value;
                result.TotalAllocatedMB = stats.TotalVirtualMemory / (1024.0 * 1024.0);
            }

            if (_snapshotA.HasSystemMemoryRegionsInfo)
            {
                ulong totalResident = 0;
                for (int i = 0; i < _snapshotA.SystemMemoryRegions.Count; i++)
                {
                    totalResident += _snapshotA.SystemMemoryRegions.RegionResident[i];
                }
                result.TotalResidentMB = totalResident / (1024.0 * 1024.0);
            }
            else
            {
                result.TotalResidentMB = result.TotalAllocatedMB;
            }

            // Snapshot B (对比模式)
            if (_compareMode && _snapshotB != null)
            {
                if (_snapshotB.MetaData.TargetMemoryStats.HasValue)
                {
                    var stats = _snapshotB.MetaData.TargetMemoryStats.Value;
                    result.TotalAllocatedMB_B = stats.TotalVirtualMemory / (1024.0 * 1024.0);
                }

                if (_snapshotB.HasSystemMemoryRegionsInfo)
                {
                    ulong totalResident = 0;
                    for (int i = 0; i < _snapshotB.SystemMemoryRegions.Count; i++)
                    {
                        totalResident += _snapshotB.SystemMemoryRegions.RegionResident[i];
                    }
                    result.TotalResidentMB_B = totalResident / (1024.0 * 1024.0);
                }
                else
                {
                    result.TotalResidentMB_B = result.TotalAllocatedMB_B;
                }
            }

            return result;
        }

        /// <summary>
        /// 构建已分配内存分布
        /// 参考: AllMemorySummaryModelBuilder.Build 和 CalculateTotals
        /// </summary>
        private AllocatedMemoryDistribution BuildAllocatedMemoryDistribution()
        {
            var result = new AllocatedMemoryDistribution();
            
            // 分别计算 A 和 B 的内存分布
            var summaryA = CalculateMemorySummary(_snapshotA);
            
            MemorySummary summaryB = default;
            if (_compareMode && _snapshotB != null)
            {
                summaryB = CalculateMemorySummary(_snapshotB);
            }
            
            // 使用 A 的总内存（对比模式下可能需要显示两者）
            result.TotalAllocatedMB = summaryA.TotalMemory / (1024.0 * 1024.0);
            
            var categories = new List<MemoryCategory>();

            // 按照 Unity 官方顺序添加类别（Native, Managed, Executables, Graphics, Untracked）
            // Unity 逻辑：每个类别同时包含 A 和 B 的数据
            
            // Native
            // Native
            // 参考: Unity 的 AllMemorySummaryModelBuilder.Build() - Native category
            if (summaryA.NativeMemory > 0 || summaryB.NativeMemory > 0)
            {
                categories.Add(CreateMemoryCategory(
                    "Native", 
                    summaryA.NativeMemory, 
                    summaryB.NativeMemory, 
                    summaryA.TotalMemory, 
                    summaryB.TotalMemory,
                    "#4CAF50",
                    "Native memory, used by objects such as:\n- Scene Objects (Game Objects and their Components),\n- Assets and Managers\n\nYou can reduce this memory usage by reducing the amount of Assets in your Scenes and by using the Addressables system to load and unload Assets on demand.",
                    ""));
            }

            // Managed
            // 参考: Unity 的 AllMemorySummaryModelBuilder.Build() - Managed category
            if (summaryA.ManagedMemory > 0 || summaryB.ManagedMemory > 0)
            {
                categories.Add(CreateMemoryCategory(
                    "Managed", 
                    summaryA.ManagedMemory, 
                    summaryB.ManagedMemory, 
                    summaryA.TotalMemory, 
                    summaryB.TotalMemory,
                    "#2196F3",
                    "Contains all Virtual Machine and Managed Heap memory\n\nThe Managed Heap contains data related to Managed Objects and the space that has been reserved for them. It is managed by the Scripting Garbage Collector, so that any managed objects that no longer have references chain to a root are collected.\n\nThe used amount in the Managed Memory is made up of memory used for Managed objects and of empty space that cannot be returned.\n\nYou can reduce this memory usage by reducing the amount of Managed Objects and by using Object Pooling to reuse objects instead of creating new ones.",
                    ""));
            }

            // Executables & Mapped
            // 参考: Unity 的 AllMemorySummaryModelBuilder.Build() - ExecutablesAndMapped category
            if (summaryA.ExecutablesAndMapped > 0 || summaryB.ExecutablesAndMapped > 0)
            {
                categories.Add(CreateMemoryCategory(
                    "Executables & Mapped", 
                    summaryA.ExecutablesAndMapped, 
                    summaryB.ExecutablesAndMapped, 
                    summaryA.TotalMemory, 
                    summaryB.TotalMemory,
                    "#FFA500",
                    "Memory taken up by the build code of the application, including all shared libraries and assemblies, managed and native. This value is not yet reported consistently on all platforms.\n\nYou can reduce this memory usage by using a higher code stripping level and by reducing your dependencies on different modules and libraries.",
                    ""));
            }

            // Graphics
            // 参考: Unity 的 AllMemorySummaryModelBuilder.Build() - Graphics category
            if (summaryA.Graphics > 0 || summaryB.Graphics > 0)
            {
                categories.Add(CreateMemoryCategory(
                    "Graphics (Estimated)", 
                    summaryA.Graphics, 
                    summaryB.Graphics, 
                    summaryA.TotalMemory, 
                    summaryB.TotalMemory,
                    "#9C27B0",
                    "Estimated memory used by the Graphics Driver and the GPU to visualize your application.\nThe information is based on the tracking of graphics resource allocations within Unity. This includes RenderTextures, Textures, Meshes, Animations and other graphics buffers which are allocated by Unity or Scripting API. Use All Of Memory tab to explore graphics resources.\nNot all these objects' memory is represented in this category. For example, Read/Write enabled graphics assets need to retain a copy in CPU-accessible memory, which doubles their total memory usage. Use Unity Objects tab to explore total memory usage of Unity Objects.",
                    ""));
            }

            // Untracked
            // 参考: Unity 的 AllMemorySummaryModelBuilder.Build() - Untracked category
            if (summaryA.Untracked > 0 || summaryB.Untracked > 0)
            {
                categories.Add(CreateMemoryCategory(
                    "Untracked", 
                    summaryA.Untracked, 
                    summaryB.Untracked, 
                    summaryA.TotalMemory, 
                    summaryB.TotalMemory,
                    "#757575",
                    "Memory that the memory profiler cannot yet account for, due to platform specific requirements, potential bugs or other gaps in memory tracking.\nThe size of Untracked memory is determined by analyzing all allocated and resident memory regions of the process and subtracting known regions which Unity native and managed memory allocators.\nTo analyze this memory further, you will need to use a platform specific profiler.",
                    "https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.1/manual/untracked-memory.html"));
            }
            result.Categories = categories;

            return result;
        }

        /// <summary>
        /// 构建托管堆利用率
        /// 参考: ManagedMemorySummaryModelBuilder
        /// </summary>
        private ManagedHeapUtilization BuildManagedHeapUtilization()
        {
            var result = new ManagedHeapUtilization();

            // 计算 Snapshot A
            var (vmA, emptyA, objectsA) = CalculateManagedHeapBreakdown(_snapshotA);
            ulong totalA = vmA + emptyA + objectsA;

            result.VirtualMachineMB = vmA / (1024.0 * 1024.0);
            result.EmptyHeapSpaceMB = emptyA / (1024.0 * 1024.0);
            result.ObjectsMB = objectsA / (1024.0 * 1024.0);
            result.TotalMB = totalA / (1024.0 * 1024.0);

            // 计算 Snapshot B (对比模式)
            if (_compareMode && _snapshotB != null)
            {
                var (vmB, emptyB, objectsB) = CalculateManagedHeapBreakdown(_snapshotB);
                ulong totalB = vmB + emptyB + objectsB;

                result.VirtualMachineMB_B = vmB / (1024.0 * 1024.0);
                result.EmptyHeapSpaceMB_B = emptyB / (1024.0 * 1024.0);
                result.ObjectsMB_B = objectsB / (1024.0 * 1024.0);
                result.TotalMB_B = totalB / (1024.0 * 1024.0);
            }

            return result;
        }

        /// <summary>
        /// 计算单个快照的托管堆分解
        /// 参考: ManagedMemorySummaryModelBuilder.CalculateTotal
        /// </summary>
        private (ulong vmMemory, ulong emptyHeap, ulong objects) CalculateManagedHeapBreakdown(CachedSnapshot snapshot)
        {
            ulong vmMemory = 0;
            ulong emptyHeap = 0;
            ulong objects = 0;

            // 遍历托管堆区域
            snapshot.EntriesMemoryMap.ForEachFlat((_, address, size, source) =>
            {
                switch (source.Id)
                {
                    case CachedSnapshot.SourceIndex.SourceId.ManagedHeapSection:
                        {
                            var sectionType = snapshot.ManagedHeapSections.SectionType[source.Index];
                            switch (sectionType)
                            {
                                case CachedSnapshot.MemorySectionType.VirtualMachine:
                                    vmMemory += size;
                                    break;
                                case CachedSnapshot.MemorySectionType.GarbageCollector:
                                    emptyHeap += size;
                                    break;
                            }
                            break;
                        }
                    case CachedSnapshot.SourceIndex.SourceId.ManagedObject:
                        objects += size;
                        break;
                }
            });

            // 添加 VM 根引用大小
            var vmRootSize = snapshot.NativeRootReferences.AccumulatedSizeOfVMRoot;
            vmMemory += vmRootSize;

            return (vmMemory, emptyHeap, objects);
        }

        /// <summary>
        /// 构建 Unity 对象类别排行 (Top N)
        /// 参考: UnityObjectsMemorySummaryModelBuilder
        /// </summary>
        private TopUnityObjectsCategories BuildTopUnityObjectsCategories()
        {
            var result = new TopUnityObjectsCategories();

            // 计算 Snapshot A 的类型大小
            var typeToSizeMapA = CalculateUnityObjectTypesSizes(_snapshotA);
            ulong totalSizeA = typeToSizeMapA.Values.Aggregate(0UL, (sum, val) => sum + val);

            // 计算 Snapshot B 的类型大小 (对比模式)
            Dictionary<string, ulong> typeToSizeMapB = null;
            ulong totalSizeB = 0;
            if (_compareMode && _snapshotB != null)
            {
                typeToSizeMapB = CalculateUnityObjectTypesSizes(_snapshotB);
                totalSizeB = typeToSizeMapB.Values.Aggregate(0UL, (sum, val) => sum + val);
            }

            // 合并两个快照的类型名称（取并集）
            var allTypeNames = new HashSet<string>(typeToSizeMapA.Keys);
            if (typeToSizeMapB != null)
            {
                foreach (var typeName in typeToSizeMapB.Keys)
                    allTypeNames.Add(typeName);
            }

            // 按 Snapshot A 的大小排序
            var sortedTypes = allTypeNames
                .Select(typeName => new
                {
                    Name = typeName,
                    SizeA = typeToSizeMapA.ContainsKey(typeName) ? typeToSizeMapA[typeName] : 0,
                    SizeB = typeToSizeMapB != null && typeToSizeMapB.ContainsKey(typeName) ? typeToSizeMapB[typeName] : 0
                })
                .OrderByDescending(x => x.SizeA)
                .ToList();

            var categories = new List<UnityObjectCategory>();
            int topCount = Math.Min(5, sortedTypes.Count);
            ulong topTotalSizeA = 0;
            ulong topTotalSizeB = 0;

            // 为 Top 5 类别分配不同的颜色
            // 参考: Unity 的 ColorPalette
            string[] colors = new[] { "#9B59B6", "#3498DB", "#E74C3C", "#F39C12", "#2ECC71" };

            for (int i = 0; i < topCount; i++)
            {
                var item = sortedTypes[i];
                categories.Add(new UnityObjectCategory
                {
                    Name = item.Name,
                    SizeMB = item.SizeA / (1024.0 * 1024.0),
                    SizeMB_B = item.SizeB / (1024.0 * 1024.0),
                    Color = colors[i % colors.Length],
                    Percentage = totalSizeA > 0 ? (double)item.SizeA / totalSizeA : 0,
                    Percentage_B = totalSizeB > 0 ? (double)item.SizeB / totalSizeB : 0
                });
                topTotalSizeA += item.SizeA;
                topTotalSizeB += item.SizeB;
            }

            // 添加 "Other" 类别
            if (sortedTypes.Count > topCount)
            {
                var otherSizeA = totalSizeA - topTotalSizeA;
                var otherSizeB = totalSizeB - topTotalSizeB;
                categories.Add(new UnityObjectCategory
                {
                    Name = "Other",
                    SizeMB = otherSizeA / (1024.0 * 1024.0),
                    SizeMB_B = otherSizeB / (1024.0 * 1024.0),
                    Color = "#BDBDBD",
                    Percentage = totalSizeA > 0 ? (double)otherSizeA / totalSizeA : 0,
                    Percentage_B = totalSizeB > 0 ? (double)otherSizeB / totalSizeB : 0
                });
            }

            result.TotalMB = totalSizeA / (1024.0 * 1024.0);
            result.Categories = categories;

            return result;
        }

        /// <summary>
        /// 计算单个快照的 Unity 对象类型大小
        /// 参考: UnityObjectsMemorySummaryModelBuilder.Build
        /// </summary>
        private Dictionary<string, ulong> CalculateUnityObjectTypesSizes(CachedSnapshot snapshot)
        {
            var typeToSizeMap = new Dictionary<string, ulong>();

            // 遍历 ProcessedNativeRoots 计算每个对象的大小
            var objectsSizeMap = new Dictionary<long, ulong>();
            for (long i = 0; i < snapshot.ProcessedNativeRoots.Count; i++)
            {
                ref readonly var data = ref snapshot.ProcessedNativeRoots.Data[i];
                if (data.NativeObjectOrRootIndex.Id == CachedSnapshot.SourceIndex.SourceId.NativeObject)
                {
                    var objectSize = data.AccumulatedRootSizes.SumUp().Committed;
                    objectsSizeMap[data.NativeObjectOrRootIndex.Index] = objectSize;
                }
            }

            // 按类型分组
            foreach (var kvp in objectsSizeMap)
            {
                var objectIndex = kvp.Key;
                var size = kvp.Value;

                // 获取对象类型名称
                var typeIndex = snapshot.NativeObjects.NativeTypeArrayIndex[objectIndex];
                if (typeIndex >= 0 && typeIndex < snapshot.NativeTypes.TypeName.Length)
                {
                    var typeName = snapshot.NativeTypes.TypeName[typeIndex];
                    if (!typeToSizeMap.ContainsKey(typeName))
                        typeToSizeMap[typeName] = 0;
                    typeToSizeMap[typeName] += size;
                }
            }

            return typeToSizeMap;
        }

        /// <summary>
        /// 计算单个快照的内存分布
        /// 参考: AllMemorySummaryModelBuilder.CalculateTotals
        /// </summary>
        private MemorySummary CalculateMemorySummary(CachedSnapshot snapshot)
        {
            var summary = new MemorySummary();

            // 遍历 EntriesMemoryMap 计算各类别
            // 注意：不能在 lambda 中直接修改 struct，需要使用临时变量
            ulong totalMemory = 0;
            ulong nativeMemory = 0;
            ulong managedMemory = 0;
            ulong executablesAndMapped = 0;
            ulong graphics = 0;
            ulong untracked = 0;

            snapshot.EntriesMemoryMap.ForEachFlatWithResidentSize((_, address, size, residentSize, source) =>
            {
                var type = snapshot.EntriesMemoryMap.GetPointType(source);
                
                totalMemory += size;
                
                switch (type)
                {
                    case CachedSnapshot.EntriesMemoryMapCache.PointType.Native:
                    case CachedSnapshot.EntriesMemoryMapCache.PointType.NativeReserved:
                        nativeMemory += size;
                        break;

                    case CachedSnapshot.EntriesMemoryMapCache.PointType.Managed:
                    case CachedSnapshot.EntriesMemoryMapCache.PointType.ManagedReserved:
                        managedMemory += size;
                        break;

                    case CachedSnapshot.EntriesMemoryMapCache.PointType.Mapped:
                        executablesAndMapped += size;
                        break;

                    case CachedSnapshot.EntriesMemoryMapCache.PointType.Device:
                        graphics += size;
                        break;

                    case CachedSnapshot.EntriesMemoryMapCache.PointType.Shared:
                    case CachedSnapshot.EntriesMemoryMapCache.PointType.Untracked:
                        untracked += size;
                        break;
                }
            });

            // 将临时变量赋值给 summary
            summary.TotalMemory = totalMemory;
            summary.NativeMemory = nativeMemory;
            summary.ManagedMemory = managedMemory;
            summary.ExecutablesAndMapped = executablesAndMapped;
            summary.Graphics = graphics;
            summary.Untracked = untracked;

            // 添加 VM 根引用 (Mono 或 IL2CPP VM 分配)
            // 从 Native 移动到 Managed
            var vmRootSize = snapshot.NativeRootReferences.AccumulatedSizeOfVMRoot;
            if (vmRootSize > 0)
            {
                var delta = Math.Min(summary.NativeMemory, vmRootSize);
                summary.NativeMemory -= delta;
                summary.ManagedMemory += delta;
            }

            // 处理 Graphics 估计值
            if (snapshot.MetaData.TargetMemoryStats.HasValue)
            {
                var stats = snapshot.MetaData.TargetMemoryStats.Value;
                
                // 如果没有 SystemMemoryRegionsInfo，使用 legacy 统计
                if (!snapshot.HasSystemMemoryRegionsInfo && stats.TotalVirtualMemory > 0)
                {
                    summary.Untracked = stats.TotalVirtualMemory - summary.TotalMemory;
                    summary.TotalMemory = stats.TotalVirtualMemory;
                }

                // 调整 Graphics 内存估计
                if (summary.Graphics < stats.GraphicsUsedMemory)
                {
                    var delta = Math.Min(stats.GraphicsUsedMemory - summary.Graphics, summary.Untracked);
                    summary.Untracked -= delta;
                    summary.Graphics += delta;
                }
                else
                {
                    var untrackedGraphics = summary.Graphics - stats.GraphicsUsedMemory;
                    summary.Untracked += untrackedGraphics;
                    summary.Graphics -= untrackedGraphics;
                }
            }

            return summary;
        }

        /// <summary>
        /// 创建内存类别（支持对比模式）
        /// </summary>
        /// <summary>
        /// 创建内存类别
        /// 参考: Unity 的 AllMemorySummaryModelBuilder.Build() 中的 Row 构造
        /// </summary>
        private MemoryCategory CreateMemoryCategory(
            string name, 
            ulong sizeA, 
            ulong sizeB, 
            ulong totalA, 
            ulong totalB,
            string color,
            string description = "",
            string documentationUrl = "")
        {
            var category = new MemoryCategory
            {
                Name = name,
                SizeMB = sizeA / (1024.0 * 1024.0),
                Color = color,
                Percentage = totalA > 0 ? (double)sizeA / totalA : 0,
                Description = description,
                DocumentationUrl = documentationUrl
            };

            if (_compareMode && _snapshotB != null)
            {
                category.SizeMB_B = sizeB / (1024.0 * 1024.0);
                category.Percentage_B = totalB > 0 ? (double)sizeB / totalB : 0;
                // DiffMB 是计算属性，会自动计算 SizeMB_B - SizeMB
            }

            return category;
        }

        /// <summary>
        /// 内存分布摘要
        /// </summary>
        private struct MemorySummary
        {
            public ulong TotalMemory;
            public ulong NativeMemory;
            public ulong ManagedMemory;
            public ulong ExecutablesAndMapped;
            public ulong Graphics;
            public ulong Untracked;
        }
    }
}

