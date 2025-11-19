using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using UnityPerfProfilerWPF.Models;
using UnityPerfProfilerWPF.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using LiveChartsCore.Defaults;
using Microsoft.Extensions.Logging;

namespace UnityPerfProfilerWPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IConnectionService _connectionService;
    private readonly IPerformanceDataService _performanceDataService;
    private readonly IDataStorageService _dataStorageService;
    private readonly IRenderDocService _renderDocService;
    private readonly ILogger<MainViewModel> _logger;
    private Timer? _updateTimer;

    [ObservableProperty]
    private string _gameName = "Unknown Application";

    [ObservableProperty]
    private string _packageName = "com.unknown.application";

    [ObservableProperty]
    private string _unityVersion = "Unknown";
    
    [ObservableProperty]
    private string _platformInfo = "Unknown Platform";
    
    [ObservableProperty]
    private string _outputDirectory = "";

    // Remove unused properties
    // private string _projectId, _screenSize, _autoCaptureStatus, _status removed

    [ObservableProperty]
    private string _ipAddress = "127.0.0.1";

    [ObservableProperty]
    private bool _isConnected = false;

    [ObservableProperty] 
    private bool _isConnecting = false;

    [ObservableProperty]
    private string _connectButtonText = "Connect";
    
    [ObservableProperty]
    private Brush _connectionStatusColor = Brushes.Red;
    
    [ObservableProperty]
    private string _connectionStatusText = "Disconnected";

    [ObservableProperty]
    private bool _renderDocEnabled = false;

    [ObservableProperty]
    private double _bufferUsage = 65.0;

    [ObservableProperty]
    private UnityConnectionState _connectionState = new();

    // Profiler control state
    [ObservableProperty]
    private bool _isProfilingEnabled = false;
    
    [ObservableProperty]
    private bool _isGCCallStackEnabled = false;
    
    // Computed properties for button states
    public bool CanStartProfiling => IsConnected && !IsProfilingEnabled;
    public bool CanStopProfiling => IsConnected && IsProfilingEnabled;

    // LiveCharts properties
    [ObservableProperty]
    private ISeries[] _chartSeries = Array.Empty<ISeries>();

    [ObservableProperty]
    private Axis[] _xAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private Axis[] _yAxes = Array.Empty<Axis>();

    [ObservableProperty]
    private TimeSpan _animationsSpeed = TimeSpan.FromMilliseconds(800);

    [ObservableProperty]
    private Func<float, float>? _easingFunction = LiveChartsCore.EasingFunctions.BounceInOut;

    [ObservableProperty]
    private LiveChartsCore.Measure.Margin? _drawMargin = new(50, 50, 50, 50);

    public MainViewModel(
        IConnectionService connectionService, 
        IPerformanceDataService performanceDataService,
        IDataStorageService dataStorageService,
        IRenderDocService renderDocService,
        ILogger<MainViewModel> logger)
    {
        _connectionService = connectionService;
        _performanceDataService = performanceDataService;
        _dataStorageService = dataStorageService;
        _renderDocService = renderDocService;
        _logger = logger;
        
        // Subscribe to events
        _connectionService.ConnectionStateChanged += OnConnectionStateChanged;
        _connectionService.DataTransferUpdated += OnDataTransferUpdated;
        
        InitializeData();
        StartPeriodicUpdates();
    }

    private void InitializeData()
    {
        // Initialize chart
        InitializeChart();
        
        // Initialize connection state
        ConnectionState = _connectionService.ConnectionState;
        IsConnected = _connectionService.IsConnected;
        BufferUsage = _connectionService.BufferUsage;
        
        // Initialize output directory
        OutputDirectory = _dataStorageService.GetOutputDirectory();
        
        // Initialize UI connection status
        UpdateConnectionUI();
    }
    
    private void UpdateConnectionUI()
    {
        if (IsConnected)
        {
            ConnectButtonText = "Disconnect";
            ConnectionStatusText = "Connected";
            ConnectionStatusColor = Brushes.LimeGreen;
        }
        else
        {
            ConnectButtonText = "Connect";
            ConnectionStatusText = "Disconnected";
            ConnectionStatusColor = Brushes.Red;
        }
        
        // Trigger property change notifications for computed properties
        OnPropertyChanged(nameof(CanStartProfiling));
        OnPropertyChanged(nameof(CanStopProfiling));
    }

    private void InitializeChart()
    {
        // Create observable chart data collections
        var sentValues = new ObservableCollection<ObservableValue>();
        var receivedValues = new ObservableCollection<ObservableValue>();

        // Initialize with sample data
        var recentPoints = _connectionService.GetRecentDataTransferPoints(20);
        foreach (var point in recentPoints)
        {
            sentValues.Add(new ObservableValue(point.SentBytes / 1024.0)); // Convert to KB/s
            receivedValues.Add(new ObservableValue(point.ReceivedBytes / 1024.0));
        }

        // If no data, create some initial empty points
        if (sentValues.Count == 0)
        {
            for (int i = 0; i < 20; i++)
            {
                sentValues.Add(new ObservableValue(0));
                receivedValues.Add(new ObservableValue(0));
            }
        }

        // Configure chart series
        ChartSeries = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Values = sentValues,
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.LimeGreen) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColors.LimeGreen) { StrokeThickness = 4 },
                GeometryFill = new SolidColorPaint(SKColors.LimeGreen),
                GeometrySize = 4,
                Name = "Sent to Server"
            },
            new LineSeries<ObservableValue>
            {
                Values = receivedValues,
                Fill = null,
                Stroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 2 },
                GeometryStroke = new SolidColorPaint(SKColors.DodgerBlue) { StrokeThickness = 4 },
                GeometryFill = new SolidColorPaint(SKColors.DodgerBlue),
                GeometrySize = 4,
                Name = "Received from Player"
            }
        };

        // Configure X axis (time axis)
        XAxes = new Axis[]
        {
            new Axis
            {
                Name = "Time",
                LabelsRotation = 0,
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                NamePaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray) { StrokeThickness = 0.5f }
            }
        };

        // Configure Y axis (data value axis)
        YAxes = new Axis[]
        {
            new Axis
            {
                Name = "Data (KB/s)",
                LabelsRotation = 0,
                TextSize = 12,
                LabelsPaint = new SolidColorPaint(SKColors.Gray),
                NamePaint = new SolidColorPaint(SKColors.LightGray),
                SeparatorsPaint = new SolidColorPaint(SKColors.DarkGray) { StrokeThickness = 0.5f },
                MinLimit = 0
            }
        };
    }

    private void StartPeriodicUpdates()
    {
        _updateTimer = new Timer(UpdateUI, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    private void UpdateUI(object? state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Update buffer usage
            BufferUsage = _connectionService.BufferUsage;
            
            // Update connection status
            var wasConnected = IsConnected;
            IsConnected = _connectionService.IsConnected;
            
            // Update connection UI if status changed
            if (IsConnected != wasConnected)
            {
                UpdateConnectionUI();
            }
        });
    }

    private void OnConnectionStateChanged(object? sender, UnityConnectionState state)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ConnectionState = state;
            var wasConnected = IsConnected;
            IsConnected = _connectionService.IsConnected;
            
            // Update UI based on actual connection status
            if (IsConnected && !wasConnected)
            {
                ConnectButtonText = "Disconnect";
                ConnectionStatusText = "Connected";
                ConnectionStatusColor = Brushes.LimeGreen;
                
                // Try to extract Unity Player information when connected
                UpdateUnityPlayerInfo(state);
            }
            else if (!IsConnected && wasConnected)
            {
                ConnectButtonText = "Connect";
                ConnectionStatusText = "Disconnected";
                ConnectionStatusColor = Brushes.Red;
                
                // Reset profiler state when disconnected
                IsProfilingEnabled = false;
                IsGCCallStackEnabled = false;
                
                // Reset Unity info to defaults
                ResetUnityPlayerInfo();
            }
            
            // Trigger property change notifications for computed properties
            OnPropertyChanged(nameof(CanStartProfiling));
            OnPropertyChanged(nameof(CanStopProfiling));
            
            IsConnecting = false;
        });
    }
    
    private void UpdateUnityPlayerInfo(UnityConnectionState state)
    {
        try
        {
            // Extract information from connection state if available
            // This is a placeholder - Unity connection info would be provided by the service
            GameName = state.ApplicationName ?? "Unity Application";
            PackageName = state.PackageName ?? "com.unity.application";  
            UnityVersion = state.UnityVersion ?? "Unknown";
            PlatformInfo = state.Platform ?? "Unknown Platform";
            
            _logger.LogInformation("Updated Unity Player info - App: {App}, Package: {Package}, Version: {Version}", 
                                 GameName, PackageName, UnityVersion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Unity Player info");
        }
    }
    
    private void ResetUnityPlayerInfo()
    {
        GameName = "Unknown Application";
        PackageName = "com.unknown.application";
        UnityVersion = "Unknown";
        PlatformInfo = "Unknown Platform";
    }

    private void OnDataTransferUpdated(object? sender, Services.DataTransferPoint dataPoint)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Update chart with new data point
            if (ChartSeries.Length >= 2 && 
                ChartSeries[0] is LineSeries<ObservableValue> sentSeries &&
                ChartSeries[1] is LineSeries<ObservableValue> receivedSeries)
            {
                var sentValues = sentSeries.Values as ObservableCollection<ObservableValue>;
                var receivedValues = receivedSeries.Values as ObservableCollection<ObservableValue>;

                if (sentValues != null && receivedValues != null)
                {
                    // Remove old data points (keep last 60)
                    while (sentValues.Count >= 60)
                    {
                        sentValues.RemoveAt(0);
                        receivedValues.RemoveAt(0);
                    }
                    
                    // Add new data points (convert bytes to KB/s)
                    sentValues.Add(new ObservableValue(dataPoint.SentBytes / 1024.0));
                    receivedValues.Add(new ObservableValue(dataPoint.ReceivedBytes / 1024.0));
                }
            }
        });
    }

    [RelayCommand]
    private async Task AddTag()
    {
        try
        {
            var tagName = $"Tag_{DateTime.Now:HHmmss}";
            var success = await _connectionService.AddTagAsync(tagName);
            
            if (success)
            {
                _logger.LogInformation("Tag added successfully: {TagName}", tagName);
            }
            else
            {
                _logger.LogWarning("Failed to add tag: {TagName}", tagName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding tag");
        }
    }

    [RelayCommand]
    private async Task CaptureObjects()
    {
        try
        {
            var success = await _connectionService.CaptureObjectSnapshotAsync();
            
            if (success)
            {
                _logger.LogInformation("Object capture initiated successfully");
            }
            else
            {
                _logger.LogWarning("Failed to initiate object capture");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing objects");
        }
    }

    [RelayCommand]
    private async Task CaptureMemory()
    {
        try
        {
            var success = await _connectionService.CaptureMemorySnapshotAsync();
            
            if (success)
            {
                _logger.LogInformation("Memory capture initiated successfully");
            }
            else
            {
                _logger.LogWarning("Failed to initiate memory capture");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error capturing memory");
        }
    }

    [RelayCommand]
    private async Task OpenRenderDoc()
    {
        try
        {
            if (!await _renderDocService.IsRenderDocAvailableAsync())
            {
                _logger.LogWarning("RenderDoc is not available on this system");
                MessageBox.Show("RenderDoc is not installed or not found in PATH.\n\nPlease install RenderDoc to use this functionality.", 
                                "RenderDoc Not Available", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _logger.LogInformation("Starting RenderDoc frame capture...");
            
            var success = await _renderDocService.CaptureFrameAsync();
            
            if (success)
            {
                _logger.LogInformation("RenderDoc frame capture completed successfully");
                MessageBox.Show("RenderDoc frame capture completed successfully!\n\nCapture files are saved in the output directory.", 
                                "RenderDoc Capture Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                _logger.LogWarning("RenderDoc frame capture failed");
                MessageBox.Show("RenderDoc frame capture failed.\n\nPlease check the logs for more information.", 
                                "RenderDoc Capture Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during RenderDoc capture");
            MessageBox.Show($"Error during RenderDoc capture: {ex.Message}", 
                            "RenderDoc Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    private async Task Stop()
    {
        try
        {
            var success = await _connectionService.StopProfilingAsync();
            
            if (success)
            {
                _logger.LogInformation("Profiling stopped successfully");
                IsProfilingEnabled = false;
            }
            else
            {
                _logger.LogWarning("Failed to stop profiling");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping profiling");
        }
    }

    [RelayCommand]
    private async Task StartProfiling()
    {
        try
        {
            var success = await _connectionService.StartProfilingAsync();
            
            if (success)
            {
                _logger.LogInformation("Profiling started successfully");
                IsProfilingEnabled = true;
                // Trigger property change notifications for button states
                OnPropertyChanged(nameof(CanStartProfiling));
                OnPropertyChanged(nameof(CanStopProfiling));
            }
            else
            {
                _logger.LogWarning("Failed to start profiling");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting profiling");
        }
    }

    [RelayCommand]
    private async Task StopProfiling()
    {
        try
        {
            var success = await _connectionService.StopProfilingAsync();
            
            if (success)
            {
                _logger.LogInformation("Profiling stopped successfully");
                IsProfilingEnabled = false;
                // Trigger property change notifications for button states
                OnPropertyChanged(nameof(CanStartProfiling));
                OnPropertyChanged(nameof(CanStopProfiling));
            }
            else
            {
                _logger.LogWarning("Failed to stop profiling");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping profiling");
        }
    }

    [RelayCommand]
    private async Task ToggleConnection()
    {
        if (IsConnecting) 
        {
            _logger.LogWarning("Connection already in progress");
            return;
        }

        try
        {
            if (IsConnected)
            {
                // Disconnect
                IsConnecting = true;
                ConnectButtonText = "Disconnecting...";
                ConnectionStatusText = "Disconnecting...";
                ConnectionStatusColor = Brushes.Orange;
                
                await _connectionService.DisconnectAsync();
                
                IsConnected = false;
                ConnectButtonText = "Connect";
                ConnectionStatusText = "Disconnected";
                ConnectionStatusColor = Brushes.Red;
                
                _logger.LogInformation("Disconnected from Unity Player");
            }
            else
            {
                // Validate IP address
                if (string.IsNullOrWhiteSpace(IpAddress))
                {
                    _logger.LogWarning("IP address cannot be empty");
                    MessageBox.Show("Please enter a valid IP address", "Invalid IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                if (!System.Net.IPAddress.TryParse(IpAddress.Trim(), out _))
                {
                    _logger.LogWarning("Invalid IP address format: {IpAddress}", IpAddress);
                    MessageBox.Show("Please enter a valid IP address format (e.g., 192.168.1.100)", "Invalid IP", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Connect
                IsConnecting = true;
                ConnectButtonText = "Connecting...";
                ConnectionStatusText = "Connecting...";
                ConnectionStatusColor = Brushes.Orange;
                
                _logger.LogInformation("Attempting to connect to Unity Player at {IpAddress}", IpAddress.Trim());
                
                // Try common Unity profiler ports
                var ports = new[] { 55000, 35000, 56000, 57000, 58000 };
                bool connected = false;
                
                foreach (var port in ports)
                {
                    _logger.LogInformation("Trying port {Port}...", port);
                    var success = await _connectionService.ConnectAsync(IpAddress.Trim(), port);
                    
                    if (success)
                    {
                        connected = true;
                        IsConnected = true;
                        ConnectButtonText = "Disconnect";
                        ConnectionStatusText = $"Connected (:{port})";
                        ConnectionStatusColor = Brushes.LimeGreen;
                        _logger.LogInformation("Successfully connected to Unity Player at {IpAddress}:{Port}", IpAddress.Trim(), port);
                        break;
                    }
                }
                
                if (!connected)
                {
                    ConnectButtonText = "Connect";
                    ConnectionStatusText = "Connection Failed";
                    ConnectionStatusColor = Brushes.Red;
                    
                    _logger.LogWarning("Failed to connect to Unity Player at {IpAddress} on any port", IpAddress.Trim());
                    MessageBox.Show($"Failed to connect to Unity Player at {IpAddress.Trim()}\n\nPlease ensure:\n• Unity Player is running\n• Profiler is enabled in Unity\n• IP address is correct\n• Network connectivity is available", 
                                    "Connection Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectButtonText = "Connect";
            ConnectionStatusText = "Connection Error";
            ConnectionStatusColor = Brushes.Red;
            
            _logger.LogError(ex, "Error during connection toggle");
            MessageBox.Show($"Connection error: {ex.Message}", "Connection Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        
        // Update connection state when IsConnected changes
        if (e.PropertyName == nameof(IsConnected))
        {
            // Connection state is handled by UpdateConnectionUI
        }
        
        // Handle GC CallStack toggle
        if (e.PropertyName == nameof(IsGCCallStackEnabled))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    bool success;
                    if (IsGCCallStackEnabled)
                    {
                        success = await _connectionService.EnableGCCallStackAsync();
                        if (!success)
                        {
                            // Revert the change if it failed
                            Application.Current.Dispatcher.Invoke(() => IsGCCallStackEnabled = false);
                            _logger.LogWarning("Failed to enable GC call stack");
                        }
                        else
                        {
                            _logger.LogInformation("GC call stack enabled successfully");
                        }
                    }
                    else
                    {
                        success = await _connectionService.DisableGCCallStackAsync();
                        if (!success)
                        {
                            // Revert the change if it failed
                            Application.Current.Dispatcher.Invoke(() => IsGCCallStackEnabled = true);
                            _logger.LogWarning("Failed to disable GC call stack");
                        }
                        else
                        {
                            _logger.LogInformation("GC call stack disabled successfully");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error toggling GC call stack");
                    // Revert the change if there was an exception
                    Application.Current.Dispatcher.Invoke(() => IsGCCallStackEnabled = !IsGCCallStackEnabled);
                }
            });
        }
    }

    [RelayCommand]
    private async Task CaptureMemorySnapshot()
    {
        await CaptureMemory();
    }
    
    [RelayCommand]
    private async Task CaptureObjectSnapshot()
    {
        await CaptureObjects();
    }
    
    [RelayCommand]
    private async Task ToggleGCCallStack()
    {
        if (!IsConnected)
        {
            _logger.LogWarning("Cannot toggle GC call stack - not connected");
            return;
        }

        await Task.Run(async () =>
        {
            try
            {
                bool success;
                if (IsGCCallStackEnabled)
                {
                    success = await _connectionService.EnableGCCallStackAsync();
                    if (!success)
                    {
                        // Revert the change if it failed
                        Application.Current.Dispatcher.Invoke(() => IsGCCallStackEnabled = false);
                        _logger.LogWarning("Failed to enable GC call stack");
                    }
                    else
                    {
                        _logger.LogInformation("GC call stack enabled successfully");
                    }
                }
                else
                {
                    success = await _connectionService.DisableGCCallStackAsync();
                    if (!success)
                    {
                        // Revert the change if it failed
                        Application.Current.Dispatcher.Invoke(() => IsGCCallStackEnabled = true);
                        _logger.LogWarning("Failed to disable GC call stack");
                    }
                    else
                    {
                        _logger.LogInformation("GC call stack disabled successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling GC call stack");
                // Revert the change if there was an exception
                Application.Current.Dispatcher.Invoke(() => IsGCCallStackEnabled = !IsGCCallStackEnabled);
            }
        });
    }
    
    [RelayCommand]
    private void OpenOutputFolder()
    {
        try
        {
            var outputPath = _dataStorageService.GetOutputDirectory();
            if (Directory.Exists(outputPath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outputPath,
                    UseShellExecute = true
                });
                _logger.LogInformation("Opened output folder: {Path}", outputPath);
            }
            else
            {
                _logger.LogWarning("Output folder does not exist: {Path}", outputPath);
                MessageBox.Show($"Output folder does not exist:\n{outputPath}", 
                              "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open output folder");
            MessageBox.Show("Failed to open output folder. Please check the logs for more information.", 
                          "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
    
    [RelayCommand]
    private async Task ClearOutputFiles()
    {
        try
        {
            var result = MessageBox.Show("Are you sure you want to delete all output files?\n\nThis action cannot be undone.", 
                                       "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                var outputPath = _dataStorageService.GetOutputDirectory();
                
                if (Directory.Exists(outputPath))
                {
                    await Task.Run(() =>
                    {
                        var directories = Directory.GetDirectories(outputPath);
                        var files = Directory.GetFiles(outputPath);
                        
                        foreach (var dir in directories)
                        {
                            Directory.Delete(dir, true);
                        }
                        
                        foreach (var file in files)
                        {
                            File.Delete(file);
                        }
                    });
                    
                    _logger.LogInformation("Cleared all output files from: {Path}", outputPath);
                    MessageBox.Show("All output files have been deleted successfully.", 
                                  "Files Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    _logger.LogWarning("Output folder does not exist: {Path}", outputPath);
                    MessageBox.Show($"Output folder does not exist:\n{outputPath}", 
                                  "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear output files");
            MessageBox.Show("Failed to clear output files. Please check the logs for more information.", 
                          "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}