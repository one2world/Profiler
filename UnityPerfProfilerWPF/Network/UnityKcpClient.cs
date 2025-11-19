using Microsoft.Extensions.Logging;

namespace UnityPerfProfilerWPF.Network;

public class UnityKcpClient : MessageClient
{
    // TODO: Implement KCP client when needed
    // For now, this is a placeholder that falls back to TCP
    
    private readonly UnityTcpClient _tcpClient;
    
    public UnityKcpClient(MessageManagerConfig config, ILogger logger) : base(logger)
    {
        _serverHost = config.ServerHost;
        _serverPort = config.ServerPort;
        
        // For now, use TCP as fallback
        _tcpClient = new UnityTcpClient(config, logger);
        _logger.LogWarning("KCP client not fully implemented, falling back to TCP");
    }
    
    public override bool Connect(int waitSeconds = 5)
    {
        return _tcpClient.Connect(waitSeconds);
    }
    
    public override bool Send(byte[] package)
    {
        return _tcpClient.Send(package);
    }
    
    public override void Close()
    {
        _tcpClient.Close();
    }
    
    public override void Dispose()
    {
        _tcpClient?.Dispose();
        base.Dispose();
    }
}
