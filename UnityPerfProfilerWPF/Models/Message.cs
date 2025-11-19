namespace UnityPerfProfilerWPF.Models;

/// <summary>
/// Base class for Unity profiler messages
/// Abstract base for all message types used in Unity protocol communication
/// </summary>
public abstract class Message
{
    /// <summary>
    /// Convert message to byte array for network transmission
    /// </summary>
    /// <returns>Message as byte array</returns>
    public abstract byte[] ToBytes();
}