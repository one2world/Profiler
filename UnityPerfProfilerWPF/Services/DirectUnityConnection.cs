using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Unity;
using UnityPerfProfilerWPF.Models;
using UnityPerfProfilerWPF.Utils;

namespace UnityPerfProfilerWPF.Services;

/// <summary>
/// Direct Unity Player connection without ForwardServer complexity
/// Simplified version for WPF application that connects directly to Unity Player
/// </summary>
public class DirectUnityConnection : IDisposable
{
    private readonly ILogger<DirectUnityConnection> _logger;
    private readonly IDataStorageService _dataStorageService;
    private TcpClient? _tcpClient;
    private NetworkStream? _networkStream;
    private CancellationTokenSource? _cancellationTokenSource;
    private Thread? _receiveThread;
    private Thread? _sendThread;
    private bool _isConnected = false;
    
    private string _playerIp = "";
    private int _playerPort = 0;
    private string _currentSessionId = "";
    private int _memorySnapshotSequence = 0;
    private int _objectSnapshotSequence = 0;
    
    // Snapshot state tracking
    private bool _isCapturingMemorySnapshot = false;
    private bool _isCapturingObjectSnapshot = false;
    private bool _isCapturingProfilerData = false;
    private int _profilerDataSequence = 0;
    
    // Events
    public event Action<UnityConnectionStatus>? ConnectionStatusChanged;
    public event Action<ProfilerMessage>? MessageReceived;
    public event Action<byte[]>? DataTransferred;
    
    public bool IsConnected => _isConnected && _tcpClient?.Connected == true;
    public string ErrorMessage { get; private set; } = "";
    
    public DirectUnityConnection(ILogger<DirectUnityConnection> logger, IDataStorageService dataStorageService)
    {
        _logger = logger;
        _dataStorageService = dataStorageService;
    }
    
    public async Task<bool> ConnectAsync(string ipAddress, int port)
    {
        try
        {
            await DisconnectAsync();
            
            _playerIp = ipAddress;
            _playerPort = port;
            _currentSessionId = Guid.NewGuid().ToString();
            
            _logger.LogInformation("Attempting to connect directly to Unity Player at {IpAddress}:{Port}", ipAddress, port);
            
            // Initialize Unity protocol with more sophisticated version detection
            ProfilerMessage.sessionId = BinaryUtils.SessionGUID2Bytes(_currentSessionId.Replace("-", ""));
            // 尝试使用较新的Unity版本以获得更好的协议支持
            // Unity官方通常根据目标Unity版本动态设置，我们先尝试2022.3.x
            var unityVersion = "2022.3.x";
            try 
            {
                ProfilerMessage.UnityVersionValue = (int)UnityVersionMapping.GetVersionValueByString(unityVersion);
                _logger.LogInformation("Using Unity version {Version} (value: {Value:X})", 
                    unityVersion, ProfilerMessage.UnityVersionValue);
            }
            catch
            {
                // 回退到2021.3.x
                ProfilerMessage.UnityVersionValue = (int)UnityVersionMapping.GetVersionValueByString("2021.3.x");
                _logger.LogWarning("Falling back to Unity version 2021.3.x");
            }
            ProfilerMessage.ResetSequence();
            ProfilerMessage.IsStream = false;
            
            ConnectionStatusChanged?.Invoke(UnityConnectionStatus.Connecting);
            
            // Try to connect to Unity Player
            _tcpClient = await TryConnectToUnityPlayer(ipAddress, port);
            
            if (_tcpClient == null)
            {
                ErrorMessage = $"Failed to connect to Unity Player at {ipAddress}:{port}";
                _logger.LogError(ErrorMessage);
                ConnectionStatusChanged?.Invoke(UnityConnectionStatus.Error);
                return false;
            }
            
            _networkStream = _tcpClient.GetStream();
            _cancellationTokenSource = new CancellationTokenSource();
            _isConnected = true;
            
            // Start network communication
            StartNetworkThreads();
            
            // 修复：默认连接时不自动开启Profiler，等待用户手动启动
            // Unity官方策略：连接后等待用户操作，不自动启动Profiler
            // var enableMessage = ProfilerMessageFactory.GetEnableProfileMessage(_logger);
            // SendMessage(enableMessage);
            
            ConnectionStatusChanged?.Invoke(UnityConnectionStatus.Connected);
            _logger.LogInformation("Successfully connected to Unity Player at {IpAddress}:{Port} - Profiler NOT started", ipAddress, port);
            
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Connection error: {ex.Message}";
            _logger.LogError(ex, "Failed to connect to Unity Player at {IpAddress}:{Port}", ipAddress, port);
            ConnectionStatusChanged?.Invoke(UnityConnectionStatus.Error);
            return false;
        }
    }
    
    private void StartNetworkThreads()
    {
        _receiveThread = new Thread(async () => await ReceiveMessagesAsync()) 
        { 
            Name = "Unity-Receive", 
            IsBackground = true 
        };
        _receiveThread.Start();
        
        _sendThread = new Thread(SendMessages) 
        { 
            Name = "Unity-Send", 
            IsBackground = true 
        };
        _sendThread.Start();
        
        _logger.LogDebug("Network threads started");
    }
    
    private async Task<TcpClient?> TryConnectToUnityPlayer(string ip, int port)
    {
        // Try specific port first if provided
        if (port > 0)
        {
            var client = await TryConnectToPort(ip, port);
            if (client != null) return client;
        }
        
        // Use Unity official port scanning strategy
        // Unity官方策略：扫描55000-55511端口范围 + 35000端口
        int gameStartPort = 55000;
        int gameEndPort = gameStartPort + 511; // 55000-55511
        
        _logger.LogDebug("[Player]: Scanning Unity official port range {StartPort}-{EndPort} + 35000", gameStartPort, gameEndPort);
        
        // Unity官方的端口数组构建逻辑
        var portList = new List<int>();
        for (int i = gameStartPort; i <= gameEndPort; i++)
        {
            portList.Add(i);
        }
        portList.Add(35000); // Unity官方特殊端口
        
        // Unity官方的超时策略
        var timeouts = new[] { 20, 40, 80, 160, 320 };
        
        foreach (var timeout in timeouts)
        {
            foreach (var tryPort in portList.Where(p => p != port))
            {
                var client = await TryConnectToPortWithTimeout(ip, tryPort, timeout);
                if (client != null)
                {
                    _logger.LogInformation("[Player]: Connected using Unity official strategy at {IP}:{Port} with {Timeout}ms timeout", 
                        ip, tryPort, timeout);
                    return client;
                }
            }
        }
        
        return null;
    }
    
    private async Task<TcpClient?> TryConnectToPortWithTimeout(string ip, int port, int timeoutMs)
    {
        try
        {
            var tcpClient = new TcpClient();
            _logger.LogTrace("Trying Unity Player at {IP}:{Port} with {Timeout}ms timeout", ip, port, timeoutMs);
            
            var connectTask = tcpClient.ConnectAsync(ip, port);
            if (await Task.WhenAny(connectTask, Task.Delay(timeoutMs)) == connectTask && tcpClient.Connected)
            {
                _logger.LogTrace("Connected to Unity Player at {IP}:{Port} with {Timeout}ms", ip, port, timeoutMs);
                return tcpClient;
            }
            
            tcpClient.Close();
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to connect to {IP}:{Port} with {Timeout}ms", ip, port, timeoutMs);
        }
        
        return null;
    }
    
    private async Task<TcpClient?> TryConnectToPort(string ip, int port)
    {
        try
        {
            var tcpClient = new TcpClient();
            _logger.LogTrace("Trying Unity Player at {IP}:{Port}", ip, port);
            
            var connectTask = tcpClient.ConnectAsync(ip, port);
            if (await Task.WhenAny(connectTask, Task.Delay(1000)) == connectTask && tcpClient.Connected)
            {
                _logger.LogInformation("Connected to Unity Player at {IP}:{Port}", ip, port);
                return tcpClient;
            }
            
            tcpClient.Close();
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Failed to connect to {IP}:{Port}", ip, port);
        }
        
        return null;
    }
    
    public async Task DisconnectAsync()
    {
        _isConnected = false;
        
        try
        {
            // Send disable profiling message
            if (_networkStream != null && _tcpClient?.Connected == true)
            {
                var disableMessage = ProfilerMessageFactory.GetDisableProfileMessage();
                SendMessage(disableMessage);
                await Task.Delay(100); // Allow time for message to send
            }
            
            // Stop and cleanup
            StopNetworkThreads();
            CleanupNetwork();
            
            ConnectionStatusChanged?.Invoke(UnityConnectionStatus.Disconnected);
            _logger.LogInformation("Disconnected from Unity Player");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }
    
    private void StopNetworkThreads()
    {
        _cancellationTokenSource?.Cancel();
        _receiveThread?.Join(1000);
        _sendThread?.Join(1000);
    }
    
    private void CleanupNetwork()
    {
        _networkStream?.Close();
        _networkStream = null;
        _tcpClient?.Close();  
        _tcpClient = null;
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
    
    private async Task ReceiveMessagesAsync()
    {
        _logger.LogDebug("Started Unity message receive thread");
        
        try
        {
            while (!_cancellationTokenSource!.Token.IsCancellationRequested && IsConnected)
            {
                if (_networkStream?.DataAvailable == true)
                {
                    var message = ProfilerMessage.ReadMessage(_networkStream, _logger);
                    if (message != null)
                    {
                        var messageId = message.GetMessageIdValue();
                        _logger.LogTrace("Received Unity message: Type={Type}, Size={Size}", 
                            messageId, message.DataSize());
                        
                        // Process specific message types for data capture
                        await ProcessReceivedMessage(message, messageId);
                        
                        MessageReceived?.Invoke(message);
                        DataTransferred?.Invoke(message.ToBytes());
                    }
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error in Unity message receive thread");
        }
        
        _logger.LogDebug("Unity message receive thread ended");
    }
    
    private async Task ProcessReceivedMessage(ProfilerMessage message, int messageId)
    {
        var dataSize = message.DataSize();
        var messageGuid = message.GetMessageId();
        _logger.LogTrace("Processing message ID={MessageId}, Size={Size} bytes", messageId, dataSize);
        
        try
        {
            // 采用Unity官方的处理逻辑：只基于消息ID=43处理内存快照
            if (messageId == 43) // Memory Snapshot Data Message
            {
                _logger.LogTrace("Processing memory snapshot data chunk, size={Size}", dataSize);
                
                // 使用Unity官方的CollectAndCompress逻辑
                var snapshotFilePath = await _dataStorageService.CollectAndCompressAsync(
                    _memorySnapshotSequence, message, ProfilerMessage.sessionId ?? Array.Empty<byte>());
                
                if (snapshotFilePath != null) // 快照完成（检测到结束标记）
                {
                    _logger.LogInformation("Memory snapshot completed: {FilePath}", snapshotFilePath);
                    _memorySnapshotSequence++; // Unity官方逻辑：完成后递增序列号
                    OnCaptureComplete("Memory Snapshot", snapshotFilePath);
                }
                return;
            }
            
            // Unity官方关键过滤逻辑：只处理需要转发的消息！
            if (!message.Need2Forward())
            {
                _logger.LogTrace("Message ID={MessageId} filtered out by Need2Forward()", messageId);
                return;
            }
            
            // Handle snapshot control messages for debugging/logging only
            if (BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kMessageSnapshotDataBegin))
            {
                _logger.LogInformation("[Debug] Memory snapshot data transfer started (Begin message received)");
                return;
            }
            
            if (BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kMessageSnapshotDataEnd))
            {
                _logger.LogInformation("[Debug] Memory snapshot data transfer ended (End message received)");
                return;
            }
            
            // Handle other message types - 只处理通过Need2Forward()过滤的消息
            var filePath = await ProcessByMessageType(message, messageId);
            if (filePath != null)
            {
                OnCaptureComplete(GetMessageTypeName(messageId), filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message ID={MessageId}", messageId);
        }
    }
    
    private async Task<string?> ProcessByMessageType(ProfilerMessage message, int messageId)
    {
        var messageGuid = message.GetMessageId();

        // Note: Memory Snapshot (messageId == 43) is now handled in ProcessReceivedMessage 
        // using Unity official logic, so it's removed from here

        // Profiler Data - handle continuous profiler data stream
        if (IsProfilerDataMessage(message) || messageId == 32)
        {
            // Start capturing profiler data if not already started
            if (!_isCapturingProfilerData)
            {
                _isCapturingProfilerData = true;
                await _dataStorageService.BeginProfilerDataAsync(_profilerDataSequence, ProfilerMessage.sessionId ?? Array.Empty<byte>());
                _logger.LogInformation("Started profiler data capture session {Sequence}", _profilerDataSequence);
            }
            
            // Append profiler data to current session file
            var filePath = await _dataStorageService.AppendProfilerDataAsync(
                message, _profilerDataSequence, ProfilerMessage.sessionId ?? Array.Empty<byte>());
            
            // Check if profiler data stream is complete (using Unity's end marker logic)
            if (IsDataStreamComplete(message))
            {
                _isCapturingProfilerData = false;
                filePath = await _dataStorageService.CompleteProfilerDataAsync(_profilerDataSequence);
                if (filePath != null) 
                {
                    _logger.LogInformation("Completed profiler data capture session {Sequence}: {FilePath}", _profilerDataSequence, filePath);
                    _profilerDataSequence++;
                }
            }
            return filePath;
        }

        // Object Snapshot  
        if (messageId == 40 || IsObjectSnapshotMessage(message))
        {
            var filePath = await _dataStorageService.SaveObjectSnapshotAsync(
                message, _objectSnapshotSequence, ProfilerMessage.sessionId ?? Array.Empty<byte>());
            if (filePath != null) _objectSnapshotSequence++;
            return filePath;
        }

        // Log Messages
        if (BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kLogMessage) || 
            BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kCleanLogMessage))
        {
            return await _dataStorageService.SaveLogMessageAsync(message, DateTime.Now);
        }

        // File Transfer Messages
        if (BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kFileTransferMessage))
        {
            return await _dataStorageService.SaveFileTransferAsync(message, DateTime.Now);
        }

        // Lua Profile Data
        if (BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kObjectLuaProfileDataMessage))
        {
            return await _dataStorageService.SaveLuaProfileDataAsync(message, DateTime.Now);
        }

        // Control Messages - no file output
        if (IsControlMessage(message))
        {
            _logger.LogDebug("Processed control message ID={MessageId}", messageId);
            return null;
        }

        // Unknown Messages - optional debug save
        if (message.DataSize() > 0 && _logger.IsEnabled(LogLevel.Trace))
        {
            return await _dataStorageService.SaveUnknownMessageAsync(message, DateTime.Now);
        }

        return null;
    }
    
    private static string GetMessageTypeName(int messageId) => messageId switch
    {
        43 or 44 => "Memory Snapshot",
        40 or 41 => "Object Snapshot", 
        32 => "Performance Data",
        _ => "Data Message"
    };
    
    private bool IsProfilerDataMessage(ProfilerMessage message)
    {
        var messageId = message.GetMessageId();
        return BinaryUtils.UnsafeCompare(messageId, MessageGuid.kProfileDataMessage) ||
               BinaryUtils.UnsafeCompare(messageId, MessageGuid.kProfilerFunctionsDataMessage) ||
               BinaryUtils.UnsafeCompare(messageId, MessageGuid.kObjectMemoryProfileDataMessage);
    }

    private bool IsDataStreamComplete(ProfilerMessage message)
    {
        // Similar to Unity's original logic - check for end marker
        var msgData = message.GetData();
        if (msgData.Length >= 4)
        {
            var len = msgData.Length;
            return msgData[len - 4] == 175 && msgData[len - 3] == 234 && 
                   msgData[len - 2] == 94 && msgData[len - 1] == 134;
        }
        return false;
    }
    
    private bool IsMemorySnapshotMessage(ProfilerMessage message)
    {
        var messageId = message.GetMessageId();
        return BinaryUtils.UnsafeCompare(messageId, MessageGuid.kMemorySnapshotDataMessage) ||
               BinaryUtils.UnsafeCompare(messageId, MessageGuid.kMemorySnapshotDataMessageAbove2019);
    }
    
    private bool IsObjectSnapshotMessage(ProfilerMessage message)
    {
        var messageId = message.GetMessageId();
        return BinaryUtils.UnsafeCompare(messageId, MessageGuid.kObjectMemoryProfileSnapshot) ||
               BinaryUtils.UnsafeCompare(messageId, MessageGuid.kObjectMemoryProfileDataMessage);
    }
    
    private bool IsControlMessage(ProfilerMessage message)
    {
        var messageId = message.GetMessageId();
        return BinaryUtils.UnsafeCompare(messageId, MessageGuid.kMessageSnapshotDataBegin) ||
               BinaryUtils.UnsafeCompare(messageId, MessageGuid.kMessageSnapshotDataEnd) ||
               BinaryUtils.UnsafeCompare(messageId, MessageGuid.kPingAliveMessage) ||
               BinaryUtils.UnsafeCompare(messageId, MessageGuid.kApplicationQuitMessage);
    }
    
    private void OnCaptureComplete(string captureType, string filePath)
    {
        try
        {
            // This could trigger a UI notification or event
            _logger.LogInformation("{CaptureType} capture completed: {FilePath}", captureType, filePath);
            // You could add a specific event here if needed for UI updates
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in capture complete notification");
        }
    }
    
    private void SendMessages()
    {
        _logger.LogDebug("Started Unity message send thread");
        
        try
        {
            while (!_cancellationTokenSource!.Token.IsCancellationRequested && IsConnected)
            {
                // This thread can be used for queued message sending if needed
                Thread.Sleep(100);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error in Unity message send thread");
        }
        
        _logger.LogDebug("Unity message send thread ended");
    }
    
    public bool SendMessage(ProfilerMessage message)
    {
        if (!IsConnected || _tcpClient == null) return false;
        
        try
        {
            var success = message.Send(_tcpClient, _logger);
            if (success)
            {
                _logger.LogTrace("Sent message: Type={Type}, Size={Size}", 
                    message.GetMessageIdValue(), message.DataSize());
                DataTransferred?.Invoke(message.ToBytes());
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send message");
            return false;
        }
    }
    
    public bool CaptureMemorySnapshot()
    {
        try
        {
            var message = ProfilerMessageFactory.GetCaptureMemorySnapshotMessage();
            return SendMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture memory snapshot");
            return false;
        }
    }
    
    public bool CaptureMemoryProfile()
    {
        try
        {
            var message = ProfilerMessageFactory.GetCaptureMemoryMessage();
            return SendMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture memory profile");
            return false;
        }
    }

    public bool EnableGCCallStack()
    {
        try
        {
            var message = ProfilerMessageFactory.GetEnableGcCallStackMessage();
            return SendMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable GC call stack");
            return false;
        }
    }

    public bool DisableGCCallStack()
    {
        try
        {
            var message = ProfilerMessageFactory.GetDisableGcCallStackMessage();
            return SendMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable GC call stack");
            return false;
        }
    }

    public bool StartProfiling()
    {
        try
        {
            var message = ProfilerMessageFactory.GetEnableProfileMessage(_logger);
            return SendMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start profiling");
            return false;
        }
    }

    public bool StopProfiling()
    {
        try
        {
            // Stop profiler data capture if active
            if (_isCapturingProfilerData)
            {
                _logger.LogInformation("Stopping active profiler data capture session {Sequence}", _profilerDataSequence);
                _isCapturingProfilerData = false;
                
                // Complete current profiler data session
                Task.Run(async () =>
                {
                    try
                    {
                        var filePath = await _dataStorageService.CompleteProfilerDataAsync(_profilerDataSequence);
                        if (filePath != null)
                        {
                            _logger.LogInformation("Force-completed profiler data session {Sequence}: {FilePath}", _profilerDataSequence, filePath);
                            _profilerDataSequence++;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error completing profiler data session on stop");
                    }
                });
            }
            
            var message = ProfilerMessageFactory.GetDisableProfileMessage();
            var success = SendMessage(message);
            if (success)
            {
                _logger.LogInformation("Profiler stopped - data capture ended");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop profiling");
            return false;
        }
    }
    
    public bool AddTag(string tagName)
    {
        try
        {
            var tagData = System.Text.Encoding.UTF8.GetBytes(tagName);
            var message = new ProfilerMessage(MessageGuid.kLogMessage, tagData, 0UL, false, false);
            return SendMessage(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add tag: {TagName}", tagName);
            return false;
        }
    }
    
    public void Dispose()
    {
        DisconnectAsync().Wait(5000);
        _cancellationTokenSource?.Dispose();
    }
}