using System.IO;

namespace UnityPerfProfilerWPF.Utils;

public static class UPRContext
{
    // Network Configuration
    public static string PlayerIp { get; set; } = "127.0.0.1";
    public static int PlayerPort { get; set; } = 55000;
    public static string PlayerUnityVersion { get; set; } = "2021.3.x";
    public static string SessionId { get; set; } = "";
    public static string PackageName { get; set; } = "";
    public static string DeviceId { get; set; } = "";
    
    // Server Configuration
    public static string ServerHost { get; set; } = "";
    public static int ServerPort { get; set; } = 8666;
    public static int FileServerPort { get; set; } = 8777;
    public static int ServerKcpPort { get; set; } = 4000;
    
    // Protocol Settings
    public static bool UseKcp { get; set; } = false;
    public static uint MessageThreshold { get; set; } = 10000;
    public static int SleepIntervalMilliseconds { get; set; } = 3;
    public static int TotalEnqueueMessages { get; set; } = 3000;
    
    // Feature Flags
    public static bool EnableScreenshot { get; set; } = true;
    public static bool EnableDeepMono { get; set; } = false;
    public static bool EnableGCCallStack { get; set; } = false;
    public static bool EnableAutoObject { get; set; } = false;
    public static bool Debug_MessageBusyMode { get; set; } = false;
    
    // Timing Configuration
    public static int ScreenshotFrequency { get; set; } = 4;
    public static int ObjectSnapshotFrequency { get; set; } = 5;
    
    // State Tracking
    public static bool ReceivedFirstFrame { get; set; } = false;
    public static ulong[] CapturedThreadIds { get; set; } = Array.Empty<ulong>();
    public static int RetryCountToGetThreadId { get; set; } = 0;
    
    // Platform Detection
    public static bool IsWebGL { get; set; } = false;
    public static bool UseADBLink { get; set; } = false;
    public static bool UseIOSLink { get; set; } = false;
    public static bool UseHDCLink { get; set; } = false;
    public static bool IsLocalPCMode { get; set; } = false;
    
    // Paths and Temp
    public static string TempPath { get; set; } = Path.GetTempPath();
    public static string PlatformDir { get; set; } = Path.Combine(Directory.GetCurrentDirectory(), "platform-tools/");
    
    // Performance Monitoring
    public static bool EnableFPS { get; set; } = true;
    public static int FPSFrequency { get; set; } = 1;
    public static bool EnableNetwork { get; set; } = true;
    public static bool GpuProfileEnabled { get; set; } = false;
    
    public static void Init()
    {
        // Initialize default values
        InitTempPath();
    }
    
    private static void InitTempPath()
    {
        var possiblePaths = new[]
        {
            TempPath,
            Path.GetTempPath(),
            Directory.GetCurrentDirectory()
        };
        
        foreach (var path in possiblePaths)
        {
            if (CheckWritePermission(path))
            {
                TempPath = path;
                return;
            }
        }
    }
    
    public static bool CheckWritePermission(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return false;
        }
        
        try
        {
            var testFile = Path.Combine(path, Path.GetRandomFileName());
            File.WriteAllText(testFile, "test");
            var content = File.ReadAllText(testFile);
            File.Delete(testFile);
            return content == "test";
        }
        catch
        {
            return false;
        }
    }
    
    public static bool CheckIfCaptureMemorySupported()
    {
        try
        {
            if (PlayerUnityVersion.Contains("Tuanjie"))
            {
                return true;
            }
            
            var versionParts = PlayerUnityVersion.Split('.');
            if (versionParts.Length > 1 && int.TryParse(versionParts[0], out var majorVersion))
            {
                if (majorVersion > 2018)
                {
                    return true;
                }
                
                if (majorVersion == 2018 && versionParts.Length > 1 && 
                    int.TryParse(versionParts[1], out var minorVersion) && minorVersion >= 3)
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }
        
        return false;
    }
    
    public static string GetEngine()
    {
        return PlayerUnityVersion.Contains("Tuanjie") ? "Tuanjie" : "Unity";
    }
}
