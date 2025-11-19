using Unity.MemoryProfiler.UI.UIContent;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 内存类别描述文本
    /// 参考: Unity.MemoryProfiler.Editor.UIContentData.TextContent
    /// 现在直接使用Unity.MemoryProfiler.UI.UIContent.TextContent
    /// </summary>
    public static class CategoryDescriptions
    {
        public static string NativeDescription => TextContent.NativeDescription;

        public static string ManagedDescription => TextContent.ManagedDescription;

        public static string ExecutablesAndMappedDescription => TextContent.ExecutablesAndMappedDescription;

        public static string GraphicsEstimatedDescription => TextContent.GraphicsEstimatedDescription;

        public static string GraphicsEstimatedDisabledDescription => TextContent.GraphicsEstimatedDisabledDescription;

        public static string UntrackedDescription => TextContent.UntrackedDescription;

        public static string UntrackedEstimatedDescription => TextContent.UntrackedEstimatedDescription;

        public static string AndroidRuntimeDescription => TextContent.AndroidRuntimeDescription;

        public static string NativeReservedDescription => TextContent.NativeReservedDescription;

        public static string ManagedReservedDescription => TextContent.ManagedReservedDescription;

        public static string GraphicsReservedDescription => TextContent.GraphicsReservedDescription;

        public static string SystemMemoryRegionDescription => TextContent.SystemMemoryRegionDescription;

        public static string ManagedMemoryHeapDescription => TextContent.ManagedMemoryHeapDescription;

        public static string NativeMemoryRegionDescription => TextContent.NativeMemoryRegionDescription;

        public static string NativeAllocationDescription => TextContent.NativeAllocationDescription;

        public static string NonTypedGroupDescription => TextContent.NonTypedGroupDescription;

        /// <summary>
        /// 根据Category获取描述
        /// </summary>
        public static string GetDescription(CategoryType category)
        {
            return category switch
            {
                CategoryType.None => string.Empty,
                CategoryType.Native => NativeDescription,
                CategoryType.Managed => ManagedDescription,
                CategoryType.ExecutablesAndMapped => ExecutablesAndMappedDescription,
                CategoryType.Graphics => GraphicsEstimatedDescription,
                CategoryType.GraphicsDisabled => GraphicsEstimatedDisabledDescription,
                CategoryType.Unknown => UntrackedDescription,
                CategoryType.UnknownEstimated => UntrackedEstimatedDescription,
                CategoryType.AndroidRuntime => AndroidRuntimeDescription,
                CategoryType.NativeReserved => NativeReservedDescription,
                CategoryType.ManagedReserved => ManagedReservedDescription,
                CategoryType.GraphicsReserved => GraphicsReservedDescription,
                _ => string.Empty
            };
        }
    }
}

