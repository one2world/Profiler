using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityPerfProfilerWPF.Utils;
using UnityPerfProfilerWPF.Unity;

namespace UnityPerfProfilerWPF.Services
{
    public interface IDataStorageService
    {
        // Unity官方风格的CollectAndCompress方法
        Task<string?> CollectAndCompressAsync(int sequence, ProfilerMessage message, byte[] sessionId);
        
        // Chunked data handling (Memory snapshots)
        Task BeginSnapshotAsync(int sequence, byte[] sessionId);
        Task<string?> AppendSnapshotDataAsync(ProfilerMessage message, int sequence, byte[] sessionId);
        Task<string?> CompleteSnapshotAsync(int sequence);
        
        // Chunked profiler data handling  
        Task BeginProfilerDataAsync(int sequence, byte[] sessionId);
        Task<string?> AppendProfilerDataAsync(ProfilerMessage message, int sequence, byte[] sessionId);
        Task<string?> CompleteProfilerDataAsync(int sequence);
        
        // Individual message handling
        Task<string?> SaveMemorySnapshotAsync(ProfilerMessage message, int sequence, byte[] sessionId);
        Task<string?> SaveObjectSnapshotAsync(ProfilerMessage message, int sequence, byte[] sessionId);
        Task<string?> SavePerformanceDataAsync(ProfilerMessage message, DateTime timestamp);
        Task<string?> SaveLogMessageAsync(ProfilerMessage message, DateTime timestamp);
        Task<string?> SaveFileTransferAsync(ProfilerMessage message, DateTime timestamp);
        Task<string?> SaveLuaProfileDataAsync(ProfilerMessage message, DateTime timestamp);
        Task<string?> SaveRenderDocCaptureAsync(byte[] data, DateTime timestamp);
        Task<string?> SaveUnknownMessageAsync(ProfilerMessage message, DateTime timestamp);
        
        string GetOutputDirectory();
        void SetOutputDirectory(string directory);
    }

    public class DataStorageService : IDataStorageService
    {
        private readonly ILogger<DataStorageService> _logger;
        private string _outputDirectory;
        
        // Memory snapshots chunked handling
        private readonly Dictionary<int, FileStream> _activeSnapshots = new();
        private readonly Dictionary<int, string> _snapshotFilePaths = new();
        private readonly object _snapshotLock = new object();
        
        // Profiler data chunked handling
        private readonly Dictionary<int, FileStream> _activeProfilerData = new();
        private readonly Dictionary<int, string> _profilerDataFilePaths = new();
        private readonly object _profilerDataLock = new object();
        
        // Unity Log aggregation - 合并Log消息到单一文件而非每消息一文件
        private FileStream? _logFileStream;
        private StreamWriter? _logWriter;
        private string? _logFilePath;
        private readonly object _logLock = new object();

        public DataStorageService(ILogger<DataStorageService> logger)
        {
            _logger = logger;
            // 改为项目根目录下的Captures文件夹，便于测试和调试
            var projectRoot = GetProjectRootDirectory();
            _outputDirectory = Path.Combine(projectRoot, "Captures");
            EnsureOutputDirectoryExists();
        }

        public string GetOutputDirectory() => _outputDirectory;

        public void SetOutputDirectory(string directory)
        {
            _outputDirectory = directory;
            EnsureOutputDirectoryExists();
        }

        private void EnsureOutputDirectoryExists()
        {
            try
            {
                if (!Directory.Exists(_outputDirectory))
                {
                    Directory.CreateDirectory(_outputDirectory);
                    _logger.LogInformation("Created output directory: {Directory}", _outputDirectory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create output directory: {Directory}", _outputDirectory);
                throw;
            }
        }

        private static string GetProjectRootDirectory()
        {
            // 从当前执行程序的位置往上找到项目根目录
            var currentDir = Directory.GetCurrentDirectory();
            var dir = new DirectoryInfo(currentDir);
            
            // 查找包含.sln或.csproj文件的目录
            while (dir != null)
            {
                if (dir.GetFiles("*.sln").Length > 0 || 
                    dir.GetFiles("UnityPerfProfilerWPF.csproj").Length > 0)
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            
            // 如果找不到项目根目录，返回当前目录
            return currentDir;
        }

        /// <summary>
        /// 完全按照Unity官方FileUtils.CollectAndCompress的逻辑实现
        /// 每个消息ID=43的消息都调用此方法，追加数据并检查结束标记
        /// Unity现在发送正确的snap文件格式，无需转换！
        /// </summary>
        public async Task<string?> CollectAndCompressAsync(int sequence, ProfilerMessage message, byte[] sessionId)
        {
            try
            {
                // 1. 获取或创建临时文件路径（Unity官方逻辑）
                var tempFile = GetTempMemoryFilePath(sequence, sessionId);
                
                // 2. 获取消息数据
                var msgData = message.GetData();
                
                _logger.LogTrace("CollectAndCompress: sequence={Sequence}, dataSize={Size}, file={File}", 
                    sequence, msgData.Length, tempFile);
                
                // 3. 追加数据到文件（Unity官方使用FileMode.Append）
                using (var stream = new FileStream(tempFile, FileMode.Append, FileAccess.Write))
                {
                    await stream.WriteAsync(msgData, 0, msgData.Length);
                    await stream.FlushAsync();
                }
                
                // 4. 检查是否为最后一块数据（Unity官方的结束标记检测）
                if (msgData.Length >= 4)
                {
                    var endIndex = msgData.Length - 4;
                    if (msgData[endIndex] == 175 && msgData[endIndex + 1] == 234 && 
                        msgData[endIndex + 2] == 94 && msgData[endIndex + 3] == 134)
                    {
                        // 找到结束标记，Unity现在发送标准snap文件格式！
                        var fileInfo = new FileInfo(tempFile);
                        _logger.LogInformation("Memory snapshot collection completed: {FilePath} ({Size:N0} bytes)", 
                            tempFile, fileInfo.Length);
                        
                        return tempFile;
                    }
                }
                
                // 未找到结束标记，继续等待更多数据
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect and compress memory snapshot data for sequence {Sequence}", sequence);
                throw;
            }
        }

        /// <summary>
        /// 将Unity协议消息格式转换为标准的Unity snap文件格式
        /// 这是关键的转换步骤，确保生成的文件能被Unity Memory Profiler正确识别
        /// </summary>
        private async Task<string> ConvertProtocolMessagesToSnapAsync(string protocolFile, int sequence, byte[] sessionId)
        {
            try
            {
                _logger.LogInformation("Converting protocol messages to Unity snap format...");
                
                // 读取所有协议消息数据
                var protocolData = await File.ReadAllBytesAsync(protocolFile);
                
                // 提取纯净的内存快照数据（跳过Unity协议消息头）
                var snapData = ExtractPureSnapshotData(protocolData);
                
                // 创建标准的Unity snap文件
                var snapFile = GetFinalSnapFilePath(sequence, sessionId);
                await CreateUnitySnapFileAsync(snapFile, snapData);
                
                // 删除临时的协议消息文件
                File.Delete(protocolFile);
                
                _logger.LogInformation("Successfully converted to Unity snap format: {SnapFile} ({Size:N0} bytes)", 
                    snapFile, snapData.Length);
                
                return snapFile;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to convert protocol messages to snap format");
                throw;
            }
        }

        /// <summary>
        /// 从Unity协议消息中提取纯净的内存快照数据
        /// Unity协议消息格式: [Magic(4)][GUID(16)][Size(4)][Data(variable)]
        /// 我们需要提取所有Data部分并合并
        /// </summary>
        private byte[] ExtractPureSnapshotData(byte[] protocolData)
        {
            var pureData = new List<byte>();
            var offset = 0;
            
            // Unity协议消息的Magic Number: 0x8F4EA567 的各种变体
            var knownMagicNumbers = new uint[]
            {
                0x8F4EA567, // 标准Magic
                0x674EA58F, // 字节序变体
                0xFABCED01, // 观察到的实际值
                0x01EDBC1F  // 可能的变体
            };
            
            while (offset < protocolData.Length - 24) // 至少需要24字节(Magic+GUID+Size)
            {
                if (offset + 4 > protocolData.Length) break;
                
                var magic = BitConverter.ToUInt32(protocolData, offset);
                
                // 检查是否是已知的Magic Number
                if (Array.Exists(knownMagicNumbers, m => m == magic))
                {
                    // 跳过Magic(4) + GUID(16) = 20字节
                    if (offset + 24 > protocolData.Length) break;
                    
                    var dataSize = BitConverter.ToUInt32(protocolData, offset + 20);
                    
                    // 验证数据大小是否合理
                    if (dataSize > 0 && dataSize <= protocolData.Length - offset - 24)
                    {
                        // 提取纯净数据部分
                        var messageData = new byte[dataSize];
                        Array.Copy(protocolData, offset + 24, messageData, 0, (int)dataSize);
                        pureData.AddRange(messageData);
                        
                        offset += 24 + (int)dataSize;
                    }
                    else
                    {
                        // 数据大小不合理，尝试下一个字节
                        offset++;
                    }
                }
                else
                {
                    // 不是协议消息头，尝试下一个字节
                    offset++;
                }
            }
            
            // 如果没有找到任何协议消息，可能整个文件就是纯数据
            if (pureData.Count == 0)
            {
                _logger.LogWarning("No protocol message headers found, treating entire file as pure snapshot data");
                return protocolData;
            }
            
            return pureData.ToArray();
        }

        /// <summary>
        /// 创建标准的Unity snap文件格式
        /// 基于Unity FileReader.cs的格式要求
        /// </summary>
        private async Task CreateUnitySnapFileAsync(string filePath, byte[] snapshotData)
        {
            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            
            // 写入Unity snap文件头（HeaderSignature）
            var headerSignature = BitConverter.GetBytes(0xAEABCDCD);
            await stream.WriteAsync(headerSignature, 0, 4);
            
            // 写入填充字节（根据Unity FileReader格式）
            var padding = new byte[32 - 4]; // 总共32字节的头部
            await stream.WriteAsync(padding, 0, padding.Length);
            
            // 写入内存快照数据
            await stream.WriteAsync(snapshotData, 0, snapshotData.Length);
            
            // 写入Unity snap文件尾（FooterSignature）
            var footerSignature = BitConverter.GetBytes(0xABCDCDAE);
            await stream.WriteAsync(footerSignature, 0, 4);
            
            await stream.FlushAsync();
        }

        /// <summary>
        /// 获取最终的snap文件路径
        /// </summary>
        private string GetFinalSnapFilePath(int sequence, byte[] sessionId)
        {
            var sessionStr = BinaryUtils.BinaryToHex(sessionId);
            var sessionDir = Path.Combine(_outputDirectory, sessionStr);
            
            if (!Directory.Exists(sessionDir))
            {
                Directory.CreateDirectory(sessionDir);
            }
            
            return Path.Combine(sessionDir, $"memory_snapshot_{sequence:D3}_{DateTime.Now:yyyyMMdd_HHmmss}.snap");
        }

        /// <summary>
        /// Unity官方的GetTempMemoryFilePath逻辑
        /// </summary>
        private string GetTempMemoryFilePath(int sequence, byte[] sessionId)
        {
            var sessionStr = BinaryUtils.BinaryToHex(sessionId);
            var tempDir = Path.Combine(_outputDirectory, sessionStr);
            
            if (!Directory.Exists(tempDir))
            {
                Directory.CreateDirectory(tempDir);
            }
            
            var tempFile = Path.Combine(tempDir, $"{sequence}.snap");
            
            // Unity官方逻辑：如果文件不存在，创建空文件
            if (!File.Exists(tempFile))
            {
                using (var stream = File.Create(tempFile))
                {
                    // 创建空文件
                }
            }
            
            return tempFile;
        }

        public Task BeginSnapshotAsync(int sequence, byte[] sessionId)
        {
            lock (_snapshotLock)
            {
                try
                {
                    var sessionDir = GetSessionDirectory(sessionId);
                    var fileName = $"memory_snapshot_{sequence:D3}_{DateTime.Now:yyyyMMdd_HHmmss}.snap";
                    var filePath = Path.Combine(sessionDir, fileName);

                    _logger.LogInformation("Beginning memory snapshot collection: {FilePath}", filePath);

                    // Close any existing stream for this sequence (safety check)
                    if (_activeSnapshots.ContainsKey(sequence))
                    {
                        _activeSnapshots[sequence].Close();
                        _activeSnapshots[sequence].Dispose();
                    }

                    // Create new file stream with Create mode (overwrites if exists)
                    _activeSnapshots[sequence] = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    _snapshotFilePaths[sequence] = filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to begin memory snapshot sequence {Sequence}", sequence);
                    throw;
                }
            }
            return Task.CompletedTask;
        }

        public Task<string?> AppendSnapshotDataAsync(ProfilerMessage message, int sequence, byte[] sessionId)
        {
            try
            {
                var msgData = message.GetData();
                
                lock (_snapshotLock)
                {
                    if (!_activeSnapshots.ContainsKey(sequence))
                    {
                        _logger.LogWarning("Attempting to append data to non-existent snapshot sequence {Sequence}", sequence);
                        return Task.FromResult<string?>(null);
                    }

                    // Append data to existing stream
                    _activeSnapshots[sequence].Write(msgData, 0, msgData.Length);
                    _activeSnapshots[sequence].Flush();
                }

                _logger.LogTrace("Appended {Size} bytes to snapshot sequence {Sequence}", msgData.Length, sequence);
                return Task.FromResult<string?>(null); // Return null until complete
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to append snapshot data for sequence {Sequence}", sequence);
                throw;
            }
        }

        public Task<string?> CompleteSnapshotAsync(int sequence)
        {
            lock (_snapshotLock)
            {
                try
                {
                    if (!_activeSnapshots.ContainsKey(sequence))
                    {
                        _logger.LogWarning("Attempting to complete non-existent snapshot sequence {Sequence}", sequence);
                        return Task.FromResult<string?>(null);
                    }

                    // Finalize and close the file
                    _activeSnapshots[sequence].Flush();
                    _activeSnapshots[sequence].Close();
                    _activeSnapshots[sequence].Dispose();

                    var filePath = _snapshotFilePaths[sequence];
                    
                    // Cleanup tracking
                    _activeSnapshots.Remove(sequence);
                    _snapshotFilePaths.Remove(sequence);

                    var fileInfo = new FileInfo(filePath);
                    _logger.LogInformation("Memory snapshot completed: {FilePath} ({Size:N0} bytes)", filePath, fileInfo.Length);
                    
                    return Task.FromResult<string?>(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to complete memory snapshot sequence {Sequence}", sequence);
                    throw;
                }
            }
        }

        public Task BeginProfilerDataAsync(int sequence, byte[] sessionId)
        {
            lock (_profilerDataLock)
            {
                try
                {
                    var sessionDir = GetSessionDirectory(sessionId);
                    var fileName = $"profiler_data_{sequence:D3}_{DateTime.Now:yyyyMMdd_HHmmss}.data";
                    var filePath = Path.Combine(sessionDir, fileName);

                    _logger.LogInformation("Beginning profiler data collection: {FilePath}", filePath);

                    // Close any existing stream for this sequence (safety check)
                    if (_activeProfilerData.ContainsKey(sequence))
                    {
                        _activeProfilerData[sequence].Close();
                        _activeProfilerData[sequence].Dispose();
                    }

                    // Create new file stream with Create mode (overwrites if exists)
                    _activeProfilerData[sequence] = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    _profilerDataFilePaths[sequence] = filePath;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to begin profiler data sequence {Sequence}", sequence);
                    throw;
                }
            }
            return Task.CompletedTask;
        }

        public Task<string?> AppendProfilerDataAsync(ProfilerMessage message, int sequence, byte[] sessionId)
        {
            try
            {
                var msgData = message.GetData();
                
                lock (_profilerDataLock)
                {
                    if (!_activeProfilerData.ContainsKey(sequence))
                    {
                        _logger.LogWarning("Attempting to append data to non-existent profiler data sequence {Sequence}", sequence);
                        return Task.FromResult<string?>(null);
                    }

                    // Append data to existing stream
                    _activeProfilerData[sequence].Write(msgData, 0, msgData.Length);
                    _activeProfilerData[sequence].Flush();
                }

                _logger.LogTrace("Appended {Size} bytes to profiler data sequence {Sequence}", msgData.Length, sequence);
                return Task.FromResult<string?>(null); // Return null until complete
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to append profiler data for sequence {Sequence}", sequence);
                throw;
            }
        }

        public Task<string?> CompleteProfilerDataAsync(int sequence)
        {
            lock (_profilerDataLock)
            {
                try
                {
                    if (!_activeProfilerData.ContainsKey(sequence))
                    {
                        _logger.LogWarning("Attempting to complete non-existent profiler data sequence {Sequence}", sequence);
                        return Task.FromResult<string?>(null);
                    }

                    // Finalize and close the file
                    _activeProfilerData[sequence].Flush();
                    _activeProfilerData[sequence].Close();
                    _activeProfilerData[sequence].Dispose();

                    var filePath = _profilerDataFilePaths[sequence];
                    
                    // Cleanup tracking
                    _activeProfilerData.Remove(sequence);
                    _profilerDataFilePaths.Remove(sequence);

                    var fileInfo = new FileInfo(filePath);
                    _logger.LogInformation("Profiler data completed: {FilePath} ({Size:N0} bytes)", filePath, fileInfo.Length);
                    
                    return Task.FromResult<string?>(filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to complete profiler data sequence {Sequence}", sequence);
                    throw;
                }
            }
        }

        public async Task<string?> SaveMemorySnapshotAsync(ProfilerMessage message, int sequence, byte[] sessionId)
        {
            try
            {
                var sessionDir = GetSessionDirectory(sessionId);
                var fileName = $"memory_snapshot_{sequence:D3}_{DateTime.Now:yyyyMMdd_HHmmss}.snap";
                var filePath = Path.Combine(sessionDir, fileName);

                _logger.LogInformation("Starting memory snapshot collection: {FilePath}", filePath);

                var msgData = message.GetData();
                
                // For multi-part snapshots, we need to append data
                if (!_activeSnapshots.ContainsKey(sequence))
                {
                    _activeSnapshots[sequence] = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                }

                await _activeSnapshots[sequence].WriteAsync(msgData, 0, msgData.Length);
                
                // Check if this is the last chunk (based on Unity's end marker pattern)
                if (IsSnapshotComplete(msgData))
                {
                    await _activeSnapshots[sequence].FlushAsync();
                    _activeSnapshots[sequence].Close();
                    _activeSnapshots[sequence].Dispose();
                    _activeSnapshots.Remove(sequence);

                    _logger.LogInformation("Memory snapshot saved: {FilePath} ({Size} bytes)", filePath, new FileInfo(filePath).Length);
                    return filePath;
                }

                return null; // Snapshot not complete yet
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save memory snapshot");
                throw;
            }
        }

        public async Task<string?> SaveObjectSnapshotAsync(ProfilerMessage message, int sequence, byte[] sessionId)
        {
            try
            {
                var sessionDir = GetSessionDirectory(sessionId);
                var fileName = $"object_snapshot_{sequence:D3}_{DateTime.Now:yyyyMMdd_HHmmss}.data";
                var filePath = Path.Combine(sessionDir, fileName);

                _logger.LogInformation("Saving object snapshot: {FilePath}", filePath);

                var msgData = message.GetData();
                await File.WriteAllBytesAsync(filePath, msgData);

                _logger.LogInformation("Object snapshot saved: {FilePath} ({Size} bytes)", filePath, msgData.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save object snapshot");
                throw;
            }
        }

        public async Task<string?> SavePerformanceDataAsync(ProfilerMessage message, DateTime timestamp)
        {
            try
            {
                var sessionDir = GetSessionDirectory(ProfilerMessage.sessionId);
                var fileName = $"performance_data_{timestamp:yyyyMMdd_HHmmss_fff}.data";
                var filePath = Path.Combine(sessionDir, fileName);

                var msgData = message.GetData();
                await File.WriteAllBytesAsync(filePath, msgData);

                _logger.LogDebug("Performance data saved: {FilePath} ({Size} bytes)", filePath, msgData.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save performance data");
                throw;
            }
        }

        public async Task<string?> SaveRenderDocCaptureAsync(byte[] data, DateTime timestamp)
        {
            try
            {
                var sessionDir = GetSessionDirectory(ProfilerMessage.sessionId);
                var fileName = $"renderdoc_capture_{timestamp:yyyyMMdd_HHmmss}.rdc";
                var filePath = Path.Combine(sessionDir, fileName);

                await File.WriteAllBytesAsync(filePath, data);

                _logger.LogInformation("RenderDoc capture saved: {FilePath} ({Size} bytes)", filePath, data.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save RenderDoc capture");
                throw;
            }
        }

        public Task<string?> SaveLogMessageAsync(ProfilerMessage message, DateTime timestamp)
        {
            try
            {
                lock (_logLock)
                {
                    // Initialize log file if not already open
                    if (_logFileStream == null || _logWriter == null)
                    {
                        var sessionDir = GetSessionDirectory(ProfilerMessage.sessionId ?? Array.Empty<byte>());
                        var logsDir = Path.Combine(sessionDir, "Logs");
                        if (!Directory.Exists(logsDir))
                        {
                            Directory.CreateDirectory(logsDir);
                        }

                        // Unity官方风格：使用单一日志文件合并所有Log消息
                        var fileName = $"unity_log_{DateTime.Now:yyyyMMdd_HHmmss}.log";
                        _logFilePath = Path.Combine(logsDir, fileName);
                        
                        _logFileStream = new FileStream(_logFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
                        _logWriter = new StreamWriter(_logFileStream, System.Text.Encoding.UTF8);
                        
                        // Write log header
                        _logWriter.WriteLine($"# Unity Log Session Started - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        _logWriter.WriteLine($"# Session ID: {Convert.ToHexString(ProfilerMessage.sessionId ?? Array.Empty<byte>())}");
                        _logWriter.WriteLine("# Format: [Timestamp] [MessageSize] Message Data");
                        _logWriter.WriteLine("# ================================================");
                        _logWriter.Flush();
                        
                        _logger.LogInformation("Unity log aggregation started: {LogFile}", _logFilePath);
                    }

                    // Append log entry in Unity-compatible format
                    var msgData = message.GetData();
                    var logEntry = $"[{timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{msgData.Length:D6}] ";
                    
                    _logWriter.Write(logEntry);
                    
                    // Write message data as text if possible, otherwise as hex
                    try
                    {
                        var textContent = System.Text.Encoding.UTF8.GetString(msgData);
                        // Check if it's valid text (no control characters except common ones)
                        if (IsValidLogText(textContent))
                        {
                            _logWriter.WriteLine(textContent.TrimEnd());
                        }
                        else
                        {
                            // Write as hex for binary data
                            _logWriter.WriteLine($"[HEX] {Convert.ToHexString(msgData)}");
                        }
                    }
                    catch
                    {
                        // Fallback to hex representation
                        _logWriter.WriteLine($"[HEX] {Convert.ToHexString(msgData)}");
                    }
                    
                    _logWriter.Flush();
                    _logger.LogTrace("Log message appended: {Size} bytes to {LogFile}", msgData.Length, _logFilePath);
                }
                
                return Task.FromResult(_logFilePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save log message to aggregated log file");
                return Task.FromResult<string?>(null);
            }
        }
        
        private static bool IsValidLogText(string text)
        {
            // Check if text contains only printable characters plus common whitespace/newlines
            foreach (char c in text)
            {
                if (c < 32 && c != '\r' && c != '\n' && c != '\t')
                {
                    return false;
                }
                if (c > 126 && c < 160) // Extended ASCII control characters
                {
                    return false;
                }
            }
            return text.Length > 0;
        }

        public async Task<string?> SaveFileTransferAsync(ProfilerMessage message, DateTime timestamp)
        {
            try
            {
                var sessionDir = GetSessionDirectory(ProfilerMessage.sessionId ?? Array.Empty<byte>());
                var transfersDir = Path.Combine(sessionDir, "FileTransfers");
                if (!Directory.Exists(transfersDir))
                {
                    Directory.CreateDirectory(transfersDir);
                }

                var fileName = $"transfer_{timestamp:yyyyMMdd_HHmmss_fff}.dat";
                var filePath = Path.Combine(transfersDir, fileName);

                var msgData = message.GetData();
                await File.WriteAllBytesAsync(filePath, msgData);

                _logger.LogDebug("File transfer saved: {FilePath} ({Size} bytes)", filePath, msgData.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save file transfer");
                throw;
            }
        }

        public async Task<string?> SaveLuaProfileDataAsync(ProfilerMessage message, DateTime timestamp)
        {
            try
            {
                var sessionDir = GetSessionDirectory(ProfilerMessage.sessionId ?? Array.Empty<byte>());
                var luaDir = Path.Combine(sessionDir, "LuaProfiles");
                if (!Directory.Exists(luaDir))
                {
                    Directory.CreateDirectory(luaDir);
                }

                var fileName = $"lua_profile_{timestamp:yyyyMMdd_HHmmss_fff}.data";
                var filePath = Path.Combine(luaDir, fileName);

                var msgData = message.GetData();
                await File.WriteAllBytesAsync(filePath, msgData);

                _logger.LogDebug("Lua profile data saved: {FilePath} ({Size} bytes)", filePath, msgData.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save Lua profile data");
                throw;
            }
        }

        public async Task<string?> SaveUnknownMessageAsync(ProfilerMessage message, DateTime timestamp)
        {
            try
            {
                var debugDir = Path.Combine(_outputDirectory, "Debug");
                if (!Directory.Exists(debugDir))
                {
                    Directory.CreateDirectory(debugDir);
                }

                var messageGuid = BinaryUtils.BinaryToHex(message.GetMessageId());
                var messageId = message.GetMessageIdValue();
                var fileName = $"unknown_message_{messageId}_{messageGuid}_{timestamp:yyyyMMdd_HHmmss}.bin";
                var filePath = Path.Combine(debugDir, fileName);

                var messageData = message.ToBytes();
                await File.WriteAllBytesAsync(filePath, messageData);

                _logger.LogTrace("Unknown message saved for debugging: {FilePath} (GUID={MessageGuid}, Size={Size})", 
                    filePath, messageGuid, messageData.Length);
                return filePath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save unknown message");
                throw;
            }
        }

        private string GetSessionDirectory(byte[] sessionId)
        {
            var sessionStr = BinaryUtils.BinaryToHex(sessionId);
            var sessionDir = Path.Combine(_outputDirectory, sessionStr);
            
            if (!Directory.Exists(sessionDir))
            {
                Directory.CreateDirectory(sessionDir);
            }
            
            return sessionDir;
        }

        private bool IsSnapshotComplete(byte[] msgData)
        {
            // Check for Unity's memory snapshot end marker (from original code)
            if (msgData.Length >= 4)
            {
                var endBytes = msgData;
                var len = msgData.Length;
                return endBytes[len - 4] == 175 && endBytes[len - 3] == 234 && 
                       endBytes[len - 2] == 94 && endBytes[len - 1] == 134;
            }
            return false;
        }

        public void Dispose()
        {
            // Cleanup log aggregation resources
            lock (_logLock)
            {
                try
                {
                    if (_logWriter != null)
                    {
                        _logWriter.WriteLine($"# Unity Log Session Ended - {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                        _logWriter.Flush();
                        _logWriter.Close();
                        _logWriter.Dispose();
                        _logWriter = null;
                        _logger.LogInformation("Unity log aggregation closed: {LogFile}", _logFilePath);
                    }
                    
                    if (_logFileStream != null)
                    {
                        _logFileStream.Close();
                        _logFileStream.Dispose();
                        _logFileStream = null;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing log aggregation resources");
                }
            }
            
            // Cleanup memory snapshots
            foreach (var stream in _activeSnapshots.Values)
            {
                try
                {
                    stream?.Close();
                    stream?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing snapshot file stream");
                }
            }
            _activeSnapshots.Clear();

            // Cleanup profiler data streams  
            foreach (var stream in _activeProfilerData.Values)
            {
                try
                {
                    stream?.Close();
                    stream?.Dispose();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing profiler data file stream");
                }
            }
            _activeProfilerData.Clear();
        }
    }
}