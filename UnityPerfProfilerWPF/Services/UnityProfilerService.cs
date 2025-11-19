using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Unity;
using UnityPerfProfilerWPF.Models;
using UnityPerfProfilerWPF.Server;
using UnityPerfProfilerWPF.Utils;

namespace UnityPerfProfilerWPF.Services;

/// <summary>
/// Unity Profiler Service - Direct Unity Player connection
/// Provides 100% Unity protocol compatibility with simplified direct connection
/// </summary>
public class UnityProfilerService : IDisposable
{
    private readonly ILogger<UnityProfilerService> _logger;
    private readonly IDataStorageService _dataStorageService;
    private DirectUnityConnection? _directConnection;
    private string _currentSessionId = string.Empty;
    
    // Unity protocol state
    public UnityConnectionState ConnectionState { get; } = new();
    public bool IsConnected => _directConnection?.IsConnected == true;
    public bool IsRunning => IsConnected;
    
    // Events
    public event EventHandler<UnityConnectionState>? ConnectionStateChanged;
    public event EventHandler<ProfilerMessage>? ProfilerMessageReceived;
    public event EventHandler<byte[]>? DataTransferred;

    public UnityProfilerService(ILogger<UnityProfilerService> logger, IDataStorageService dataStorageService)
    {
        _logger = logger;
        _dataStorageService = dataStorageService;
        InitializeUnityContext();
    }

    private void InitializeUnityContext()
    {
        // Initialize UPR context with default values
        UPRContext.Init();
        UPRContext.PlayerIp = "127.0.0.1";
        UPRContext.PlayerPort = 55000;
        UPRContext.PlayerUnityVersion = "2021.3.x";
        UPRContext.SessionId = Guid.NewGuid().ToString();
        
        _logger.LogInformation("Unity profiler context initialized");
    }

    public async Task<bool> ConnectToUnityPlayerAsync(string ipAddress, int port)
    {
        try
        {
            await DisconnectAsync();
            
            // Update UPR context with connection parameters
            UPRContext.PlayerIp = ipAddress;
            UPRContext.PlayerPort = port;
            _currentSessionId = UPRContext.SessionId;
            
            _logger.LogInformation("Starting direct Unity Player connection to {IpAddress}:{Port}", ipAddress, port);
            
            // Create direct Unity connection
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _directConnection = new DirectUnityConnection(
                loggerFactory.CreateLogger<DirectUnityConnection>(), 
                _dataStorageService);
            
            // Setup event handlers
            SetupDirectConnectionEventHandlers();
            
            ConnectionState.PlayerStatus = UnityConnectionStatus.Connecting;
            OnConnectionStateChanged();
            
            // Attempt to connect
            var success = await _directConnection.ConnectAsync(ipAddress, port);
            
            if (success)
            {
                ConnectionState.PlayerStatus = UnityConnectionStatus.Connected;
                OnConnectionStateChanged();
                _logger.LogInformation("Successfully connected to Unity Player at {IpAddress}:{Port}", ipAddress, port);
            }
            else
            {
                ConnectionState.PlayerStatus = UnityConnectionStatus.Error;
                OnConnectionStateChanged();
                var errorMsg = _directConnection.ErrorMessage;
                _logger.LogError("Failed to connect to Unity Player: {Error}", errorMsg);
            }
            
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while connecting to Unity Player {IpAddress}:{Port}", ipAddress, port);
            
            ConnectionState.PlayerStatus = UnityConnectionStatus.Error;
            OnConnectionStateChanged();
            
            return false;
        }
    }

    private void SetupDirectConnectionEventHandlers()
    {
        if (_directConnection == null) return;
        
        _directConnection.ConnectionStatusChanged += (status) =>
        {
            ConnectionState.PlayerStatus = status;
            OnConnectionStateChanged();
        };
        
        _directConnection.MessageReceived += (message) =>
        {
            ProfilerMessageReceived?.Invoke(this, message);
        };
        
        _directConnection.DataTransferred += (data) =>
        {
            DataTransferred?.Invoke(this, data);
        };
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_directConnection != null)
            {
                await _directConnection.DisconnectAsync();
                _directConnection = null;
            }

            ConnectionState.PlayerStatus = UnityConnectionStatus.Disconnected;
            OnConnectionStateChanged();

            _logger.LogInformation("Disconnected from Unity Player");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disconnect");
        }
    }

    public bool EnableProfiling()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot enable profiling - not connected to Unity Player");
            return false;
        }

        try
        {
            // DirectConnection automatically sends enable profiling message when connecting
            _logger.LogInformation("Profiling enabled via DirectConnection");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable profiling");
            return false;
        }
    }

    public Task<bool> DisableProfilingAsync()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot disable profiling - not connected to Unity Player");
            return Task.FromResult(false);
        }

        try
        {
            // 发送停止Profiling消息给Unity
            var success = _directConnection.StopProfiling();
            if (success)
            {
                _logger.LogInformation("Profiling disabled via DirectConnection");
            }
            else
            {
                _logger.LogWarning("Failed to send disable profiling message");
            }
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable profiling");
            return Task.FromResult(false);
        }
    }

    public bool CaptureMemorySnapshot()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot capture memory snapshot - not connected to Unity Player");
            return false;
        }

        try
        {
            var success = _directConnection.CaptureMemorySnapshot();
            if (success)
            {
                _logger.LogInformation("Memory snapshot capture requested");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture memory snapshot");
            return false;
        }
    }

    public Task<bool> CaptureMemoryProfileAsync()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot capture memory profile - not connected to Unity Player");
            return Task.FromResult(false);
        }

        try
        {
            var success = _directConnection.CaptureMemoryProfile();
            if (success)
            {
                _logger.LogInformation("Memory profile capture requested");
            }
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to capture memory profile");
            return Task.FromResult(false);
        }
    }

    public Task<bool> AddTagAsync(string tagName)
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot add tag - not connected to Unity Player");
            return Task.FromResult(false);
        }

        try
        {
            var success = _directConnection.AddTag(tagName);
            if (success)
            {
                _logger.LogInformation("Tag added: {TagName}", tagName);
            }
            return Task.FromResult(success);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to add tag: {TagName}", tagName);
            return Task.FromResult(false);
        }
    }

    public bool StartProfiling()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot start profiling - not connected to Unity Player");
            return false;
        }

        try
        {
            var success = _directConnection.StartProfiling();
            if (success)
            {
                _logger.LogInformation("Profiling started via DirectConnection");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start profiling");
            return false;
        }
    }

    public bool StopProfiling()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot stop profiling - not connected to Unity Player");
            return false;
        }

        try
        {
            var success = _directConnection.StopProfiling();
            if (success)
            {
                _logger.LogInformation("Profiling stopped via DirectConnection");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop profiling");
            return false;
        }
    }

    public bool EnableGCCallStack()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot enable GC call stack - not connected to Unity Player");
            return false;
        }

        try
        {
            var success = _directConnection.EnableGCCallStack();
            if (success)
            {
                _logger.LogInformation("GC call stack enabled via DirectConnection");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable GC call stack");
            return false;
        }
    }

    public bool DisableGCCallStack()
    {
        if (_directConnection == null || !IsConnected)
        {
            _logger.LogWarning("Cannot disable GC call stack - not connected to Unity Player");
            return false;
        }

        try
        {
            var success = _directConnection.DisableGCCallStack();
            if (success)
            {
                _logger.LogInformation("GC call stack disabled via DirectConnection");
            }
            return success;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable GC call stack");
            return false;
        }
    }

    public int GetPendingMessageCount()
    {
        // DirectConnection doesn't track pending messages the same way
        return 0;
    }

    public ProfilerMessage[] GetRecentMessages(int maxCount = 10)
    {
        // DirectConnection doesn't expose message history in the same way
        return Array.Empty<ProfilerMessage>();
    }

    public int[] GetMeterData()
    {
        // Return empty array for now - could be implemented later if needed
        return Array.Empty<int>();
    }

    public int[] GetOutputMeterData()
    {
        // Return empty array for now - could be implemented later if needed
        return Array.Empty<int>();
    }

    public string GetErrorMessage()
    {
        return _directConnection?.ErrorMessage ?? string.Empty;
    }

    private void OnConnectionStateChanged()
    {
        ConnectionStateChanged?.Invoke(this, ConnectionState);
    }

    public void Dispose()
    {
        try
        {
            DisconnectAsync().Wait(5000);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disposal");
        }
    }
}