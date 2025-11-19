namespace Unity.MemoryProfiler.UI.ModelBuilders.Comparison
{
    /// <summary>
    /// 可对比项数据接口
    /// 等价于Unity的IPrivateComparableItemData
    /// </summary>
    public interface IComparableItemData
    {
        /// <summary>
        /// 项名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 项大小（Committed字节数）
        /// </summary>
        ulong SizeInBytes { get; }
    }
}

