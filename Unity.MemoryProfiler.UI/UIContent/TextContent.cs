namespace Unity.MemoryProfiler.UI.UIContent
{
    /// <summary>
    /// UI文本内容
    /// 参考: Unity.MemoryProfiler.Editor.UIContentData.TextContent
    /// </summary>
    public static class TextContent
    {
        // ===== 常规消息 =====
        public const string NoSelectionMessage = "No Selection";
        public const string NoSelectionDescription = "Select an item from the tree to view its details here.";
        public const string InvalidObjectMessage = "Failed to read Object data.";
        public const string PleaseReportBugMessage = "If this issue persists, please consider reporting it.";
        public const string InvalidObjectPleaseReportABugMessage = "please report this issue via 'Help > Report a Bug...' with this snapshot attached.";

        // ===== 视图名称 =====
        public const string SummaryViewName = "Summary";
        public const string UnityObjectsViewName = "Unity Objects";
        public const string AllOfMemoryViewName = "All Of Memory";
        public const string MemoryMapViewName = "Memory Map";

        // ===== 详情面板标题 =====
        public const string BasicInfoGroupName = "Basic Information";
        public const string MemoryInfoGroupName = "Memory Information";
        public const string DescriptionGroupName = "Description";
        public const string AdvancedInfoGroupName = "Advanced";
        public const string CallStacksGroupName = "Call Stacks";

        // ===== 内存描述文本 =====
        
        /// <summary>
        /// Native内存描述
        /// </summary>
        public static string NativeDescription => "Native memory, used by objects such as:" +
            "\n- Scene Objects (Game Objects and their Components)," +
            "\n- Assets and Managers" +
            "\n- Native Allocations including Native Arrays and other Native Containers" +
            "\n- CPU side of Graphics Asset memory" +
            "\n- And other" +
            "\n\nThis doesn't include Graphics, which is shown in a separate category." +
            $"\n\nYou can inspect these categories further in the {AllOfMemoryViewName} view.";

        /// <summary>
        /// Managed内存描述
        /// </summary>
        public static string ManagedDescription => "Contains all Virtual Machine and Managed Heap memory" +
            "\n\nThe Managed Heap contains data related to Managed Objects and the space that has been reserved for them. It is managed by the Scripting Garbage Collector, so that any managed objects that no longer have references chain to a root are collected." +
            "\n\nThe used amount in the Managed Memory is made up of memory used for Managed objects and of empty space that cannot be returned." +
            "\n\nThe 'reserved' amount in this category may be quickly be reused if needed, or it will be returned to the system every 6th GC.Collect sweep.";

        /// <summary>
        /// Executables and Mapped Files描述
        /// </summary>
        public static string ExecutablesAndMappedDescription => "Memory taken up by the build code of the application, including all shared libraries and assemblies, managed and native. This value is not yet reported consistently on all platforms." +
            "\n\nYou can reduce this memory usage by using a higher code stripping level and by reducing your dependencies on different modules and libraries.";

        /// <summary>
        /// Graphics (Estimated)描述
        /// </summary>
        public static string GraphicsEstimatedDescription => "Estimated memory used by the Graphics Driver and the GPU to visualize your application." +
            $"\nThe information is based on the tracking of graphics resource allocations within Unity. This includes RenderTextures, Textures, Meshes, Animations and other graphics buffers which are allocated by Unity or Scripting API. Use {AllOfMemoryViewName} tab to explore graphics resources." +
            $"\nNot all these objects' memory is represented in this category. For example, Read/Write enabled graphics assets need to retain a copy in CPU-accessible memory, which doubles their total memory usage. Use {UnityObjectsViewName} tab to explore total memory usage of Unity Objects. " +
            "Also, not necessarily all memory from these type of objects resides in GPU memory. Memory Profiler is unable to get exact residence information for graphics resources.";

        /// <summary>
        /// Graphics (Disabled)描述
        /// </summary>
        public static string GraphicsEstimatedDisabledDescription => "Estimated memory used by the Graphics Driver and the GPU to visualize your application." +
            "\nThe information is based on the process memory regions reported by the operating system. This includes display buffers, RenderTextures, Textures, Meshes, Animations." +
            "\n\nNote: The current platform does not provide device memory information and we can not determine resident memory details of graphics memory. " +
            "We defer analysis to the 'Untracked' group which accurately represents resident and allocated memory status and is based on memory regions information provided by the operating system. " +
            "And we keep 'Graphics' group only for reference as a disabled view item.";

        /// <summary>
        /// Untracked内存描述
        /// </summary>
        public static string UntrackedDescription => "Memory that the memory profiler cannot yet account for, due to platform specific requirements, potential bugs or other gaps in memory tracking. " +
            "\nThe size of Untracked memory is determined by analyzing all allocated and resident memory regions of the process and subtracting known regions which Unity native and managed memory allocators use." +
            "\nTo analyze this memory further, you will need to use a platform specific profiler." +
            "\n\nNote: Untracked memory might include a portion of 'Graphics' memory. " +
            "We do know accurate information about Untracked memory regions, but we are not able to determine contribution of individual graphics resources to the specific memory region." +
            "\nThus we display Untracked regions according to the system information and disable 'Graphics' group in the view.";

        /// <summary>
        /// Untracked (Estimated)描述
        /// </summary>
        public static string UntrackedEstimatedDescription => "Memory that the memory profiler cannot yet account for, due to platform specific requirements, potential bugs or other gaps in memory tracking. " +
            "\nThe size of Untracked memory is determined by analyzing all allocated and resident memory regions of the process and subtracting known regions which Unity native and managed memory allocators." +
            "\nTo analyze this memory further, you will need to use a platform specific profiler." +
            "\n\n*: When calculating Allocated size of Untracked memory we also subtract the size of 'Graphics' memory. " +
            "We do know that certain types of memory regions are allocated for the graphics device, however we are unable to determine mapping of individual graphics resources to those regions. " +
            "Thus we subtract the total 'Graphics' size from regions which belong to the graphics device, and then from the biggest regions if we are unable to determine device regions.";

        /// <summary>
        /// Native Reserved内存描述
        /// </summary>
        public static string NativeReservedDescription => "Reserved memory is memory that Unity allocated from the system (OS) but isn't used by any Unity objects at the moment of the capture. " +
            "There are many reasons why Unity might allocate memory for it internal allocators:" +
            "\n- Loading of resources" +
            "\n- Direct allocation by the user" +
            "\n- Memory-heavy computations" +
            "\n- Creation & destruction of GameObjects and their components" +
            "\n\nMost Unity allocators allocate memory in chunks, and when a chunk isn't used anymore it's released back to the system. " +
            "If you observe a high value of reserved memory it's most probably caused by fragmentation. " +
            "When memory is fragmented small objects might still reside in a chunk and cause it to remain allocated by Unity. " +
            "You can investigate which allocator has the highest reserved memory value by enabling detailed memory breakdown in settings.";

        /// <summary>
        /// Managed Reserved内存描述
        /// </summary>
        public static string ManagedReservedDescription => "Reserved memory is memory that Unity Managed Heap allocated from the system (OS) but isn't used by any Unity objects at the moment of the capture. " +
            "\n\nManaged Heap is allocated in blocks which store managed objects of similar size. Each block can store some amount of such objects and if it stays empty for several GC passes the block is released to the OS. " +
            "Managed Heap blocks might get fragmented and still contain just a few objects out of a capacity of thousands. Such blocks are still considered used, so their memory can't be returned to the system and they count towards 'reserved'.";

        /// <summary>
        /// Graphics Reserved内存描述
        /// </summary>
        public static string GraphicsReservedDescription => "Reserved Graphics memory that has been allocated but not yet used by any graphics resources.";

        /// <summary>
        /// Android Runtime描述
        /// </summary>
        public static string AndroidRuntimeDescription => "Android Runtime (ART) is the managed runtime used by applications and some system services on Android. " +
            "ART as the runtime executes the Dalvik Executable format and Dex bytecode specification." +
            "\n\nTo profile Android Runtime use platform native tools such as Android Studio.";

        // ===== 类型和对象描述 =====
        
        public const string SystemMemoryRegionDescription = "Region as reported by the OS";
        public const string NativeAllocationDescription = "Native Allocation registered by Unity Memory Manager";
        public const string ManagedMemoryHeapDescription = "Allocation made by Mono/IL2CPP for GC pool";
        public const string NativeMemoryRegionDescription = "This is a memory chunk allocated by Unity Allocator. " +
            "Most Unity allocators allocate memory in chunks, and when a chunk isn't used anymore it's released back to the system. " +
            "You can read more about different types of Unity allocators and their settings on \"Memory allocator customization\" documentation page.";
        
        public const string NonTypedGroupDescription = "The selected item is a group of similar elements";

        // ===== Unity Objects描述 =====
        
        public const string UnityObjectDescription = "This is a Unity Object (derived from UnityEngine.Object).";
        public const string NativeObjectDescription = "This is a Native Unity Object.";
        public const string ManagedObjectDescription = "This is a Managed (C#) Object.";
        public const string TypeDescription = "The selected item is a Type.";

        // ===== 警告和提示 =====
        
        public const string UnreliableDataWarning = "This is an estimated value and may not be fully accurate. The actual value might differ from what is shown here.";
        public const string EstimatedMemoryWarning = "This memory value is estimated and may not reflect the exact usage.";
        public const string NoDataAvailable = "No data available for this item.";

        // ===== 工具提示 =====
        
        public const string AllocatedMemoryTooltip = "Allocated (or Committed) memory is memory that the OS has allocated for the application.";
        public const string ResidentMemoryTooltip = "Resident memory is memory that is currently in physical RAM.";
        public const string PercentageTooltip = "Percentage of total memory used by this item.";
        
        public const string NativeMemoryTooltip = "Memory allocated on the native (C++) side of Unity.";
        public const string ManagedMemoryTooltip = "Memory allocated in the managed (C#) heap.";
        public const string GraphicsMemoryTooltip = "Memory allocated for graphics resources (textures, meshes, etc.).";
        
        // ===== 按钮和操作文本 =====
        
        public const string CopyToClipboard = "Copy To Clipboard";
        public const string SelectInEditor = "Select in Editor";
        public const string SearchInProject = "Search In Project";
        public const string SearchInScene = "Search In Scene";
        public const string ExportData = "Export Data";

        // ===== GCHandle存活性分析 (参考: Unity.MemoryProfiler.Editor.UIContentData.TextContent Line 164-174) =====
        
        /// <summary>
        /// 被Native代码使用的空数组（UsedByNativeCode属性）
        /// 参考: Unity Line 164-165
        /// </summary>
        public const string UsedByNativeCodeStatus = "Empty Array Required by Unity's subsystems";
        public const string UsedByNativeCodeHint = "This array's Type is marked with a [UsedByNativeCode] or [RequiredByNativeCode] Attribute in the Unity code-base and the array exists so that the Type is not compiled out on build. It is held in memory via a GCHandle. You can search the public C# reference repository for those attributes https://github.com/Unity-Technologies/UnityCsReference/.";

        /// <summary>
        /// 被GCHandle持有的对象
        /// 参考: Unity Line 167-168
        /// </summary>
        public const string HeldByGCHandleStatus = "Held Alive By GCHandle";
        public const string HeldByGCHandleHint = "This Object is pinned or otherwise held in memory because a GCHandle was allocated for it.";

        /// <summary>
        /// Unity对象被GCHandle持有
        /// 参考: Unity Line 170-171
        /// </summary>
        public const string UnityObjectHeldByGCHandleStatus = "Unity Object Held Alive By GCHandle";
        public const string UnityObjectHeldByGCHandleHint = "This Object has an m_CachedPtr field, indicating it is a Unity Object, yet no native object holding its GCHandle was captured in the snapshot. This could be due to a bug where native objects could change between taking a snapshot of the managed heap and capturing native objects. This was fixed in 6000.0.16f1, 2022.3.43f1, and 2021.3.44f1 so if you are on Unity versions higher than those, please report a bug.";

        /// <summary>
        /// 未知的存活原因（Bug）
        /// 参考: Unity Line 173-174
        /// </summary>
        public const string UnkownLivenessReasonStatus = "Bug: Liveness Reason Unknown";
        public const string UnkownLivenessReasonHint = "There is no reference pointing to this object and no GCHandle reported for it. This is a Bug, please report it using 'Help > Report a Bug' and attach the snapshot to the report.";

        // ===== Native Allocation相关 (参考: Unity.MemoryProfiler.Editor.UIContentData.TextContent Line 178-193) =====
        
        /// <summary>
        /// Unknown Unknown Allocations错误提示
        /// 参考: Unity Line 178-183
        /// </summary>
        public const string UnknownUnknownAllocationsErrorBoxMessage = "This is a bug in the native code of the engine, please file a bug report. " +
            "Chances are high that every single allocation here is a separate bug to be fixed by a different team and should be treated as such. " +
            "By their very nature, User facing releases lack the information needed to differentiate these allocations in a meaningful way. " +
            "To get an approximation of what constitutes a separate vs a duplicate bug the byte size of each allocation should be used (size in byte is given when hovering the Native Size). " +
            "\nNote: It is likely that this memory is actually needed, but without it being properly rooted, there is no way to tell." +
            "\nUnity's staff is making a best effort attempt to catch these internally, but there are a myriad of ways of using the engine so that it is impossible to catch all possible scenarios.";

        /// <summary>
        /// Unknown Unknown Allocations错误提示（内部模式）
        /// 参考: Unity Line 185-186
        /// </summary>
        public const string UnknownUnknownAllocationsErrorBoxMessageInternalMode = UnknownUnknownAllocationsErrorBoxMessage +
            "\n\nThose with access to Unity's source code can compile the engine with ENABLE_STACKS_ON_ALL_ALLOCS set to 1 in MemoryProfiler.h to see where in the code base it was allocated from, and with that info disambiguate the issue further. ";

        /// <summary>
        /// Native Allocation Found References提示
        /// 参考: Unity Line 188-190
        /// </summary>
        public const string NativeAllocationFoundReferencesHint =
            "Only references from Managed Objects to this allocation are found. References from the stack or native code are not found. " +
            "0 'Found References' does not necessarily mean that this allocation is leaked.";

        /// <summary>
        /// Native Allocation Internal Mode CallStacks提示
        /// 参考: Unity Line 192-193
        /// </summary>
        public const string NativeAllocationInternalModeCallStacksInfoBoxMessage =
            "If you have access to Unity's source code, you can compile the engine with ENABLE_STACKS_ON_ALL_ALLOCS set to 1 in MemoryProfiler.h to see where this allocation was made.";

        // ===== Graphics Resource相关 (参考: Unity.MemoryProfiler.Editor.UIContentData.TextContent Line 199-204) =====
        
        /// <summary>
        /// Unrooted Graphics Resource错误提示
        /// 参考: Unity Line 199-200
        /// </summary>
        public const string UnrootedGraphcisResourceErrorBoxMessage =
            "This graphics resource was not associated to a root in Unity's native code. This is a bug in the engine, please file a bug report.";

        /// <summary>
        /// Graphics Resource with Snapshot with CallStacks提示
        /// 参考: Unity Line 202-204
        /// </summary>
        public const string GraphcisResourceWithSnapshotWithCallStacksInfoBoxMessage =
            "Callstacks are not reported for Gfx Resources themselves, so any callstacks listed here relate to their root. " +
            "Use the Gfx Resource ID and the native define ENABLE_DEBUG_GFXALLOCATION_CALLSTACK_INFO in MemoryProfiler.h to get more details when debugging Unity's Native Code.";

        // ===== Unity Object Status/Hint相关 (参考: Unity.MemoryProfiler.Editor.UIContentData.TextContent Line 244-247) =====
        
        /// <summary>
        /// Leaked Managed Shell名称
        /// 参考: Unity Line 246
        /// </summary>
        public const string LeakedManagedShellName = "Leaked Managed Shell";

        // ===== Invalid Object相关 (参考: Unity.MemoryProfiler.Editor.UIContentData.TextContent Line 176) =====
        
        /// <summary>
        /// Invalid Object错误提示
        /// 参考: Unity Line 176
        /// </summary>
        public const string InvalidObjectErrorBoxMessage = "This is an invalid Managed Object, i.e. the Memory Profiler could not identify it's type and data. To help us in finding and fixing this issue, " + InvalidObjectPleaseReportABugMessage;

        // ===== 文档链接 =====
        
        public const string DocumentationBaseUrl = "https://docs.unity3d.com/Packages/com.unity.memoryprofiler@latest/";
        public const string DocumentationMemoryCategories = DocumentationBaseUrl + "manual/memory-categories.html";
        public const string DocumentationUnityObjects = DocumentationBaseUrl + "manual/unity-objects.html";
        public const string DocumentationAllOfMemory = DocumentationBaseUrl + "manual/all-of-memory.html";
    }
}

