using UnityPerfProfilerWPF.Models;

namespace UnityPerfProfilerWPF.Services;

public interface IConnectionService : IDisposable
{
    Task<bool> ConnectAsync(string ipAddress, int port);
    Task DisconnectAsync();
    bool IsConnected { get; }
    UnityConnectionState ConnectionState { get; }
    double BufferUsage { get; }
    
    event EventHandler<UnityConnectionState>? ConnectionStateChanged;
    event EventHandler<DataTransferPoint>? DataTransferUpdated;

    // Unity profiler specific methods
    Task<bool> SendCommandAsync(UnityProfilerCommand command, string? parameter = null);
    Task<bool> CaptureMemorySnapshotAsync();
    Task<bool> CaptureObjectSnapshotAsync();
    Task<bool> AddTagAsync(string tagName);
    Task<bool> StopProfilingAsync();
    
    // Profiler control methods
    Task<bool> StartProfilingAsync();
    Task<bool> EnableGCCallStackAsync();
    Task<bool> DisableGCCallStackAsync();
    
    IEnumerable<DataTransferPoint> GetRecentDataTransferPoints(int maxCount = 20);
}

public interface IPerformanceDataService
{
    Task<List<DataTransferPoint>> GetDataTransferHistoryAsync(TimeSpan duration);
    Task<PerformanceMetrics> GetCurrentMetricsAsync();
    event EventHandler<DataTransferPoint>? DataPointAdded;
    event EventHandler<PerformanceMetrics>? MetricsUpdated;
}

public interface IUnitySessionService
{
    UnityProfilerSession? CurrentSession { get; }
    Task<bool> StartSessionAsync(string sessionId, string gamePackageName, string deviceId);
    Task<bool> StopSessionAsync();
    Task<bool> ValidateSessionAsync(string sessionId);
    event EventHandler<UnityProfilerSession>? SessionChanged;
}

public interface IProfilerCommandService
{
    Task<bool> ExecuteCommandAsync(UnityProfilerCommand command, string? parameter = null);
    Task<bool> AddTagAsync(string tagName);
    Task<bool> CaptureMemoryAsync();
    Task<bool> CaptureObjectsAsync();
    Task<bool> OpenRenderDocAsync();
    Task<bool> StopAsync();
}

public class UPRTag
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public int FrameNumber { get; set; }
}

public class UPRServerConfig
{
    public string SessionId { get; set; } = string.Empty;
    public string ServerHost { get; set; } = "127.0.0.1";
    public int ServerPort { get; set; } = 8001;
    public int FileServerPort { get; set; } = 8002;
    public string PlayerIp { get; set; } = "127.0.0.1";
    public string UnityVersion { get; set; } = "2019.4.x";
    public bool UseKcp { get; set; } = false;
    public string TempPath { get; set; } = string.Empty;
}

// Performance metrics data structures
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

public class DataTransferPoint
{
    public DateTime Timestamp { get; set; }
    public double SentBytes { get; set; }
    public double ReceivedBytes { get; set; }
}