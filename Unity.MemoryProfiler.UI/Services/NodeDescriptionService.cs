using System;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// 节点描述服务 - 提供所有节点类型的描述文本和文档链接
    /// 参考: Unity的TextContent和BreakdownDetailsViewControllerFactory
    /// </summary>
    internal static class NodeDescriptionService
    {
        // 文档基础URL
        private const string DocumentationBaseUrl = "https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.1/manual/";

        /// <summary>
        /// 预定义类别枚举（对应Unity的IAnalysisViewSelectable.Category）
        /// </summary>
        public enum Category
        {
            None = 0,
            Native = 1,
            NativeReserved = 2,
            Managed = 3,
            ManagedReserved = 4,
            ExecutablesAndMapped = 5,
            Graphics = 6,
            GraphicsDisabled = 7,
            GraphicsReserved = 8,
            Unknown = 9,
            UnknownEstimated = 10,
            AndroidRuntime = 11,
            FirstDynamicId = 12
        }

        /// <summary>
        /// 判断itemId是否为预定义类别
        /// </summary>
        public static bool IsPredefinedCategory(int itemId)
        {
            return itemId > 0 && itemId < (int)Category.FirstDynamicId;
        }

        /// <summary>
        /// 获取节点描述
        /// </summary>
        public static string GetDescription(int itemId, string name, bool isGroupNode)
        {
            // 第一优先级：预定义类别
            if (IsPredefinedCategory(itemId))
            {
                return GetPredefinedCategoryDescription((Category)itemId);
            }

            // 第二优先级：根据名称匹配已知分组和类别
            var knownDescription = GetKnownNodeDescription(name);
            if (!string.IsNullOrEmpty(knownDescription))
            {
                return knownDescription;
            }

            // 第三优先级：通用分组描述
            if (isGroupNode)
            {
                return $"A group of related memory items. This group contains memory allocations and objects organized for analysis purposes.";
            }

            // 默认描述
            return "This item represents a collection of memory allocations or objects in the memory snapshot.";
        }

        /// <summary>
        /// 获取文档URL
        /// </summary>
        public static string? GetDocumentationUrl(int itemId, string name)
        {
            if (IsPredefinedCategory(itemId))
            {
                return GetPredefinedCategoryDocUrl((Category)itemId);
            }

            return GetKnownNodeDocUrl(name);
        }

        #region 预定义类别描述

        private static string GetPredefinedCategoryDescription(Category category)
        {
            return category switch
            {
                Category.Native => 
                    "Memory allocated by Unity's native (C++) engine and subsystems. " +
                    "This includes Unity objects, native allocations, and subsystem memory.",

                Category.NativeReserved => 
                    "Memory reserved by Unity's native allocator but not yet in use. " +
                    "This is pre-allocated memory to avoid frequent system calls and improve performance.",

                Category.Managed => 
                    "Memory allocated from the managed heap for C# objects and data structures. " +
                    "This is memory managed by Unity's Scripting VM (Virtual Machine) and the garbage collector.",

                Category.ManagedReserved => 
                    "Memory reserved by the C# garbage collector for future allocations. " +
                    "This memory is allocated from the system but not yet used for managed objects.",

                Category.ExecutablesAndMapped => 
                    "Memory used by executable files and memory-mapped files. " +
                    "This includes the Unity engine binary, game code, and mapped assets.",

                Category.Graphics => 
                    "Estimated memory used by graphics resources such as textures, meshes, shaders, and render targets. " +
                    "This is an approximation based on resource metadata.",

                Category.GraphicsDisabled => 
                    "Graphics resource tracking is disabled. " +
                    "Enable it in the Memory Profiler settings to see detailed graphics memory usage.",

                Category.GraphicsReserved => 
                    "Memory reserved by the graphics driver for future allocations. " +
                    "This is pre-allocated memory managed by the graphics subsystem.",

                Category.Unknown => 
                    "Memory that is not tracked by the Memory Profiler. " +
                    "This typically includes system allocations, third-party libraries, and memory allocated outside Unity's tracking systems.",

                Category.UnknownEstimated => 
                    "Estimated untracked memory calculated as the difference between total system memory usage and tracked memory. " +
                    "This provides an approximation of memory used by components not directly tracked by the profiler.",

                Category.AndroidRuntime => 
                    "Memory used by the Android runtime environment, including the Dalvik or ART virtual machine. " +
                    "This is specific to Android platform builds.",

                _ => "Memory category information."
            };
        }

        private static string? GetPredefinedCategoryDocUrl(Category category)
        {
            return category switch
            {
                Category.Native => DocumentationBaseUrl + "native-memory.html",
                Category.Managed => DocumentationBaseUrl + "managed-memory.html",
                Category.Graphics => DocumentationBaseUrl + "graphics-memory.html",
                Category.ExecutablesAndMapped => DocumentationBaseUrl + "executables-and-mapped.html",
                _ => DocumentationBaseUrl + "memory-categories.html"
            };
        }

        #endregion

        #region 已知节点描述（动态节点）

        private static string? GetKnownNodeDescription(string name)
        {
            return name switch
            {
                // Native分组
                "Native Objects" => 
                    "Unity Engine native C++ objects that are accounted for in the Memory Profiler Module. " +
                    "This includes all native objects created by Unity's engine, such as GameObject components, Assets, and internal engine structures.",

                "Unity Subsystems" => 
                    "Memory used by Unity's subsystems and internal data structures. " +
                    "This includes memory allocated by various Unity systems such as the Scripting VM, Graphics Driver, Physics, Audio, and other engine subsystems.",

                "Native Reserved" => 
                    "Memory reserved by Unity's native memory allocators but not currently in use. " +
                    "This memory is pre-allocated to improve allocation performance and reduce fragmentation.",

                // Managed分组
                "Managed Objects" => 
                    "C# Managed Objects allocated on the managed heap. " +
                    "This includes all objects created in C# scripts, such as class instances, arrays, and boxed value types.",

                "Virtual Machine" => 
                    "Memory used by Unity's Scripting VM (Virtual Machine), including internal data structures, code, and execution stacks. " +
                    "This is the overhead of running C# code in Unity.",

                "Managed Reserved" => 
                    "Memory reserved by the managed heap allocator for future allocations. " +
                    "The garbage collector pre-allocates this memory to avoid frequent system calls.",

                // Graphics相关
                "Graphics Resources" => 
                    "Graphics resources such as textures, meshes, shaders, materials, and render targets. " +
                    "These are GPU resources that consume video memory.",

                "Graphics Driver" => 
                    "Memory used by the graphics driver for command buffers, state management, and driver-internal structures.",

                // Executables
                "Executables & Mapped" => 
                    "Memory used by executable files and memory-mapped files. " +
                    "This includes the Unity engine binary, game code (DLLs), and assets loaded via memory mapping.",

                // Unity特定子系统
                "Unity.GfxDriver" => 
                    "Memory allocated by Unity's Graphics Driver subsystem for rendering operations. " +
                    "This includes command buffers, render states, and graphics API wrappers.",

                "Unity.Profiler" => 
                    "Memory used by Unity's Profiler system for collecting and storing performance data.",

                "Unity.Serialization" => 
                    "Memory used by Unity's Serialization system for reading and writing data.",

                "Unity.Loading" => 
                    "Memory used during asset loading operations, including temporary buffers and decompression.",

                // 托管堆相关
                "Managed Heap" => 
                    "Memory allocated from the managed heap for C# objects and data structures. " +
                    "This represents the active memory used by your C# scripts and Unity's managed code.",

                "Managed Stack" => 
                    "Memory used by the managed execution stack for method calls, local variables, and parameters.",

                // 其他常见节点
                "Empty Heap Memory" => 
                    "Unused memory within allocated managed heap sections. " +
                    "This memory is reserved but not currently occupied by managed objects.",

                "System Memory Region" => 
                    "A memory region allocated directly from the operating system. " +
                    "These are large memory blocks allocated using system APIs.",

                "Native Memory Region" => 
                    "A memory region used by Unity's native allocators. " +
                    "These regions are subdivided for individual allocations.",

                _ => null
            };
        }

        private static string? GetKnownNodeDocUrl(string name)
        {
            return name switch
            {
                "Native Objects" => DocumentationBaseUrl + "native-objects.html",
                "Managed Objects" => DocumentationBaseUrl + "managed-objects.html",
                "Virtual Machine" => DocumentationBaseUrl + "virtual-machine.html",
                "Unity.GfxDriver" => DocumentationBaseUrl + "graphics-memory.html",
                "Graphics Resources" => DocumentationBaseUrl + "graphics-memory.html",
                "Managed Heap" => DocumentationBaseUrl + "managed-memory.html",
                _ => null
            };
        }

        #endregion

        /// <summary>
        /// 获取Source类型的描述（当Source有效但itemId不是预定义类别时）
        /// 参考: BreakdownDetailsViewControllerFactory的source.Id switch
        /// </summary>
        public static string GetSourceTypeDescription(Editor.CachedSnapshot.SourceIndex.SourceId sourceId)
        {
            return sourceId switch
            {
                Editor.CachedSnapshot.SourceIndex.SourceId.SystemMemoryRegion =>
                    "A memory region allocated from the operating system using system APIs such as VirtualAlloc or mmap.",

                Editor.CachedSnapshot.SourceIndex.SourceId.ManagedHeapSection =>
                    "A section of the managed heap used for C# object allocations. " +
                    "The managed heap is divided into multiple sections to improve allocation performance.",

                Editor.CachedSnapshot.SourceIndex.SourceId.NativeMemoryRegion =>
                    "A memory region used by Unity's native allocators. " +
                    "These regions are managed by Unity's memory management system and subdivided for individual allocations.",

                Editor.CachedSnapshot.SourceIndex.SourceId.NativeAllocation =>
                    "A native memory allocation made by Unity's allocator. " +
                    "This represents a specific memory block allocated for a particular purpose.",

                Editor.CachedSnapshot.SourceIndex.SourceId.NativeObject =>
                    "A native Unity object instance created by Unity's engine. " +
                    "This includes components, assets, and internal engine objects.",

                Editor.CachedSnapshot.SourceIndex.SourceId.ManagedObject =>
                    "A managed object instance created in C# code. " +
                    "This is allocated on the managed heap and managed by the garbage collector.",

                Editor.CachedSnapshot.SourceIndex.SourceId.GfxResource =>
                    "A graphics resource such as a texture, mesh, shader, or render target. " +
                    "These resources are typically stored in GPU memory.",

                Editor.CachedSnapshot.SourceIndex.SourceId.NativeRootReference =>
                    "A root reference in the native memory system. " +
                    "This is a starting point for memory traversal and prevents objects from being garbage collected.",

                Editor.CachedSnapshot.SourceIndex.SourceId.NativeType =>
                    "A native type definition in Unity's engine. " +
                    "This represents the type information for native C++ classes.",

                Editor.CachedSnapshot.SourceIndex.SourceId.ManagedType =>
                    "A managed type definition in C#. " +
                    "This represents the type information for C# classes, structs, and interfaces.",

                _ => "A memory item in the snapshot."
            };
        }
    }
}

