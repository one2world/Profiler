using System;
using System.IO;
using Microsoft.Extensions.Logging;
using UnityPerfProfilerWPF.Utils;

namespace UnityPerfProfilerWPF.Models;

/// <summary>
/// File-backed message for storing large Unity profiler data
/// Based on Unity's FileMessage class for handling large data transfers
/// </summary>
public class FileMessage : Message
{
    private readonly string _dumpPath;
    private readonly ILogger? _logger;

    public FileMessage(ILogger? logger = null)
    {
        _logger = logger;
        _dumpPath = GetRandomMessageDumpFile();
        TouchFile(_dumpPath);
    }

    /// <summary>
    /// Store bytes to the backing file
    /// </summary>
    /// <param name="bytes">Data to store</param>
    public void Store(byte[] bytes)
    {
        try
        {
            if (!File.Exists(_dumpPath))
            {
                _logger?.LogDebug("Dump file doesn't exist: {DumpPath}", _dumpPath);
                return;
            }

            using var stream = new FileStream(_dumpPath, FileMode.Append);
            stream.Write(bytes, 0, bytes.Length);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing bytes to: {DumpPath}", _dumpPath);
        }
    }

    /// <summary>
    /// Convert stored file data to bytes
    /// </summary>
    /// <returns>File contents as byte array</returns>
    public override byte[] ToBytes()
    {
        try
        {
            return File.ReadAllBytes(_dumpPath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading bytes from: {DumpPath}", _dumpPath);
            return Array.Empty<byte>();
        }
    }

    private static string GetRandomMessageDumpFile()
    {
        var tempPath = UPRContext.TempPath;
        var fileName = $"unity_profiler_msg_{Guid.NewGuid():N}.tmp";
        return Path.Combine(tempPath, fileName);
    }

    private static void TouchFile(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                File.Create(path).Dispose();
            }
        }
        catch
        {
            // Ignore file creation errors
        }
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_dumpPath))
            {
                File.Delete(_dumpPath);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to delete dump file: {DumpPath}", _dumpPath);
        }
    }
}