using System.Collections.Generic;

namespace UnityPerfProfilerWPF.Utils;

/// <summary>
/// Unity version to numeric value mapping
/// Based on Unity's version encoding system for profiler protocol
/// </summary>
public static class UnityVersionMapping
{
    private static readonly Dictionary<string, uint> VersionMap = new()
    {
        // Unity LTS versions
        { "2019.4.x", 538444550 },
        { "2020.3.x", 538444601 },
        { "2021.3.x", 538444652 },
        { "2022.3.x", 538444703 },
        { "2023.2.x", 538444750 },
        
        // Common Unity versions
        { "2019.4", 538444550 },
        { "2020.3", 538444601 },
        { "2021.3", 538444652 },
        { "2022.3", 538444703 },
        { "2023.1", 538444730 },
        { "2023.2", 538444750 },
        
        // Default fallback
        { "default", 538444550 }
    };
    
    /// <summary>
    /// Get numeric version value for Unity version string
    /// </summary>
    /// <param name="versionString">Unity version (e.g., "2021.3.x")</param>
    /// <returns>Numeric version value for Unity protocol</returns>
    public static uint GetVersionValueByString(string versionString)
    {
        if (string.IsNullOrEmpty(versionString))
        {
            return VersionMap["default"];
        }
        
        // Try exact match first
        if (VersionMap.TryGetValue(versionString, out var exactValue))
        {
            return exactValue;
        }
        
        // Try major.minor match (remove patch version)
        var parts = versionString.Split('.');
        if (parts.Length >= 2)
        {
            var majorMinor = $"{parts[0]}.{parts[1]}";
            if (VersionMap.TryGetValue(majorMinor, out var majorMinorValue))
            {
                return majorMinorValue;
            }
            
            // Try with .x suffix
            var majorMinorX = $"{parts[0]}.{parts[1]}.x";
            if (VersionMap.TryGetValue(majorMinorX, out var majorMinorXValue))
            {
                return majorMinorXValue;
            }
        }
        
        // Fallback to default
        return VersionMap["default"];
    }
    
    /// <summary>
    /// Get version string from numeric value
    /// </summary>
    /// <param name="versionValue">Numeric version value</param>
    /// <returns>Unity version string</returns>
    public static string GetVersionStringByValue(uint versionValue)
    {
        foreach (var kvp in VersionMap)
        {
            if (kvp.Value == versionValue)
            {
                return kvp.Key;
            }
        }
        
        return "unknown";
    }
}