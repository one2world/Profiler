using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Models;
using UnityPerfProfilerWPF.Unity;
using System.Collections.Concurrent;

namespace UnityPerfProfilerWPF.Services;

public class UnityConnectionService : IConnectionService
{
    private readonly ILogger<UnityConnectionService> _logger;
    private readonly UnityProfilerService _unityProfilerService;
    private readonly ConcurrentQueue<DataTransferPoint> _dataTransferQueue = new();
    private Timer? _metricsTimer;
    private long _totalBytesSent = 0;
    private long _totalBytesReceived = 0;
    private DateTime _lastMetricsUpdate = DateTime.Now;

    public bool IsConnected => _unityProfilerService.IsConnected;
    public UnityConnectionState ConnectionState => _unityProfilerService.ConnectionState;
    public double BufferUsage { get; private set; } = 0;

    public event EventHandler<UnityConnectionState>? ConnectionStateChanged;
    public event EventHandler<DataTransferPoint>? DataTransferUpdated;

    public UnityConnectionService(ILogger<UnityConnectionService> logger, UnityProfilerService unityProfilerService)
    {
        _logger = logger;
        _unityProfilerService = unityProfilerService;
        
        // Subscribe to Unity service events
        _unityProfilerService.ConnectionStateChanged += OnUnityConnectionStateChanged;
        _unityProfilerService.DataTransferred += OnUnityDataTransferred;
        _unityProfilerService.ProfilerMessageReceived += OnUnityMessageReceived;
        
        _metricsTimer = new Timer(UpdateMetrics, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public async Task<bool> ConnectAsync(string ipAddress, int port)
    {
        try
        {
            _logger.LogInformation("Connecting to Unity Player at {IpAddress}:{Port}", ipAddress, port);
            
            var success = await _unityProfilerService.ConnectToUnityPlayerAsync(ipAddress, port);
            if (success)
            {
                _logger.LogInformation("Successfully connected to Unity Player using Unity protocol");
            }
            else
            {
                _logger.LogWarning("Failed to connect to Unity Player");
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to Unity Player {IpAddress}:{Port}", ipAddress, port);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            await _unityProfilerService.DisconnectAsync();
            _logger.LogInformation("Disconnected from Unity Player");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }

    public async Task<bool> SendCommandAsync(UnityProfilerCommand command, string? parameter = null)
    {
        try
        {
            bool success = command switch
            {
                UnityProfilerCommand.EnableProfiling => _unityProfilerService.EnableProfiling(),
                UnityProfilerCommand.DisableProfiling => await _unityProfilerService.DisableProfilingAsync(),
                UnityProfilerCommand.CaptureMemory => _unityProfilerService.CaptureMemorySnapshot(),
                UnityProfilerCommand.CaptureObjects => await _unityProfilerService.CaptureMemoryProfileAsync(),
                UnityProfilerCommand.AddTag => await _unityProfilerService.AddTagAsync(parameter ?? ""),
                UnityProfilerCommand.Stop => await _unityProfilerService.DisableProfilingAsync(),
                _ => false
            };

            if (success)
            {
                _logger.LogInformation("Unity command executed successfully: {Command}", command);
            }
            else
            {
                _logger.LogWarning("Failed to execute Unity command: {Command}", command);
            }

            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing Unity command: {Command}", command);
            return false;
        }
    }

    public async Task<bool> CaptureMemorySnapshotAsync()
    {
        return await SendCommandAsync(UnityProfilerCommand.CaptureMemory);
    }

    public async Task<bool> CaptureObjectSnapshotAsync()
    {
        return await SendCommandAsync(UnityProfilerCommand.CaptureObjects);
    }

    public async Task<bool> AddTagAsync(string tagName)
    {
        return await SendCommandAsync(UnityProfilerCommand.AddTag, tagName);
    }

    public async Task<bool> StopProfilingAsync()
    {
        return await SendCommandAsync(UnityProfilerCommand.Stop);
    }

    public Task<bool> StartProfilingAsync()
    {
        try
        {
            bool success = _unityProfilerService.StartProfiling();
            if (success)
            {
                _logger.LogInformation("Profiling started successfully");
            }
            else
            {
                _logger.LogWarning("Failed to start profiling");
            }
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting profiling");
            return Task.FromResult(false);
        }
    }

    public Task<bool> EnableGCCallStackAsync()
    {
        try
        {
            bool success = _unityProfilerService.EnableGCCallStack();
            if (success)
            {
                _logger.LogInformation("GC call stack enabled successfully");
            }
            else
            {
                _logger.LogWarning("Failed to enable GC call stack");
            }
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enabling GC call stack");
            return Task.FromResult(false);
        }
    }

    public Task<bool> DisableGCCallStackAsync()
    {
        try
        {
            bool success = _unityProfilerService.DisableGCCallStack();
            if (success)
            {
                _logger.LogInformation("GC call stack disabled successfully");
            }
            else
            {
                _logger.LogWarning("Failed to disable GC call stack");
            }
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disabling GC call stack");
            return Task.FromResult(false);
        }
    }

    public IEnumerable<DataTransferPoint> GetRecentDataTransferPoints(int maxCount = 20)
    {
        var points = new List<DataTransferPoint>();
        var tempQueue = new ConcurrentQueue<DataTransferPoint>();

        // Dequeue all items and keep the last maxCount
        while (_dataTransferQueue.TryDequeue(out var point))
        {
            tempQueue.Enqueue(point);
            if (tempQueue.Count > maxCount)
            {
                tempQueue.TryDequeue(out _);
            }
        }

        // Put items back and return as list
        while (tempQueue.TryDequeue(out var point))
        {
            points.Add(point);
            _dataTransferQueue.Enqueue(point);
        }

        return points;
    }

    private void OnUnityConnectionStateChanged(object? sender, UnityConnectionState state)
    {
        ConnectionStateChanged?.Invoke(this, state);
    }

    private void OnUnityDataTransferred(object? sender, byte[] data)
    {
        Interlocked.Add(ref _totalBytesReceived, data.Length);
        
        // Update buffer usage simulation based on Unity message queue
        var pendingMessages = _unityProfilerService.GetPendingMessageCount();
        BufferUsage = Math.Min(100, pendingMessages * 5); // Simple simulation
    }

    private void OnUnityMessageReceived(object? sender, ProfilerMessage message)
    {
        _logger.LogTrace("Unity message received: Type={MessageType}, Size={Size}bytes", 
            message.GetMessageIdValue(), message.DataSize());

        // Process specific Unity message types for additional functionality
        var messageGuid = message.GetMessageId();
        if (BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kMemorySnapshotDataMessage))
        {
            _logger.LogInformation("Memory snapshot data received from Unity");
        }
        else if (BinaryUtils.UnsafeCompare(messageGuid, MessageGuid.kProfileDataMessage))
        {
            // Process profiler frame data
            UpdateBufferUsageFromProfilerData(message);
        }
    }

    private void UpdateBufferUsageFromProfilerData(ProfilerMessage message)
    {
        try
        {
            // Simple buffer usage calculation based on message size and frequency
            var dataSize = message.DataSize();
            BufferUsage = Math.Min(100, (BufferUsage * 0.9) + (dataSize / 10240.0 * 100)); // Decay + new data
        }
        catch (Exception ex)
        {
            _logger.LogTrace(ex, "Error updating buffer usage from profiler data");
        }
    }

    private void UpdateMetrics(object? state)
    {
        var now = DateTime.Now;
        var timeDelta = (now - _lastMetricsUpdate).TotalSeconds;
        
        if (timeDelta > 0)
        {
            var sentBytes = Interlocked.Exchange(ref _totalBytesSent, 0);
            var receivedBytes = Interlocked.Exchange(ref _totalBytesReceived, 0);
            
            var dataPoint = new DataTransferPoint
            {
                Timestamp = now,
                SentBytes = sentBytes / timeDelta, // Bytes per second
                ReceivedBytes = receivedBytes / timeDelta
            };

            _dataTransferQueue.Enqueue(dataPoint);
            
            // Keep only last 60 data points (1 minute of data)
            while (_dataTransferQueue.Count > 60)
            {
                _dataTransferQueue.TryDequeue(out _);
            }

            DataTransferUpdated?.Invoke(this, dataPoint);
        }

        _lastMetricsUpdate = now;
    }

    public void Dispose()
    {
        _metricsTimer?.Dispose();
        _unityProfilerService?.Dispose();
    }
}