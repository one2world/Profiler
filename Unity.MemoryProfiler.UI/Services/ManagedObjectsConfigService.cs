using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// Managed Objects 配置服务
    /// 从 appsettings.json 读取源码目录列表
    /// </summary>
    public static class ManagedObjectsConfigService
    {
        private static List<string>? _cachedSourceDirectories;
        private static DateTime _lastLoadTime = DateTime.MinValue;

        /// <summary>
        /// 获取源码目录列表
        /// </summary>
        /// <param name="key">配置键名，默认为 "SourceDirectories"</param>
        public static List<string> GetSourceDirectories(string key = "SourceDirectories")
        {
            var configPath = GetConfigFilePath();
            
            // 检查文件是否被修改（简化缓存，只缓存默认 key）
            if (key == "SourceDirectories" && _cachedSourceDirectories != null && File.Exists(configPath))
            {
                var lastWriteTime = File.GetLastWriteTime(configPath);
                if (lastWriteTime <= _lastLoadTime)
                {
                    return _cachedSourceDirectories;
                }
            }

            var result = new List<string>();

            if (File.Exists(configPath))
            {
                try
                {
                    var jsonString = File.ReadAllText(configPath);
                    
                    // 移除 JSON 注释（简单处理）
                    var lines = jsonString.Split('\n');
                    var cleanedLines = new List<string>();
                    foreach (var line in lines)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.StartsWith("//"))
                            continue;
                        cleanedLines.Add(line);
                    }
                    jsonString = string.Join("\n", cleanedLines);

                    using var doc = JsonDocument.Parse(jsonString);
                    var root = doc.RootElement;
                    
                    if (root.TryGetProperty("ManagedObjects", out var managedObjects))
                    {
                        if (managedObjects.TryGetProperty(key, out var directories))
                        {
                            foreach (var dir in directories.EnumerateArray())
                            {
                                var path = dir.GetString();
                                if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
                                {
                                    result.Add(path);
                                }
                            }
                        }
                    }

                    if (key == "SourceDirectories")
                    {
                        _cachedSourceDirectories = result;
                        _lastLoadTime = DateTime.Now;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ManagedObjectsConfig] Error reading config: {ex.Message}");
                }
            }

            return result;
        }

        /// <summary>
        /// 重新加载配置
        /// </summary>
        public static void ReloadConfig()
        {
            _cachedSourceDirectories = null;
            _lastLoadTime = DateTime.MinValue;
        }

        /// <summary>
        /// 获取配置文件路径
        /// </summary>
        private static string GetConfigFilePath()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(appDir, "appsettings.json");
        }
    }
}
