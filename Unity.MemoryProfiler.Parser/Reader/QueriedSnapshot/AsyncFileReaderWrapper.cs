using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.MemoryProfiler.Editor.Containers;
#if ENABLE_CORECLR
using Allocator = Unity.Collections.AllocatorManager;
using AllocatorType = Unity.Collections.AllocatorManager.AllocatorHandle;
using static Unity.Collections.AllocatorManager;
#else
using Allocator = Unity.Collections.Allocator;
using AllocatorType = Unity.Collections.Allocator;
#endif

namespace Unity.MemoryProfiler.Editor.Format.QueriedSnapshot
{
    /// <summary>
    /// AsyncFileReaderWrapper - 包装FileReader提供真正的异步支持
    /// 
    /// 设计目标：
    /// 1. 分块读取大文件（1MB chunks）避免UI冻结
    /// 2. 报告进度到UI（IProgress<FileReadProgress>）
    /// 3. 支持取消操作（CancellationToken）
    /// 4. 向后兼容现有同步API
    /// 
    /// Unity官方实现参考：Unity.MemoryProfiler.Editor.Format.QueriedSnapshot.FileReader
    /// </summary>
    internal class AsyncFileReaderWrapper : IAsyncFileReader
    {
        private readonly FileReader _innerReader;
        private const int ChunkSize = 1024 * 1024; // 1MB chunks
        
        public AsyncFileReaderWrapper()
        {
            _innerReader = new FileReader();
        }
        
        #region IFileReader Synchronous Interface (Forward to inner reader)
        
        public bool HasOpenFile => _innerReader.HasOpenFile;
        
        public uint FormatVersionNumeric => _innerReader.FormatVersionNumeric;
        
        public string FullPath => _innerReader.FullPath;
        
        public FormatVersion FormatVersion => _innerReader.FormatVersion;
        
        public ReadError Open(string filePath)
        {
            return _innerReader.Open(filePath);
        }
        
        public EntryFormat GetEntryFormat(EntryType type)
        {
            return _innerReader.GetEntryFormat(type);
        }
        
        public long GetSizeForEntryRange(EntryType entry, long offset, long count, bool includeOffsets = true)
        {
            return _innerReader.GetSizeForEntryRange(entry, offset, count, includeOffsets);
        }
        
        public uint GetEntryCount(EntryType entry)
        {
            return _innerReader.GetEntryCount(entry);
        }
        
        public void GetEntryOffsets(EntryType entry, DynamicArray<long> buffer)
        {
            _innerReader.GetEntryOffsets(entry, buffer);
        }
        
        public GenericReadOperation Read(EntryType entry, DynamicArray<byte> buffer, long offset, long count, bool includeOffsets = true)
        {
            return _innerReader.Read(entry, buffer, offset, count, includeOffsets);
        }
        
        public GenericReadOperation Read(EntryType entry, long offset, long count, AllocatorType allocator, bool includeOffsets = true)
        {
            return _innerReader.Read(entry, offset, count, allocator, includeOffsets);
        }
        
        public unsafe ReadError ReadUnsafe(EntryType entry, void* buffer, long bufferLength, long offset, long count, bool includeOffsets = true)
        {
            return _innerReader.ReadUnsafe(entry, buffer, bufferLength, offset, count, includeOffsets);
        }
        
        public GenericReadOperation AsyncRead(EntryType entry, long offset, long count, AllocatorType allocator, bool includeOffsets = true)
        {
            return _innerReader.AsyncRead(entry, offset, count, allocator, includeOffsets);
        }
        
        public GenericReadOperation AsyncRead(EntryType entry, DynamicArray<byte> buffer, long offset, long count, bool includeOffsets = true)
        {
            return _innerReader.AsyncRead(entry, buffer, offset, count, includeOffsets);
        }
        
        public NestedDynamicSizedArrayReadOperation<T> AsyncReadDynamicSizedArray<T>(EntryType entry, long offset, long count, AllocatorType allocator) where T : unmanaged
        {
            return _innerReader.AsyncReadDynamicSizedArray<T>(entry, offset, count, allocator);
        }
        
        public void Close()
        {
            _innerReader.Close();
        }
        
        public void Dispose()
        {
            _innerReader?.Dispose();
        }
        
        #endregion
        
        #region IAsyncFileReader Asynchronous Interface
        
        /// <summary>
        /// 异步打开文件
        /// Unity官方实现：FileReader.Open() - 同步版本
        /// WPF实现：包装在Task中，添加进度报告
        /// </summary>
        public async Task<ReadError> OpenAsync(
            string filePath,
            IProgress<FileReadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                // 报告开始
                progress?.Report(new FileReadProgress
                {
                    BytesRead = 0,
                    TotalBytes = 100,
                    CurrentEntryType = "Opening file"
                });
                
                var result = _innerReader.Open(filePath);
                
                // 报告完成
                progress?.Report(new FileReadProgress
                {
                    BytesRead = 100,
                    TotalBytes = 100,
                    CurrentEntryType = "File opened"
                });
                
                return result;
            }, cancellationToken);
        }
        
        /// <summary>
        /// 异步读取数据 - 分块读取大数据
        /// Unity官方实现：FileReader.Read() - 使用Unity JobSystem
        /// WPF实现：使用Task + 分块读取 + 进度报告
        /// </summary>
        public async Task<GenericReadOperation> ReadAsync(
            EntryType entry,
            DynamicArray<byte> buffer,
            long offset,
            long count,
            bool includeOffsets = true,
            IProgress<FileReadProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            return await Task.Run(() =>
            {
                // 计算总大小
                var totalSize = _innerReader.GetSizeForEntryRange(entry, offset, count, includeOffsets);
                
                // 如果数据量小，直接同步读取
                if (totalSize < ChunkSize)
                {
                    progress?.Report(new FileReadProgress
                    {
                        BytesRead = 0,
                        TotalBytes = totalSize,
                        CurrentEntryType = entry.ToString()
                    });
                    
                    var result = _innerReader.Read(entry, buffer, offset, count, includeOffsets);
                    
                    progress?.Report(new FileReadProgress
                    {
                        BytesRead = totalSize,
                        TotalBytes = totalSize,
                        CurrentEntryType = entry.ToString()
                    });
                    
                    return result;
                }
                
                // 大数据量：分块读取
                return ReadInChunks(entry, buffer, offset, count, includeOffsets, totalSize, progress, cancellationToken);
                
            }, cancellationToken);
        }
        
        /// <summary>
        /// 分块读取大数据
        /// </summary>
        private GenericReadOperation ReadInChunks(
            EntryType entry,
            DynamicArray<byte> buffer,
            long offset,
            long count,
            bool includeOffsets,
            long totalSize,
            IProgress<FileReadProgress> progress,
            CancellationToken cancellationToken)
        {
            // 计算分块数
            var chunkCount = (int)((count + ChunkSize - 1) / ChunkSize);
            var bytesRead = 0L;
            
            var sw = Stopwatch.StartNew();
            
            // 逐块读取
            for (int i = 0; i < chunkCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                var chunkOffset = offset + i * ChunkSize;
                var chunkSize = Math.Min(ChunkSize, count - i * ChunkSize);
                
                // 创建临时buffer for this chunk
                using (var chunkBuffer = new DynamicArray<byte>((int)chunkSize, Allocator.Temp))
                {
                    var op = _innerReader.Read(entry, chunkBuffer, chunkOffset, chunkSize, includeOffsets);
                    if (op.Error != ReadError.Success)
                    {
                        return op; // 返回错误
                    }
                    
                    // 复制到目标buffer
                    unsafe
                    {
                        System.Buffer.MemoryCopy(
                            chunkBuffer.GetUnsafePtr(),
                            (byte*)buffer.GetUnsafePtr() + bytesRead,
                            buffer.Count - bytesRead,
                            chunkSize);
                    }
                    
                    bytesRead += chunkSize;
                }
                
                // 报告进度 (每100ms或每10%报告一次)
                if (sw.ElapsedMilliseconds > 100 || (i % Math.Max(1, chunkCount / 10)) == 0)
                {
                    progress?.Report(new FileReadProgress
                    {
                        BytesRead = bytesRead,
                        TotalBytes = totalSize,
                        CurrentEntryType = entry.ToString()
                    });
                    sw.Restart();
                }
            }
            
            // 最终进度报告
            progress?.Report(new FileReadProgress
            {
                BytesRead = totalSize,
                TotalBytes = totalSize,
                CurrentEntryType = entry.ToString()
            });
            
            // 返回完成的操作
            var result = new GenericReadOperation(default, buffer);
            result.Error = ReadError.Success;
            return result;
        }
        
        #endregion
    }
}

