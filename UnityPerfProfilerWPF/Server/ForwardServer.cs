using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Models;
using UnityPerfProfilerWPF.Network;
using UnityPerfProfilerWPF.Unity;
using UnityPerfProfilerWPF.Utils;

namespace UnityPerfProfilerWPF.Server;

public class ForwardServer : IDisposable
{
    public enum Status
    {
        Stopped,
        Running,
        Stopping
    }
    
    public enum ConnectionStatus
    {
        Disconnected,
        Connecting,
        Connected
    }
    
    private readonly ILogger<ForwardServer> _logger;
    private readonly MessageMeter _receiveMeter = new MessageMeter(30);
    private readonly MessageMeter _outputMeter = new MessageMeter(30);
    private readonly ConcurrentQueue<ProfilerMessage> _messageQueue = new ConcurrentQueue<ProfilerMessage>();
    private readonly ConcurrentQueue<Dictionary<string, string>> _requestQueue = new ConcurrentQueue<Dictionary<string, string>>();
    
    private Status _serverStatus = Status.Stopped;
    private ConnectionStatus _playerConnectionStatus = ConnectionStatus.Disconnected;
    private ConnectionStatus _upaServerConnectionStatus = ConnectionStatus.Disconnected;
    
#pragma warning disable CS0414 // Field is assigned but its value is never used
    private ConnectionStatus _fileConnectionStatus = ConnectionStatus.Disconnected;
    private ConnectionStatus _packageConnectionStatus = ConnectionStatus.Disconnected;
    private ConnectionStatus _luaConnectionStatus = ConnectionStatus.Disconnected;
#pragma warning restore CS0414
    
    private MessageManager? _playerMessageManager;
    private MessageManager? _fileMessageManager;
    
    private Thread? _tcpThread;
    private Thread? _playerSendThread;
    private Thread? _playerReceiveThread;
    private Thread? _monitorThread;
    private Thread? _requestQueueThread;
    
    private TcpClient? _playerTcpClient;
    private NetworkStream? _playerStream;
    
    private string _playerIp = "";
    private int _playerPort;
    private string _playerName = "";
    private string _errorMessage = string.Empty;
    
    private static ForwardServer? _instance;
    private static readonly object _instanceLock = new object();
    
    // Events
    public event Action? OnFinished;
    public event Action<string, Exception>? OnStoppedWithError;
#pragma warning disable CS0067 // Event is never used
    public event Action<string, string>? OnOutputLogChange;
#pragma warning restore CS0067
    public event Action? OnConnectionStatusChange;
    
    private ForwardServer(ILogger<ForwardServer> logger)
    {
        _logger = logger;
    }
    
    public static ForwardServer GetInstance(ILogger<ForwardServer>? logger = null)
    {
        if (_instance == null)
        {
            lock (_instanceLock)
            {
                _instance ??= new ForwardServer(logger ?? throw new ArgumentNullException(nameof(logger)));
            }
        }
        return _instance;
    }
    
    public Status ServerStatus
    {
        get => _serverStatus;
        private set
        {
            _serverStatus = value;
            UpdateStatus();
        }
    }
    
    public ConnectionStatus PlayerConnectionStatus
    {
        get => _playerConnectionStatus;
        private set
        {
            _playerConnectionStatus = value;
            UpdateStatus();
        }
    }
    
    public ConnectionStatus UPAServerConnectionStatus
    {
        get => _upaServerConnectionStatus;
        private set
        {
            _upaServerConnectionStatus = value;
            UpdateStatus();
        }
    }
    
    public string ErrorMessage => _errorMessage;
    public int PendingMessageCount => _messageQueue.Count;
    public int[] MeterData => _receiveMeter.GetMeterData();
    public int[] OutputMeterData => _outputMeter.GetMeterData();
    
    private void UpdateStatus()
    {
        try
        {
            _logger.LogDebug("Status Update - Server: {ServerStatus}, Player: {PlayerStatus}, UPA: {UPAStatus}",
                _serverStatus, _playerConnectionStatus, _upaServerConnectionStatus);
            OnConnectionStatusChange?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update status");
        }
    }
    
    public void Start(string sessionId, string serverHost, int serverPort, string unityVersion, string playerIp, int playerPort, string playerName)
    {
        if (ServerStatus != Status.Stopped)
        {
            _logger.LogWarning("Server is already running or stopping");
            return;
        }
        
        try
        {
            _logger.LogInformation("Starting ForwardServer - Session: {SessionId}, Player: {PlayerIp}:{PlayerPort}", sessionId, playerIp, playerPort);
            
            UPRContext.SessionId = sessionId;
            _errorMessage = string.Empty;
            _playerIp = playerIp;
            _playerPort = playerPort;
            _playerName = playerName;
            
            InitParams();
            
            // Initialize ProfilerMessage with session data
            ProfilerMessage.sessionId = BinaryUtils.SessionGUID2Bytes(sessionId.Replace("-", string.Empty));
            ProfilerMessage.UnityVersionValue = (int)UnityVersionMapping.GetVersionValueByString(unityVersion);
            ProfilerMessage.ResetSequence();
            
            // Setup message managers
            var playerConfig = new MessageManagerConfig
            {
                ServerHost = UPRContext.ServerHost,
                ServerPort = UPRContext.UseKcp ? UPRContext.ServerKcpPort : UPRContext.ServerPort,
                Meter = _outputMeter,
                BusyMode = true,
                TransportType = UPRContext.UseKcp ? TransportType.Kcp : TransportType.Tcp
            };
            
            var fileConfig = new MessageManagerConfig
            {
                ServerHost = UPRContext.ServerHost,
                ServerPort = UPRContext.FileServerPort,
                Meter = _outputMeter,
                TransportType = TransportType.Tcp,
                BusyMode = false
            };
            
            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            _playerMessageManager = new MessageManager(playerConfig, loggerFactory.CreateLogger<MessageManager>());
            _fileMessageManager = new MessageManager(fileConfig, loggerFactory.CreateLogger<MessageManager>());
            
            UPAServerConnectionStatus = ConnectionStatus.Connected;
            ServerStatus = Status.Running;
            
            // Start threads
            _monitorThread = new Thread(ServerMonitor) { Name = "ForwardServer-Monitor", IsBackground = true };
            _monitorThread.Start();
            
            _tcpThread = new Thread(ConnectAndProcessPlayer) { Name = "ForwardServer-PlayerConnection", IsBackground = true };
            _tcpThread.Start();
            
            _requestQueueThread = new Thread(RequestDataToServer) { Name = "ForwardServer-RequestQueue", IsBackground = true };
            _requestQueueThread.Start();
            
            _logger.LogInformation("ForwardServer started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ForwardServer");
            OnStoppedWithError?.Invoke("Failed to start server", ex);
        }
    }
    
    private void InitParams()
    {
        ProfilerMessage.IsStream = false;
        _receiveMeter.Reset();
        _outputMeter.Reset();
        
        while (_messageQueue.TryDequeue(out _)) { }
        while (_requestQueue.TryDequeue(out _)) { }
        
        _logger.LogDebug("ForwardServer parameters initialized");
    }
    
    private void ConnectAndProcessPlayer()
    {
        try
        {
            _logger.LogDebug("Starting player connection thread");
            PlayerConnectionStatus = ConnectionStatus.Connecting;
            
            _playerTcpClient = ConnectToSelectedPlayer();
            if (_playerTcpClient == null)
            {
                _logger.LogError("Cannot find Unity Game Player");
                _errorMessage = "Cannot find Unity Game Player";
                ServerStatus = Status.Stopping;
                return;
            }
            
            PlayerConnectionStatus = ConnectionStatus.Connected;
            _logger.LogInformation("Connected to Unity Game Player");
            
            _playerStream = _playerTcpClient.GetStream();
            var enableProfileMessage = ProfilerMessageFactory.GetEnableProfileMessage();
            UPRContext.ReceivedFirstFrame = false;
            
            // Start player communication threads
            _playerSendThread = new Thread(MessageToPlayer) { Name = "ForwardServer-PlayerSend", IsBackground = true };
            _playerSendThread.Start();
            
            _playerReceiveThread = new Thread(MessageFromPlayer) { Name = "ForwardServer-PlayerReceive", IsBackground = true };
            _playerReceiveThread.Start();
            
            // Send initial profile enable message
            if (!enableProfileMessage.Send(_playerTcpClient))
            {
                _logger.LogWarning("Failed to send start message to player");
            }
            
            // Configure GC call stack
            if (UPRContext.EnableGCCallStack)
            {
                _messageQueue.Enqueue(ProfilerMessageFactory.GetEnableGcCallStackMessage());
            }
            else
            {
                _messageQueue.Enqueue(ProfilerMessageFactory.GetDisableGcCallStackMessage());
            }
        }
        catch (Exception ex)
        {
            if (ex is not ThreadAbortException)
            {
                _logger.LogError(ex, "Error in player connection thread");
            }
        }
        
        _logger.LogDebug("Player connection thread ended");
    }
    
    private TcpClient? ConnectToSelectedPlayer()
    {
        int gameStartPort = UPRContext.PlayerPort;
        int gameEndPort = gameStartPort + 511;
        
        _logger.LogDebug("Attempting to connect to player at {PlayerIp}:{StartPort}-{EndPort}", _playerIp, gameStartPort, gameEndPort);
        
        if (_playerPort == 0)
        {
            return TryToConnectToClient(_playerIp, gameStartPort, gameEndPort, "player");
        }
        
        var tcpClient = new TcpClient();
        try
        {
            tcpClient.Connect(_playerIp, _playerPort);
            _logger.LogInformation("Connected to player: {PlayerName} IP: {PlayerIp}:{PlayerPort}", _playerName, _playerIp, _playerPort);
            return tcpClient.Connected ? tcpClient : null;
        }
        catch (SocketException ex)
        {
            _logger.LogWarning(ex, "Failed to connect to player at {PlayerIp}:{PlayerPort}", _playerIp, _playerPort);
            return null;
        }
    }
    
    private TcpClient? TryToConnectToClient(string ip, int startPort, int endPort, string type)
    {
        int[] timeouts = { 20, 40, 80, 160, 320 };
        var ports = new List<int>();
        
        for (int i = startPort; i <= endPort; i++)
        {
            ports.Add(i);
        }
        
        if (type == "player")
        {
            ports.Add(35000); // Add default Unity profiler port
        }
        
        foreach (var timeout in timeouts)
        {
            foreach (var port in ports)
            {
                try
                {
                    var client = new TcpClient();
                    var connectResult = client.BeginConnect(ip, port, null, null);
                    
                    if (connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromMilliseconds(timeout)))
                    {
                        if (client.Connected)
                        {
                            _logger.LogInformation("Connected to {Type} at {IP}:{Port} (timeout: {Timeout}ms)", type, ip, port, timeout);
                            
                            if (type == "player")
                            {
                                _playerPort = port;
                            }
                            
                            return client;
                        }
                    }
                    
                    client.Close();
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Failed to connect to {IP}:{Port} (timeout: {Timeout}ms)", ip, port, timeout);
                }
            }
        }
        
        _logger.LogWarning("Failed to connect to {Type} at {IP} (tried ports {StartPort}-{EndPort})", type, ip, startPort, endPort);
        return null;
    }
    
    private void MessageToPlayer()
    {
        _logger.LogDebug("Starting message to player thread");
        
        try
        {
            while (ServerStatus == Status.Running || ServerStatus == Status.Stopping)
            {
                if (_messageQueue.TryDequeue(out var message))
                {
                    if (_playerTcpClient?.Connected == true)
                    {
                        try
                        {
                            if (!message.Send(_playerTcpClient))
                            {
                                // Try to reconnect and retry
                                _playerTcpClient = ConnectToSelectedPlayer();
                                var success = message.Send(_playerTcpClient);
                                _logger.LogDebug("Reconnect and retry send result: {Success}", success);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send message to player");
                        }
                    }
                    
                    // Check if this is a disable profile message (stop condition)
                    if (message.Equals(ProfilerMessageFactory.GetDisableProfileMessage()))
                    {
                        break;
                    }
                }
                else
                {
                    if (ServerStatus == Status.Stopped || ServerStatus == Status.Stopping)
                    {
                        break;
                    }
                    Thread.Sleep(50);
                }
            }
            
            _playerTcpClient?.Close();
        }
        catch (Exception ex)
        {
            if (ex is not ThreadAbortException)
            {
                _logger.LogError(ex, "Error in message to player thread");
            }
        }
        finally
        {
            _logger.LogInformation("Message to player thread finished");
        }
    }
    
    private void MessageFromPlayer()
    {
        _logger.LogDebug("Starting message from player thread");
        
        var enableMessage = ProfilerMessageFactory.GetEnableProfileMessage();
        var stopMessage = ProfilerMessageFactory.GetDisableProfileMessage();
        
        int sleepCount = 1;
        int totalMessages = 0;
        int totalEnqueuedMessages = 0;
        bool hasReceived = false;
        
        try
        {
            while (ServerStatus == Status.Running)
            {
                if (_playerStream?.DataAvailable == true)
                {
                    hasReceived = true;
                    var message = ProfilerMessage.ReadMessage(_playerStream);
                    
                    if (message != null)
                    {
                        _receiveMeter.IncreaseCount((int)message.DataSize());
                        totalMessages++;
                        
                        if (message.Need2Forward())
                        {
                            totalEnqueuedMessages++;
                            
                            // Handle memory snapshot messages
                            if (message.GetMessageIdValue() == 43) // Memory snapshot
                            {
                                _logger.LogInformation("Memory snapshot message received");
                                // TODO: Process memory snapshot
                            }
                            
                            _playerMessageManager?.Send(message);
                            
                            if (totalEnqueuedMessages >= UPRContext.TotalEnqueueMessages)
                            {
                                _logger.LogDebug("Messages processed: {Total}, Enqueued: {Enqueued}, Sleep count: {Sleep}",
                                    totalMessages, totalEnqueuedMessages, sleepCount);
                                sleepCount = 0;
                                totalEnqueuedMessages = 0;
                                totalMessages = 0;
                            }
                        }
                    }
                }
                else
                {
                    // Handle no data scenarios
                    if (!hasReceived && sleepCount % 2000 == 500)
                    {
                        if (_playerTcpClient?.Connected != true)
                        {
                            _playerTcpClient = ConnectToSelectedPlayer();
                        }
                        
                        enableMessage.Send(_playerTcpClient);
                        _logger.LogInformation("Resent start instruction to player");
                    }
                    
                    sleepCount++;
                    Thread.Sleep(UPRContext.SleepIntervalMilliseconds);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message from player thread");
        }
        finally
        {
            PlayerConnectionStatus = ConnectionStatus.Disconnected;
            _logger.LogInformation("Message from player thread finished");
        }
    }
    
    private void ServerMonitor()
    {
        _logger.LogDebug("Starting server monitor thread");
        float logCountSeconds = 0f;
        
        while (ServerStatus != Status.Stopped)
        {
            Thread.Sleep(200);
            logCountSeconds += 0.2f;
            
            if (logCountSeconds > 10f)
            {
                logCountSeconds = 0f;
                UpdateStatus();
                
                var receivedData = MeterData;
                var sentData = OutputMeterData;
                
                _logger.LogInformation("Player Speed: {ReceiveSpeed}/s, Server Speed: {SendSpeed}/s",
                    FormatSpeed(receivedData[0]), FormatSpeed(sentData[0]));
            }
        }
        
        _logger.LogDebug("Server monitor thread finished");
    }
    
    private void RequestDataToServer()
    {
        _logger.LogDebug("Starting request data to server thread");
        
        while (ServerStatus == Status.Running || ServerStatus == Status.Stopping || !_requestQueue.IsEmpty)
        {
            try
            {
                if (_requestQueue.TryDequeue(out var requestNode))
                {
                    var type = requestNode["type"];
                    var serverUrl = requestNode["serverUrl"];
                    var json = requestNode["json"];
                    
                    // TODO: Implement network request sending
                    _logger.LogDebug("Processing request: {Type} to {ServerUrl}", type, serverUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error processing request data");
            }
            
            Thread.Sleep(100);
        }
        
        _logger.LogDebug("Request data to server thread finished");
    }
    
    public void CaptureMemorySnapshot()
    {
        _logger.LogInformation("Requesting memory snapshot capture");
        
        try
        {
            _messageQueue.Enqueue(ProfilerMessageFactory.GetCaptureMemorySnapshotMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue memory snapshot capture message");
            throw;
        }
    }
    
    public void CaptureObjectSnapshot()
    {
        if (UPRContext.EnableAutoObject)
        {
            _logger.LogDebug("Auto object snapshot already enabled");
            return;
        }
        
        _logger.LogInformation("Requesting object snapshot capture");
        
        try
        {
            _messageQueue.Enqueue(ProfilerMessageFactory.GetCaptureMemoryMessage());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enqueue object snapshot capture message");
            throw;
        }
    }
    
    public void Stop(bool innerStop = false)
    {
        _logger.LogInformation("Stopping ForwardServer");
        
        _messageQueue.Enqueue(ProfilerMessageFactory.GetDisableProfileMessage());
        ServerStatus = Status.Stopping;
        
        _playerMessageManager?.Stop(() =>
        {
            UPAServerConnectionStatus = ConnectionStatus.Disconnected;
            _logger.LogInformation("Player message manager stopped");
        });
        
        _fileMessageManager?.Stop(() =>
        {
            _logger.LogInformation("File message manager stopped");
        });
        
        // Clean up state
        Task.Run(() =>
        {
            Thread.Sleep(1500);
            
            ServerStatus = Status.Stopped;
            UPRContext.ReceivedFirstFrame = false;
            
            OnFinished?.Invoke();
            _logger.LogInformation("ForwardServer stopped");
        });
    }
    
    public void ForceStop()
    {
        _logger.LogInformation("Force stopping ForwardServer");
        
        if (ServerStatus != Status.Stopping)
        {
            Stop(false);
        }
        
        // Force terminate threads
        var threadsToAbort = new[] { _tcpThread, _playerSendThread, _playerReceiveThread };
        
        foreach (var thread in threadsToAbort)
        {
            if (thread?.IsAlive == true)
            {
                try
                {
                    if (!thread.Join(500))
                    {
                        _logger.LogWarning("Force terminating thread: {ThreadName}", thread.Name);
                        // Note: Thread.Abort is not available in .NET Core+
                        // We rely on proper cancellation tokens and thread cooperation
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error stopping thread: {ThreadName}", thread.Name);
                }
            }
        }
        
        ServerStatus = Status.Stopped;
        Thread.Sleep(500);
        
        OnFinished?.Invoke();
    }
    
    private static string FormatSpeed(int bytesPerSecond)
    {
        if (bytesPerSecond < 1024)
            return $"{bytesPerSecond} B";
        if (bytesPerSecond < 1024 * 1024)
            return $"{bytesPerSecond / 1024.0:F1} KB";
        return $"{bytesPerSecond / (1024.0 * 1024.0):F1} MB";
    }
    
    public void Dispose()
    {
        if (ServerStatus != Status.Stopped)
        {
            Stop();
        }
        
        // Wait for cleanup
        Thread.Sleep(1000);
        
        _playerMessageManager?.Dispose();
        _fileMessageManager?.Dispose();
        
        _playerTcpClient?.Close();
        _playerStream?.Close();
        
        GC.SuppressFinalize(this);
    }
}
