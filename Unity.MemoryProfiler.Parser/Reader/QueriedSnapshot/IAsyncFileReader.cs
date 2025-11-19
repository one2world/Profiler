using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.MemoryProfiler.Editor.Containers;

namespace Unity.MemoryProfiler.Editor.Format.QueriedSnapshot
{
    /// <summary>
    /// 异步文件读取器接口
    /// 用于大文件加载时提供进度报告和取消支持
    /// </summary>
    internal interface IAsyncFileReader : IFileReader
    {
        /// <summary>
        /// 异步打开文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <param name="progress">进度报告</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读取错误码</returns>
        Task<ReadError> OpenAsync(
            string filePath,
            IProgress<FileReadProgress> progress = null,
            CancellationToken cancellationToken = default);
        
        /// <summary>
        /// 异步读取数据到DynamicArray
        /// </summary>
        /// <param name="entry">Entry类型</param>
        /// <param name="buffer">目标缓冲区</param>
        /// <param name="offset">偏移量</param>
        /// <param name="count">数量</param>
        /// <param name="includeOffsets">是否包含偏移信息</param>
        /// <param name="progress">进度报告</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>读取操作结果</returns>
        Task<GenericReadOperation> ReadAsync(
            EntryType entry,
            DynamicArray<byte> buffer,
            long offset,
            long count,
            bool includeOffsets = true,
            IProgress<FileReadProgress> progress = null,
            CancellationToken cancellationToken = default);
    }
}

