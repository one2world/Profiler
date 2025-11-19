using System;
using Microsoft.Extensions.Logging;

namespace UnityPerfProfilerWPF.Unity;

/// <summary>
/// Unity Profiler Message Factory - exact port from UnityPerfProfiler
/// </summary>
public static class ProfilerMessageFactory
{
    // Message data bytes - exact from Unity protocol
    private static readonly byte[] enableBytes;
    private static readonly byte[] enableBytes201702;
    private static readonly byte[] enableBytes201803;
    private static readonly byte[] disableBytes;
    private static readonly byte[] snapshotFlagBytes;
    private static readonly byte[] enableGCCallStackBytes;
    private static readonly byte[] disableGCCallStackBytes;

    static ProfilerMessageFactory()
    {
        byte[] array = new byte[4];
        array[0] = 1;
        enableBytes = array;

        byte[] array2 = new byte[4];
        array2[0] = 3;
        enableBytes201702 = array2;

        byte[] array3 = new byte[4];
        array3[0] = 253;
        array3[1] = 31;
        enableBytes201803 = array3;

        disableBytes = new byte[4];

        byte[] array4 = new byte[4];
        array4[0] = 3;
        snapshotFlagBytes = array4;

        byte[] array5 = new byte[4];
        array5[0] = 1;
        enableGCCallStackBytes = array5;

        disableGCCallStackBytes = new byte[4];
    }

    public static ProfilerMessage GetEnableProfileMessage(ILogger? logger = null)
    {
        try
        {
            logger?.LogInformation("Unity Version Hex {UnityVersion}", ProfilerMessage.UnityVersionValue.ToString("X4"));
            
            if (ProfilerMessage.UnityVersionValue >= 538444550)
            {
                return new ProfilerMessage(MessageGuid.kProfileStartupInformation, enableBytes201803, 0UL, false, false);
            }
            else if (ProfilerMessage.UnityVersionValue <= 538380585)
            {
                return new ProfilerMessage(MessageGuid.kProfileStartupInformation, enableBytes201702, 0UL, false, false);
            }
            else
            {
                return new ProfilerMessage(MessageGuid.kProfileStartupInformation, enableBytes, 0UL, false, false);
            }
        }
        catch (Exception e)
        {
            throw new Exception("Failed to get enable profile message. Brief description:" + e?.ToString());
        }
    }

    public static ProfilerMessage GetDisableProfileMessage()
    {
        try
        {
            return new ProfilerMessage(MessageGuid.kProfileStartupInformation, disableBytes, 0UL, false, false);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to get disable profile message. Brief description:" + e?.ToString());
        }
    }

    public static ProfilerMessage GetCaptureMemoryMessage()
    {
        try
        {
            return new ProfilerMessage(MessageGuid.kObjectMemoryProfileSnapshot, disableBytes, 0UL, false, false);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to get capture memory message. Brief description:" + e?.ToString());
        }
    }

    public static ProfilerMessage GetCaptureMemorySnapshotMessage()
    {
        try
        {
            return new ProfilerMessage(MessageGuid.kMemorySnapshotRequest, snapshotFlagBytes, 0UL, false, false);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to get capture memory snapshot message. Brief description:" + e?.ToString());
        }
    }

    public static ProfilerMessage GetEnableGcCallStackMessage()
    {
        try
        {
            return new ProfilerMessage(MessageGuid.kProfilerSetMemoryRecordMode, enableGCCallStackBytes, 0UL, false, false);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to get capture memory snapshot message. Brief description:" + e?.ToString());
        }
    }

    public static ProfilerMessage GetDisableGcCallStackMessage()
    {
        try
        {
            return new ProfilerMessage(MessageGuid.kProfilerSetMemoryRecordMode, disableGCCallStackBytes, 0UL, false, false);
        }
        catch (Exception e)
        {
            throw new Exception("Failed to get capture memory snapshot message. Brief description:" + e?.ToString());
        }
    }
}