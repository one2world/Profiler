using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Models;

namespace UnityPerfProfilerWPF.Services;

public class PerformanceDataService : IPerformanceDataService
{
    private readonly ILogger<PerformanceDataService> _logger;
    private readonly List<DataTransferPoint> _dataHistory = new();
    private readonly Timer? _dataCollectionTimer;
    private readonly Random _random = new();
    private bool _isCollecting = false;

    public event EventHandler<DataTransferPoint>? DataPointAdded;
    public event EventHandler<PerformanceMetrics>? MetricsUpdated;

    public PerformanceDataService(ILogger<PerformanceDataService> logger)
    {
        _logger = logger;
        
        // Create timer for simulated data collection
        _dataCollectionTimer = new Timer(CollectData, null, Timeout.Infinite, 1000);
        
        // Start collection automatically
        StartCollectionAsync();
    }

    public Task<List<DataTransferPoint>> GetDataTransferHistoryAsync(TimeSpan duration)
    {
        var cutoffTime = DateTime.Now - duration;
        List<DataTransferPoint> result;
        
        lock (_dataHistory)
        {
            result = _dataHistory.Where(dp => dp.Timestamp >= cutoffTime)
                                 .OrderBy(dp => dp.Timestamp)
                                 .ToList();
        }
        
        return Task.FromResult(result);
    }

    public Task<PerformanceMetrics> GetCurrentMetricsAsync()
    {
        var metrics = GenerateSimulatedMetrics();
        return Task.FromResult(metrics);
    }

    public Task StartCollectionAsync()
    {
        _isCollecting = true;
        _dataCollectionTimer?.Change(0, 1000); // Start immediately, then every second
        
        _logger.LogInformation("Started performance data collection");
        return Task.CompletedTask;
    }

    public Task StopCollectionAsync()
    {
        _isCollecting = false;
        _dataCollectionTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        
        _logger.LogInformation("Stopped performance data collection");
        return Task.CompletedTask;
    }

    public IEnumerable<DataTransferPoint> GetRecentData(TimeSpan timeRange)
    {
        var cutoffTime = DateTime.Now - timeRange;
        lock (_dataHistory)
        {
            return _dataHistory.Where(dp => dp.Timestamp >= cutoffTime)
                               .OrderBy(dp => dp.Timestamp)
                               .ToList();
        }
    }

    private void CollectData(object? state)
    {
        if (!_isCollecting)
            return;

        try
        {
            // Generate simulated performance data
            var metrics = GenerateSimulatedMetrics();
            
            // Create data transfer point
            var dataPoint = new DataTransferPoint
            {
                Timestamp = DateTime.Now,
                SentBytes = metrics.NetworkBytesSent,
                ReceivedBytes = metrics.NetworkBytesReceived
            };
            
            lock (_dataHistory)
            {
                _dataHistory.Add(dataPoint);
                
                // Keep history within reasonable bounds (last hour)
                var cutoffTime = DateTime.Now.AddHours(-1);
                _dataHistory.RemoveAll(dp => dp.Timestamp < cutoffTime);
            }

            // Trigger events
            DataPointAdded?.Invoke(this, dataPoint);
            MetricsUpdated?.Invoke(this, metrics);
            
            _logger.LogTrace("Collected performance metrics: CPU={CpuUsage:F1}%, Memory={MemoryUsage:F1}MB, FPS={FPS}", 
                metrics.CpuUsage, metrics.MemoryUsage / 1024.0 / 1024.0, metrics.FrameRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error collecting performance data");
        }
    }

    private PerformanceMetrics GenerateSimulatedMetrics()
    {
        // Generate simulated performance metrics data
        var baseTime = DateTime.Now;
        var timeOffset = (baseTime.Second % 60) * 0.1;
        
        return new PerformanceMetrics
        {
            CpuUsage = 30 + Math.Sin(timeOffset) * 20 + _random.NextDouble() * 10,
            MemoryUsage = (long)(512 * 1024 * 1024 + Math.Cos(timeOffset * 0.8) * 100 * 1024 * 1024 + _random.NextDouble() * 50 * 1024 * 1024), // Bytes
            FrameRate = 60 + Math.Sin(timeOffset * 0.5) * 15 + _random.NextDouble() * 10,
            DrawCalls = (int)(500 + Math.Sin(timeOffset * 1.5) * 200 + _random.NextDouble() * 100),
            NetworkBytesSent = (long)(1024 + Math.Sin(timeOffset * 2) * 512 + _random.NextDouble() * 256),
            NetworkBytesReceived = (long)(768 + Math.Cos(timeOffset * 1.8) * 384 + _random.NextDouble() * 192),
            Timestamp = DateTime.Now
        };
    }

    public void Dispose()
    {
        _dataCollectionTimer?.Dispose();
    }
}