using System.Linq;
using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Models;
using UnityPerfProfilerWPF.Utils;

namespace UnityPerfProfilerWPF.Network;

public abstract class MessageClient : IDisposable
{
    protected readonly ILogger _logger;
    protected MessageMeter? _meter;
    protected string _serverHost = string.Empty;
    protected int _serverPort;
    
    protected MessageClient(ILogger logger)
    {
        _logger = logger;
    }
    
    public static MessageClient Create(MessageManagerConfig config, ILogger logger)
    {
        MessageClient client = config.TransportType switch
        {
            TransportType.Tcp => new UnityTcpClient(config, logger),
            TransportType.Kcp => new UnityKcpClient(config, logger),
            _ => throw new ArgumentException($"Invalid transport type: {config.TransportType}")
        };
        
        client._meter = config.Meter;
        
        if (client.TryConnect())
        {
            return client;
        }
        
        throw new InvalidOperationException("Message Client connect failed");
    }
    
    public bool Send(Message message)
    {
        return Send(message.ToBytes());
    }
    
    public abstract bool Send(byte[] package);
    public abstract bool Connect(int waitSeconds);
    public abstract void Close();
    
    public bool TryConnect()
    {
        Close();
        
        int[] timeouts = { 4, 6, 9, 18, 24 };
        return timeouts.Any(Connect);
    }
    
    public virtual void Dispose()
    {
        Close();
    }
}
