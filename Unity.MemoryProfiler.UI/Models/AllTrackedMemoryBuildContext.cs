using System.Collections.Generic;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// AllTrackedMemory构建上下文
    /// 对应Unity的AllTrackedMemoryModelBuilder.BuildContext (Line 77-142)
    /// 管理构建过程中的所有中间状态，避免重复遍历CachedSnapshot
    /// </summary>
    internal class AllTrackedMemoryBuildContext
    {
        public AllTrackedMemoryBuildContext()
        {
            // Native memory groups
            NativeObjectIndex2SizeMap = new Dictionary<SourceIndex, long>();
            NativeRootReference2SizeMap = new Dictionary<SourceIndex, long>();
            NativeRootReference2UnsafeAllocations2SizeMap = new Dictionary<SourceIndex, Dictionary<SourceIndex, long>>();
            NativeRegionName2SizeMap = new Dictionary<SourceIndex, long>();

            // Managed memory groups
            ManagedTypeName2ObjectsTreeMap = new Dictionary<SourceIndex, List<TreeNode<MemoryItemData>>>();
            ManagedTypeName2NativeName2ObjectsTreeMap = new Dictionary<SourceIndex, Dictionary<string, List<TreeNode<MemoryItemData>>>>();

            // Graphics memory groups
            GfxObjectIndex2SizeMap = new Dictionary<SourceIndex, long>();
            GfxReservedRegionIndex2SizeMap = new Dictionary<SourceIndex, long>();

            // System memory regions
            ExecutablesName2SizeMap = new Dictionary<string, long>();
            UntrackedRegionsName2SizeMap = new Dictionary<string, long>();
        }

        #region Total Memory

        /// <summary>
        /// 总内存大小（Total Committed + Resident）
        /// 对应Unity BuildContext.Total (Line 79)
        /// </summary>
        public long Total { get; set; }

        #endregion

        #region Native Memory Groups

        /// <summary>
        /// Native对象索引 → 大小
        /// Key: CachedSnapshot.NativeObjects索引
        /// 对应Unity BuildContext.NativeObjectIndex2SizeMap (Line 82-83)
        /// </summary>
        public Dictionary<SourceIndex, long> NativeObjectIndex2SizeMap { get; }

        /// <summary>
        /// Native根引用索引 → 大小
        /// Key: CachedSnapshot.NativeRootReferences索引
        /// 对应Unity BuildContext.NativeRootReference2SizeMap (Line 85-86)
        /// </summary>
        public Dictionary<SourceIndex, long> NativeRootReference2SizeMap { get; }

        /// <summary>
        /// Native根引用 → Unsafe分配 → 大小
        /// Key: NativeRootReferences索引 → NativeAllocations索引 → 大小
        /// 对应Unity BuildContext.NativeRootReference2UnsafeAllocations2SizeMap (Line 88-89)
        /// </summary>
        public Dictionary<SourceIndex, Dictionary<SourceIndex, long>> NativeRootReference2UnsafeAllocations2SizeMap { get; }

        /// <summary>
        /// Native内存区域索引 → 大小
        /// Key: CachedSnapshot.NativeMemoryRegions索引（保留内存）
        /// 对应Unity BuildContext.NativeRegionName2SizeMap (Line 91-93)
        /// </summary>
        public Dictionary<SourceIndex, long> NativeRegionName2SizeMap { get; }

        #endregion

        #region Managed Memory Groups

        /// <summary>
        /// Managed类型 → 对象列表
        /// Key: TypeDescription索引
        /// Value: 该类型的所有对象TreeNode
        /// 对应Unity BuildContext.ManagedTypeName2ObjectsTreeMap (Line 97-99)
        /// </summary>
        public Dictionary<SourceIndex, List<TreeNode<MemoryItemData>>> ManagedTypeName2ObjectsTreeMap { get; }

        /// <summary>
        /// Managed类型 → Native对象名 → 对象列表
        /// Key: TypeDescription索引 → Native对象名 → 对象TreeNode列表
        /// 用于DisambiguateUnityObjects模式
        /// 对应Unity BuildContext.ManagedTypeName2NativeName2ObjectsTreeMap (Line 101-103)
        /// </summary>
        public Dictionary<SourceIndex, Dictionary<string, List<TreeNode<MemoryItemData>>>> ManagedTypeName2NativeName2ObjectsTreeMap { get; }

        /// <summary>
        /// Managed虚拟机内存大小
        /// 对应Unity BuildContext.ManagedMemoryVM (Line 105-106)
        /// </summary>
        public long ManagedMemoryVM { get; set; }

        /// <summary>
        /// Managed保留内存大小（GC Heap中未使用的部分）
        /// 对应Unity BuildContext.ManagedMemoryReserved (Line 108-109)
        /// </summary>
        public long ManagedMemoryReserved { get; set; }

        #endregion

        #region Graphics Memory Groups

        /// <summary>
        /// Graphics对象索引 → 大小
        /// Key: CachedSnapshot.GfxResources索引
        /// 对应Unity BuildContext.GfxObjectIndex2SizeMap (Line 113)
        /// </summary>
        public Dictionary<SourceIndex, long> GfxObjectIndex2SizeMap { get; }

        /// <summary>
        /// Graphics保留区域索引 → 大小
        /// 对应Unity BuildContext.GfxReservedRegionIndex2SizeMap (Line 114)
        /// </summary>
        public Dictionary<SourceIndex, long> GfxReservedRegionIndex2SizeMap { get; }

        /// <summary>
        /// 未追踪的Graphics资源大小
        /// 对应Unity BuildContext.UntrackedGraphicsResources (Line 112)
        /// </summary>
        public long UntrackedGraphicsResources { get; set; }

        #endregion

        #region System Memory Regions

        /// <summary>
        /// 可执行文件名 → 大小
        /// Key: 可执行文件/DLL名称
        /// 对应Unity BuildContext.ExecutablesName2SizeMap (Line 118)
        /// </summary>
        public Dictionary<string, long> ExecutablesName2SizeMap { get; }

        /// <summary>
        /// 未追踪区域名 → 大小
        /// Key: 内存区域名称
        /// 对应Unity BuildContext.UntrackedRegionsName2SizeMap (Line 121)
        /// </summary>
        public Dictionary<string, long> UntrackedRegionsName2SizeMap { get; }

        #endregion

        #region Platform Specific

        /// <summary>
        /// Android Runtime内存大小
        /// 对应Unity BuildContext.AndroidRuntime (Line 124)
        /// </summary>
        public long AndroidRuntime { get; set; }

        #endregion

        #region Statistics Helper Methods

        /// <summary>
        /// 获取Native总大小
        /// </summary>
        public long GetNativeTotalSize()
        {
            long total = 0;
            foreach (var size in NativeObjectIndex2SizeMap.Values)
                total += size;
            foreach (var size in NativeRootReference2SizeMap.Values)
                total += size;
            foreach (var size in NativeRegionName2SizeMap.Values)
                total += size;
            return total;
        }

        /// <summary>
        /// 获取Managed总大小
        /// </summary>
        public long GetManagedTotalSize()
        {
            long total = ManagedMemoryVM + ManagedMemoryReserved;
            
            foreach (var objectsList in ManagedTypeName2ObjectsTreeMap.Values)
            {
                foreach (var obj in objectsList)
                {
                    total += obj.Data?.Size ?? 0;
                }
            }
            
            return total;
        }

        /// <summary>
        /// 获取Graphics总大小
        /// </summary>
        public long GetGraphicsTotalSize()
        {
            long total = UntrackedGraphicsResources;
            foreach (var size in GfxObjectIndex2SizeMap.Values)
                total += size;
            foreach (var size in GfxReservedRegionIndex2SizeMap.Values)
                total += size;
            return total;
        }

        /// <summary>
        /// 获取Executables总大小
        /// </summary>
        public long GetExecutablesTotalSize()
        {
            long total = 0;
            foreach (var size in ExecutablesName2SizeMap.Values)
                total += size;
            return total;
        }

        /// <summary>
        /// 获取Untracked总大小
        /// </summary>
        public long GetUntrackedTotalSize()
        {
            long total = 0;
            foreach (var size in UntrackedRegionsName2SizeMap.Values)
                total += size;
            return total;
        }

        #endregion

        public override string ToString()
        {
            return $"BuildContext: Total={Total}, Native={GetNativeTotalSize()}, " +
                   $"Managed={GetManagedTotalSize()}, Graphics={GetGraphicsTotalSize()}, " +
                   $"Executables={GetExecutablesTotalSize()}, Untracked={GetUntrackedTotalSize()}";
        }
    }
}

