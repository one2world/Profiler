using System.IO;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace UnityPerfProfilerWPF.Network;

public class UnityTcpClient : MessageClient
{
    private TcpClient? _internalClient;
    private BinaryWriter? _writer;
    
    public UnityTcpClient(MessageManagerConfig config, ILogger logger) : base(logger)
    {
        _serverHost = config.ServerHost;
        _serverPort = config.ServerPort;
    }
    
    public override bool Connect(int waitSeconds = 5)
    {
        try
        {
            _internalClient = new TcpClient();
            
            var connectResult = _internalClient.BeginConnect(_serverHost, _serverPort, null, null);
            if (!connectResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(waitSeconds)))
            {
                _logger.LogWarning("TCP connection timeout to {ServerHost}:{ServerPort}", _serverHost, _serverPort);
                return false;
            }
            
            if (!_internalClient.Connected)
            {
                _logger.LogWarning("TCP connection failed to {ServerHost}:{ServerPort}", _serverHost, _serverPort);
                return false;
            }
            
            _writer = new BinaryWriter(_internalClient.GetStream());
            _logger.LogInformation("TCP connected to {ServerHost}:{ServerPort}", _serverHost, _serverPort);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting TCP client to {ServerHost}:{ServerPort}", _serverHost, _serverPort);
            return false;
        }
    }
    
    public override bool Send(byte[] bytes)
    {
        try
        {
            if (_writer == null || _internalClient?.Connected != true)
            {
                _logger.LogWarning("TCP client not connected, cannot send message");
                return false;
            }
            
            _writer.Write(bytes, 0, bytes.Length);
            _writer.Flush();
            
            _meter?.IncreaseCount(bytes.Length);
            _logger.LogTrace("Sent {BytesCount} bytes via TCP", bytes.Length);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending TCP message");
            return false;
        }
    }
    
    public override void Close()
    {
        try
        {
            _writer?.Close();
            _writer = null;
            
            _internalClient?.Close();
            _internalClient = null;
            
            _logger.LogInformation("TCP client closed");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error closing TCP client");
        }
    }
    
    public override void Dispose()
    {
        Close();
        base.Dispose();
    }
}
