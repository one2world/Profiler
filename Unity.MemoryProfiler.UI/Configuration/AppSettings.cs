using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Unity.MemoryProfiler.UI.Configuration
{
    /// <summary>
    /// 应用程序配置
    /// </summary>
    public class AppSettings
    {
        private static AppSettings? _instance;
        private static readonly string _configFilePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "appsettings.json"
        );

        /// <summary>
        /// 快照文件夹路径（默认相对路径）
        /// </summary>
        [JsonPropertyName("snapshotDirectory")]
        public string SnapshotDirectory { get; set; } = @"..\..\..\..\MemoryCaptures";

        /// <summary>
        /// 主题（保留用于未来扩展）
        /// </summary>
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "VS2019Dark";

        /// <summary>
        /// 获取单例实例
        /// </summary>
        public static AppSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Load();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 获取快照目录的完整路径
        /// </summary>
        public string GetSnapshotDirectoryFullPath()
        {
            if (Path.IsPathRooted(SnapshotDirectory))
            {
                // 绝对路径
                return SnapshotDirectory;
            }
            else
            {
                // 相对路径：相对于应用程序目录
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var fullPath = Path.GetFullPath(Path.Combine(baseDir, SnapshotDirectory));
                return fullPath;
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private static AppSettings Load()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        WriteIndented = true
                    });
                    
                    if (settings != null)
                    {
                        Console.WriteLine($"[AppSettings] 配置加载成功: {_configFilePath}");
                        Console.WriteLine($"[AppSettings] 快照目录: {settings.SnapshotDirectory}");
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppSettings] 加载配置失败: {ex.Message}");
            }

            // 创建默认配置
            var defaultSettings = new AppSettings();
            defaultSettings.Save();
            Console.WriteLine($"[AppSettings] 使用默认配置并已保存到: {_configFilePath}");
            return defaultSettings;
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public void Save()
        {
            try
            {
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_configFilePath, json);
                Console.WriteLine($"[AppSettings] 配置已保存: {_configFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AppSettings] 保存配置失败: {ex.Message}");
            }
        }
    }
}

