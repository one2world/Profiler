using System.ComponentModel;

namespace UnityPerfProfilerWPF.Models;

public class DataTransferPoint
{
    public DateTime Timestamp { get; set; }
    public double SentBytes { get; set; }
    public double ReceivedBytes { get; set; }
}

public class UnityDeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string BrandName { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string UnityVersion { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string ScreenSize { get; set; } = string.Empty;
    public string IpAddress { get; set; } = "127.0.0.1";
    
    // Additional fields from DeviceInfo
    public string OSVersion { get; set; } = string.Empty;
    public bool IsConnected { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.Now;

    [Obsolete("Use Platform instead")]
    public string PlatformType
    {
        get => Platform;
        set => Platform = value;
    }
}

public enum UnityConnectionStatus
{
    Disconnected,
    Connecting,
    Connected,
    Error
}

public class UnityConnectionState : INotifyPropertyChanged
{
    private UnityConnectionStatus _playerStatus = UnityConnectionStatus.Disconnected;
    private UnityConnectionStatus _serverStatus = UnityConnectionStatus.Disconnected;
    private UnityConnectionStatus _packageStatus = UnityConnectionStatus.Disconnected;
    private UnityConnectionStatus _luaStatus = UnityConnectionStatus.Disconnected;
    
    // Unity Player information
    private string? _applicationName;
    private string? _packageName;
    private string? _unityVersion;
    private string? _platform;

    public UnityConnectionStatus PlayerStatus
    {
        get => _playerStatus;
        set
        {
            if (_playerStatus != value)
            {
                _playerStatus = value;
                OnPropertyChanged(nameof(PlayerStatus));
            }
        }
    }

    public UnityConnectionStatus ServerStatus
    {
        get => _serverStatus;
        set
        {
            if (_serverStatus != value)
            {
                _serverStatus = value;
                OnPropertyChanged(nameof(ServerStatus));
            }
        }
    }

    public UnityConnectionStatus PackageStatus
    {
        get => _packageStatus;
        set
        {
            if (_packageStatus != value)
            {
                _packageStatus = value;
                OnPropertyChanged(nameof(PackageStatus));
            }
        }
    }

    public UnityConnectionStatus LuaStatus
    {
        get => _luaStatus;
        set
        {
            if (_luaStatus != value)
            {
                _luaStatus = value;
                OnPropertyChanged(nameof(LuaStatus));
            }
        }
    }
    
    // Unity Player information properties
    public string? ApplicationName
    {
        get => _applicationName;
        set
        {
            if (_applicationName != value)
            {
                _applicationName = value;
                OnPropertyChanged(nameof(ApplicationName));
            }
        }
    }
    
    public string? PackageName
    {
        get => _packageName;
        set
        {
            if (_packageName != value)
            {
                _packageName = value;
                OnPropertyChanged(nameof(PackageName));
            }
        }
    }
    
    public string? UnityVersion
    {
        get => _unityVersion;
        set
        {
            if (_unityVersion != value)
            {
                _unityVersion = value;
                OnPropertyChanged(nameof(UnityVersion));
            }
        }
    }
    
    public string? Platform
    {
        get => _platform;
        set
        {
            if (_platform != value)
            {
                _platform = value;
                OnPropertyChanged(nameof(Platform));
            }
        }
    }

    public bool IsFullyConnected => PlayerStatus == UnityConnectionStatus.Connected && 
                                   ServerStatus == UnityConnectionStatus.Connected;

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}


public class UnityProfilerSession
{
    public string SessionId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public UnityDeviceInfo DeviceInfo { get; set; } = new();
    public List<DataTransferPoint> DataTransferHistory { get; set; } = new();
    public UnityConnectionState ConnectionState { get; set; } = new();
    public bool IsActive { get; set; }
    public string Status { get; set; } = "READY";
    public double BufferUsage { get; set; } = 0;
    public bool RenderDocEnabled { get; set; } = true;
    public bool AutoCaptureEnabled { get; set; } = false;
    public string AutoCaptureInterval { get; set; } = "every 4 seconds";
}

public enum UnityProfilerCommand
{
    Stop,
    AddTag,
    CaptureMemory,
    CaptureObjects,
    ForceStop,
    StartProfiling,
    ToggleConnection,
    EnableProfiling,
    DisableProfiling,
    EnableGCCallStack,
    DisableGCCallStack
}

public class UnityProfilerCommandEventArgs : EventArgs
{
    public UnityProfilerCommand Command { get; set; }
    public string? Parameter { get; set; }

    public UnityProfilerCommandEventArgs(UnityProfilerCommand command, string? parameter = null)
    {
        Command = command;
        Parameter = parameter;
    }
}

// Unity Performance Metrics
public class PerformanceMetrics
{
    public double CpuUsage { get; set; }
    public long MemoryUsage { get; set; }
    public double FrameRate { get; set; }
    public int DrawCalls { get; set; }
    public long NetworkBytesReceived { get; set; }
    public long NetworkBytesSent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

