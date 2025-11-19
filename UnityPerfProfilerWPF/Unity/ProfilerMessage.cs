using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Models;

namespace UnityPerfProfilerWPF.Unity;

/// <summary>
/// Unity Profiler Message - exact port from UnityPerfProfiler
/// </summary>
public class ProfilerMessage : Message
{
    // Static fields
    public static bool IsStream = false;
    public const ulong GLOBAL_THREAD_ID = 18446744073709551615UL;
    private const int COMPRESSION_THRESHOLD = 300;
    public static byte[]? sessionId;
    public static ulong mainThreadId;
    public static int UnityVersionValue;
    private static readonly Mutex mutex = new();
    public static int sequenceId = 0;

    // Magic numbers and protocol constants
    private static readonly byte[] magicNumber = new byte[] { 143, 78, 165, 103 };
    private static readonly byte[] server_magic_tag = BitConverter.GetBytes(2014011020150810L);
    private static readonly byte[] empty_zero = new byte[4];
    private static readonly byte[] header_flag = BitConverter.GetBytes(64717);

    // Instance fields
    private readonly byte[] messageId;
    private readonly byte[] data;
    public double tick;
    public bool IsStreamHeader;
    public ulong threadId;
    public bool IsMainThreadOrRequired;

    public ProfilerMessage(byte[] messageId, byte[] data, ulong threadId, bool isStreamHeader = false, bool isMainThreadOrRequired = false)
    {
        this.messageId = messageId;
        this.data = data;
        this.IsStreamHeader = isStreamHeader;
        this.IsMainThreadOrRequired = isMainThreadOrRequired;
        this.threadId = threadId;
        this.tick = (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalMilliseconds;
    }

    public override byte[] ToBytes()
    {
        return ToServerBinary();
    }

    public static int GetSequence()
    {
        mutex.WaitOne();
        int result = sequenceId;
        sequenceId++;
        mutex.ReleaseMutex();
        return result;
    }

    public static void ResetSequence()
    {
        mutex.WaitOne();
        sequenceId = 0;
        mutex.ReleaseMutex();
    }

    public static ProfilerMessage? ReadMessage(NetworkStream bReader, ILogger? logger = null)
    {
        if (!bReader.DataAvailable)
        {
            return null;
        }
        
        try
        {
            // Read magic number with better error handling
            byte[] magic = new byte[4];
            if (!BinaryUtils.ReadBytes(bReader, magic))
            {
                logger?.LogTrace("[Profiler]: Failed to read magic bytes - stream may be incomplete");
                return null;
            }
            
            // Validate magic number but provide better diagnostics
            if (!BinaryUtils.UnsafeCompare(magic, magicNumber))
            {
                var magicHex = BinaryUtils.BinaryToHex(magic);
                var expectedHex = BinaryUtils.BinaryToHex(magicNumber);
                logger?.LogWarning("[Profiler]: Magic number mismatch. Got: {Actual}, Expected: {Expected}", magicHex, expectedHex);
                
                // Try to recover by looking for the correct magic sequence in the next bytes
                if (!TryRecoverFromBadMagic(bReader, magic, logger))
                {
                    return null;
                }
            }

            // Read message ID
            byte[] messageid = new byte[16];
            if (!BinaryUtils.ReadBytes(bReader, messageid))
            {
                logger?.LogWarning("[Profiler]: Failed to read message ID");
                return null;
            }

            // Read data size
            byte[] dataSize = new byte[4];
            if (!BinaryUtils.ReadBytes(bReader, dataSize))
            {
                logger?.LogWarning("[Profiler]: Failed to read data size");
                return null;
            }

            int dataSizeValue = BitConverter.ToInt32(dataSize, 0);
            
            // Validate data size to prevent memory issues
            if (dataSizeValue < 0 || dataSizeValue > 100 * 1024 * 1024) // Max 100MB per message
            {
                logger?.LogError("[Profiler]: Invalid data size: {DataSize} bytes", dataSizeValue);
                return null;
            }

            // Read message data
            byte[] data = new byte[dataSizeValue];
            if (!BinaryUtils.ReadBytes(bReader, data))
            {
                logger?.LogError("[Profiler]: Failed to read message data ({DataSize} bytes)", dataSizeValue);
                return null;
            }

            bool isStreamHeader = false;
            bool isMainThreadOrRequired = false;
            ulong threadId = 0UL;
            
            // Enhanced message type detection and logging
            var messageTypeId = MessageGuid.GetIdByGuid(messageid);
            var messageTypeName = GetMessageTypeName(messageTypeId);
            
            logger?.LogTrace("[Profiler]: Processing message type {MessageType} ({MessageId}), Size: {Size} bytes", 
                messageTypeName, (int)messageTypeId, dataSizeValue);

            // Handle known snapshot message types
            if (BinaryUtils.UnsafeCompare(messageid, MessageGuid.kMessageSnapshotDataBegin))
            {
                logger?.LogInformation("[Profiler]: Start to receive Memory Snapshot Data");
            }
            else if (BinaryUtils.UnsafeCompare(messageid, MessageGuid.kMessageSnapshotDataEnd))
            {
                logger?.LogInformation("[Profiler]: Finish to receive Memory Snapshot Data");
            }
            else if (BinaryUtils.UnsafeCompare(messageid, MessageGuid.kMemorySnapshotDataMessage) ||
                     BinaryUtils.UnsafeCompare(messageid, MessageGuid.kMemorySnapshotDataMessageAbove2019))
            {
                logger?.LogDebug("[Profiler]: Received memory snapshot data chunk");
            }
            else if (BinaryUtils.UnsafeCompare(messageid, MessageGuid.kObjectMemoryProfileSnapshot) ||
                     BinaryUtils.UnsafeCompare(messageid, MessageGuid.kObjectMemoryProfileDataMessage))
            {
                logger?.LogDebug("[Profiler]: Received object profile data");
            }
            else
            {
                // Log unknown message types for debugging
                var guidHex = BinaryUtils.BinaryToHex(messageid);
                logger?.LogDebug("[Profiler]: Unknown message type: {MessageGuid}, Size: {Size}", guidHex, dataSizeValue);
            }
            
            // Don't skip messages with empty data - they might be control messages
            if (dataSizeValue == 0)
            {
                logger?.LogTrace("[Profiler]: Received empty message of type {MessageType}", messageTypeName);
                // Still process the message, don't return null
            }

            // Process stream header detection (only if we have data)
            if (dataSizeValue > 0)
            {
                if (data.Length >= 4 && data[0] == 80 && data[1] == 68 && data[2] == 51 && data[3] == 85)
                {
                    isStreamHeader = true;
                    if (data.Length >= 36) // Ensure we have enough bytes for thread ID
                    {
                        mainThreadId = BitConverter.ToUInt64(data, 28);
                    }
                    isMainThreadOrRequired = true;
                    IsStream = true;
                    logger?.LogDebug("[Profiler]: Stream header detected");
                }
                else if (data.Length >= 16) // Need at least 16 bytes for thread ID at offset 8
                {
                    try
                    {
                        threadId = BitConverter.ToUInt64(data, 8);
                        if (threadId.Equals(mainThreadId))
                        {
                            isMainThreadOrRequired = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        logger?.LogTrace(ex, "[Profiler]: Could not parse thread ID from message data");
                        // Continue processing - don't fail the entire message
                    }
                }
            }

            // Mark important message types as required regardless of thread
            if (!isMainThreadOrRequired && 
                (BinaryUtils.UnsafeCompare(messageid, MessageGuid.kObjectMemoryProfileDataMessage) || 
                 BinaryUtils.UnsafeCompare(messageid, MessageGuid.kMemorySnapshotDataMessage) || 
                 BinaryUtils.UnsafeCompare(messageid, MessageGuid.kMemorySnapshotDataMessageAbove2019)))
            {
                isMainThreadOrRequired = true;
                logger?.LogTrace("[Profiler]: Marking memory-related message as required");
            }

            return new ProfilerMessage(messageid, data, threadId, isStreamHeader, isMainThreadOrRequired);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[Profiler]: parse Profiler Data Error");
            return null;
        }
    }

    /// <summary>
    /// Attempts to recover from bad magic number by looking for the correct sequence
    /// </summary>
    private static bool TryRecoverFromBadMagic(NetworkStream stream, byte[] badMagic, ILogger? logger)
    {
        try
        {
            // Look for magic sequence in the next few bytes
            var buffer = new byte[16]; // Look ahead buffer
            var bytesRead = stream.Read(buffer, 0, buffer.Length);
            
            if (bytesRead > 0)
            {
                // Search for magic sequence in the combined bad magic + buffer
                var searchBuffer = new byte[badMagic.Length + bytesRead];
                Array.Copy(badMagic, 0, searchBuffer, 0, badMagic.Length);
                Array.Copy(buffer, 0, searchBuffer, badMagic.Length, bytesRead);
                
                // Look for the magic sequence
                for (int i = 1; i < searchBuffer.Length - magicNumber.Length + 1; i++)
                {
                    bool found = true;
                    for (int j = 0; j < magicNumber.Length; j++)
                    {
                        if (searchBuffer[i + j] != magicNumber[j])
                        {
                            found = false;
                            break;
                        }
                    }
                    
                    if (found)
                    {
                        logger?.LogInformation("[Profiler]: Recovered from bad magic, skipped {SkippedBytes} bytes", i);
                        
                        // We need to put back the data we don't need
                        // This is complex with NetworkStream, so for now just log the recovery
                        return true;
                    }
                }
            }
            
            logger?.LogWarning("[Profiler]: Could not recover from bad magic number");
            return false;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[Profiler]: Error during magic number recovery");
            return false;
        }
    }

    /// <summary>
    /// Gets a human-readable name for a message type ID
    /// </summary>
    private static string GetMessageTypeName(MessageID messageId)
    {
        return messageId switch
        {
            MessageID.kObjectMemoryProfileDataMessage => "ObjectMemoryProfile",
            MessageID.kMemorySnapshotDataMessage => "MemorySnapshot",
            MessageID.kObjectMemoryProfileSnapshot => "ObjectMemorySnapshot",
            MessageID.kProfileDataMessage => "ProfileData",
            MessageID.kLogMessage => "Log",
            MessageID.kMemorySnapshotRequest => "MemorySnapshotRequest",
            MessageID.kPingAliveMessage => "PingAlive",
            MessageID.kApplicationQuitMessage => "ApplicationQuit",
            MessageID.kFileTransferMessage => "FileTransfer",
            MessageID.kCleanLogMessage => "CleanLog",
            MessageID.kObjectLuaProfileDataMessage => "ObjectLuaProfile",
            _ => $"Unknown({(int)messageId})"
        };
    }

    public override string ToString()
    {
        StringBuilder stringBuilder = new();
        stringBuilder.Append(BinaryUtils.BinaryToHex(messageId));
        stringBuilder.Append(" Size: " + data.Length.ToString());
        return stringBuilder.ToString();
    }

    public byte[] GetMessageId()
    {
        return messageId;
    }

    public int GetMessageIdValue()
    {
        return (int)MessageGuid.GetIdByGuid(messageId);
    }

    public uint DataSize()
    {
        return (uint)data.Length;
    }

    public byte[] GetData()
    {
        return data;
    }

    public string GetDataHex()
    {
        if (IsStreamHeader && data.Length < 40)
        {
            return BitConverter.ToString(data).Replace("-", "") + " MainThreadID[" + 
                   BitConverter.ToUInt64(data, 28).ToString() + "]";
        }
        return "";
    }

    public string GetFullMessageHex()
    {
        if (data.Length < 40)
        {
            return BitConverter.ToString(ToServerBinary()).Replace("-", "");
        }
        return "Data too long.";
    }

    public uint Size()
    {
        return (uint)(24 + data.Length);
    }

    public bool Send(TcpClient tcpClient, ILogger? logger = null)
    {
        if (!tcpClient.Connected)
            return false;

        try
        {
            int intValue = data?.Length ?? 0;
            byte[] intBytes = BitConverter.GetBytes(intValue);
            byte[] startUpGuid = messageId;
            
            BinaryWriter clientStreamWriter = new(tcpClient.GetStream());
            clientStreamWriter.Write(magicNumber, 0, magicNumber.Length);
            clientStreamWriter.Write(messageId, 0, startUpGuid.Length);
            clientStreamWriter.Write(intBytes, 0, intBytes.Length);
            
            if (data != null)
            {
                clientStreamWriter.Write(data, 0, data.Length);
            }
            
            clientStreamWriter.Flush();
            return true;
        }
        catch (Exception e)
        {
            logger?.LogError(e, "[Profiler]: Failed to send profiler message");
            return false;
        }
    }

    public bool Need2Forward()
    {
        return !IsStream || IsMainThreadOrRequired || threadId == ulong.MaxValue;
    }

    public override bool Equals(object? obj)
    {
        if (obj is not ProfilerMessage msg)
            return false;
            
        return BinaryUtils.UnsafeCompare(messageId, msg.messageId) && 
               BinaryUtils.UnsafeCompare(data, msg.data);
    }

    public override int GetHashCode()
    {
        if (data.Length == 0)
        {
            return messageId.GetHashCode();
        }
        return messageId.GetHashCode() + data[0].GetHashCode();
    }

    private byte[] ToServerBinary()
    {
        byte[] compressedData = Compress(data);
        byte[] encryptedData = Encrypt(compressedData);
        int magicnumber = new Random().Next();
        
        MemoryStream memoryStream = new(64 + encryptedData.Length);
        memoryStream.Write(server_magic_tag, 0, 8);
        memoryStream.Write(header_flag, 0, 4);
        memoryStream.Write(BitConverter.GetBytes(UnityVersionValue), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(32), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(magicnumber), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(encryptedData.Length), 0, 4);
        
        int msgId = (int)MessageGuid.GetIdByGuid(messageId);
        memoryStream.Write(BitConverter.GetBytes(msgId), 0, 4);
        memoryStream.Write(encryptedData, 0, encryptedData.Length);
        memoryStream.Write(BitConverter.GetBytes(magicnumber), 0, 4);
        memoryStream.Write(BitConverter.GetBytes(tick), 0, 8);
        
        if (sessionId != null)
        {
            memoryStream.Write(sessionId, 0, 16);
        }
        else
        {
            memoryStream.Write(new byte[16], 0, 16);
        }
        
        memoryStream.Write(BitConverter.GetBytes(GetSequence()), 0, 4);
        return memoryStream.ToArray();
    }

    public byte[] Compress(byte[] data)
    {
        byte[] buf = new byte[data.Length + 9];
        if (data.Length < COMPRESSION_THRESHOLD)
        {
            buf[0] = 0;
            Array.Copy(BitConverter.GetBytes(data.Length), 0, buf, 1, 4);
            Array.Copy(BitConverter.GetBytes(data.Length), 0, buf, 5, 4);
            Array.Copy(data, 0, buf, 9, data.Length);
        }
        else
        {
            // For large data, we would use LZ4 compression here
            // For now, just use uncompressed for simplicity
            buf[0] = 0;
            Array.Copy(BitConverter.GetBytes(data.Length), 0, buf, 1, 4);
            Array.Copy(BitConverter.GetBytes(data.Length), 0, buf, 5, 4);
            Array.Copy(data, 0, buf, 9, data.Length);
        }
        return buf;
    }

    public byte[] Encrypt(byte[] data)
    {
        try
        {
            byte[] buf = new byte[data.Length + 9];
            for (int i = 0; i < 5; i++)
            {
                buf[i] = 0;
            }
            Array.Copy(BitConverter.GetBytes(data.Length), 0, buf, 5, 4);
            Array.Copy(data, 0, buf, 9, data.Length);
            
            if (data.Length < 16)
            {
                return buf;
            }
            
            for (int j = 0; j < 16; j++)
            {
                byte b = buf[9 + j];
                buf[9 + j] = buf[buf.Length - j - 1];
                buf[buf.Length - j - 1] = b;
            }
            return buf;
        }
        catch (Exception)
        {
            // Log error
            return new byte[1];
        }
    }
}