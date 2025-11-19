namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 内存类别枚举类型
    /// 参考: Unity.MemoryProfiler.Editor.UI.IAnalysisViewSelectable.Category
    /// </summary>
    public enum CategoryType
    {
        None = 0,
        Native,
        NativeReserved,
        Managed,
        ManagedReserved,
        ExecutablesAndMapped,
        Graphics,
        GraphicsDisabled,
        GraphicsReserved,
        Unknown,
        UnknownEstimated,
        AndroidRuntime,
        // 动态ID从这里开始
        FirstDynamicId
    }
}

