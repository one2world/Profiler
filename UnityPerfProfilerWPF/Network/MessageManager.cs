using System.Collections.Concurrent;
using System.Threading;
using UnityPerfProfilerWPF.Models;
using UnityPerfProfilerWPF.Utils;
using Microsoft.Extensions.Logging;

namespace UnityPerfProfilerWPF.Network;

public class MessageManager : IDisposable
{
    private readonly ILogger<MessageManager> _logger;
    private readonly MessageManagerConfig _config;
    private readonly ConcurrentQueue<Message> _sendingQueue;
    private readonly Thread _sendingThread;
    private readonly MessageClient _socketClient;
    private readonly PersistentBuffer _buffer;
    private readonly bool _busyMode;
    private StatusCode _status;
    private Action? _onFinishedCallback;
    private readonly object _statusLock = new object();
    
    private static int MaxLength => (int)UPRContext.MessageThreshold;
    private static int BusyThreshold => Convert.ToInt32(MaxLength * 0.8);
    private static int NormalThreshold => Convert.ToInt32(MaxLength * 0.6);
    
    public int MessageCount => _sendingQueue.Count;
    
    public MessageManager(MessageManagerConfig config, ILogger<MessageManager> logger)
    {
        _logger = logger;
        _config = config;
        _status = StatusCode.Normal;
        _sendingQueue = new ConcurrentQueue<Message>();
        _buffer = new PersistentBuffer(logger);
        _socketClient = MessageClient.Create(config, logger);
        _busyMode = config.BusyMode;
        
        _sendingThread = new Thread(SendingThread)
        {
            Name = "MessageManager-SendingThread",
            IsBackground = true
        };
        _sendingThread.Start();
    }
    
    public void Send(Message message)
    {
        if (!_busyMode)
        {
            _sendingQueue.Enqueue(message);
            return;
        }
        
        if (UPRContext.Debug_MessageBusyMode)
        {
            lock (_buffer)
            {
                if (!_buffer.Append(message))
                {
                    _sendingQueue.Enqueue(_buffer.GenerateNode());
                    _buffer.Flush(500);
                    _buffer.Append(message);
                }
            }
            return;
        }
        
        lock (_statusLock)
        {
            switch (_status)
            {
                case StatusCode.Normal:
                    _sendingQueue.Enqueue(message);
                    if (_sendingQueue.Count > BusyThreshold)
                    {
                        _status = StatusCode.Busy;
                        _logger.LogInformation("Message sending changed to busy mode");
                    }
                    break;
                    
                case StatusCode.Busy:
                    lock (_buffer)
                    {
                        if (!_buffer.Append(message))
                        {
                            _sendingQueue.Enqueue(_buffer.GenerateNode());
                            _buffer.Flush(500);
                            
                            if (_sendingQueue.Count < NormalThreshold)
                            {
                                _status = StatusCode.Normal;
                                _sendingQueue.Enqueue(message);
                                _logger.LogInformation("Message sending back to normal mode");
                            }
                            else
                            {
                                _buffer.Append(message);
                            }
                        }
                    }
                    break;
                    
                default:
                    throw new InvalidOperationException("Message sending request rejected by closing/closed queue");
            }
        }
    }
    
    public void Stop(Action? callback = null)
    {
        lock (_statusLock)
        {
            if (_status == StatusCode.Busy)
            {
                _buffer.Flush(500);
            }
            _status = StatusCode.Stopping;
            _onFinishedCallback = callback;
        }
    }
    
    private void SendingThread()
    {
        try
        {
            while (true)
            {
                StatusCode currentStatus;
                lock (_statusLock)
                {
                    currentStatus = _status;
                }
                
                if (currentStatus == StatusCode.Stopped)
                    break;
                
                if (_sendingQueue.TryDequeue(out var message))
                {
                    if (!_socketClient.Send(message))
                    {
                        _logger.LogDebug("Send message error, retrying");
                        
                        if (!_socketClient.Send(message) && 
                            (!_socketClient.TryConnect() || !_socketClient.Send(message)))
                        {
                            _logger.LogError("Failed to reconnect and send message");
                            break;
                        }
                    }
                }
                else
                {
                    lock (_statusLock)
                    {
                        if (_status == StatusCode.Stopping)
                        {
                            _status = StatusCode.Stopped;
                        }
                    }
                    Thread.Sleep(100);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in message sending thread");
        }
        finally
        {
            _socketClient.Close();
            _onFinishedCallback?.Invoke();
        }
    }
    
    public bool CanSend()
    {
        lock (_statusLock)
        {
            return _status != StatusCode.Stopped && _status != StatusCode.Stopping;
        }
    }
    
    public void Dispose()
    {
        Stop();
        
        // Wait for thread to finish (with timeout)
        if (_sendingThread.IsAlive)
        {
            if (!_sendingThread.Join(5000))
            {
                _logger.LogWarning("Sending thread did not finish within timeout");
            }
        }
        
        _socketClient?.Dispose();
    }
    
    public enum StatusCode
    {
        Normal,
        Busy,
        Stopping,
        Stopped
    }
    
    private class PersistentBuffer
    {
        private readonly ILogger _logger;
        private readonly List<byte> _bytesBuffer;
        private int _countDown;
        private FileMessage _fileMessage;
        
        public PersistentBuffer(ILogger logger)
        {
            _logger = logger;
            _bytesBuffer = new List<byte>();
            _fileMessage = new FileMessage();
        }
        
        public FileMessage GenerateNode()
        {
            if (_bytesBuffer.Count > 0)
            {
                _fileMessage.Store(_bytesBuffer.ToArray());
            }
            return _fileMessage;
        }
        
        public void Flush(int bufferSize = 500)
        {
            _countDown = bufferSize;
            if (_bytesBuffer.Count > 0)
            {
                _bytesBuffer.Clear();
            }
            _fileMessage = new FileMessage();
        }
        
        public bool Append(Message message)
        {
            if (_countDown <= 0)
            {
                return false;
            }
            
            try
            {
                _bytesBuffer.AddRange(message.ToBytes());
                _countDown--;
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error appending message to buffer. Message size: {MessageSize}", 
                    message.ToBytes().Length);
                return false;
            }
        }
    }
}
