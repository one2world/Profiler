using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// UI状态管理器 - 保存和恢复UI状态
    /// 参考: Unity使用EditorPrefs进行状态持久化
    /// </summary>
    public static class UIStateManager
    {
        private static readonly string SettingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "UnityMemoryProfilerWPF",
            "ui_state.json"
        );

        private static Dictionary<string, object> _cache = new Dictionary<string, object>();
        private static bool _isLoaded = false;

        /// <summary>
        /// 加载所有设置
        /// </summary>
        private static void Load()
        {
            if (_isLoaded)
                return;

            _isLoaded = true;

            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    _cache = JsonSerializer.Deserialize<Dictionary<string, object>>(json) 
                        ?? new Dictionary<string, object>();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load UI state: {ex.Message}");
                _cache = new Dictionary<string, object>();
            }
        }

        /// <summary>
        /// 保存所有设置
        /// </summary>
        private static void Save()
        {
            try
            {
                var directory = Path.GetDirectoryName(SettingsFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save UI state: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取bool值
        /// </summary>
        public static bool GetBool(string key, bool defaultValue = false)
        {
            Load();
            
            if (_cache.TryGetValue(key, out var value))
            {
                if (value is bool boolValue)
                    return boolValue;
                
                // 尝试从JsonElement转换
                if (value is JsonElement element && (element.ValueKind == JsonValueKind.True || element.ValueKind == JsonValueKind.False))
                    return element.GetBoolean();
            }
            
            return defaultValue;
        }

        /// <summary>
        /// 设置bool值
        /// </summary>
        public static void SetBool(string key, bool value)
        {
            Load();
            _cache[key] = value;
            Save();
        }

        /// <summary>
        /// 获取double值
        /// </summary>
        public static double GetDouble(string key, double defaultValue = 0.0)
        {
            Load();
            
            if (_cache.TryGetValue(key, out var value))
            {
                if (value is double doubleValue)
                    return doubleValue;
                
                if (value is int intValue)
                    return intValue;
                
                // 尝试从JsonElement转换
                if (value is JsonElement element && element.ValueKind == JsonValueKind.Number)
                    return element.GetDouble();
            }
            
            return defaultValue;
        }

        /// <summary>
        /// 设置double值
        /// </summary>
        public static void SetDouble(string key, double value)
        {
            Load();
            _cache[key] = value;
            Save();
        }

        /// <summary>
        /// 获取string值
        /// </summary>
        public static string GetString(string key, string defaultValue = "")
        {
            Load();
            
            if (_cache.TryGetValue(key, out var value))
            {
                if (value is string stringValue)
                    return stringValue;
                
                // 尝试从JsonElement转换
                if (value is JsonElement element && element.ValueKind == JsonValueKind.String)
                    return element.GetString() ?? defaultValue;
            }
            
            return defaultValue;
        }

        /// <summary>
        /// 设置string值
        /// </summary>
        public static void SetString(string key, string value)
        {
            Load();
            _cache[key] = value;
            Save();
        }

        /// <summary>
        /// 删除指定key
        /// </summary>
        public static void DeleteKey(string key)
        {
            Load();
            if (_cache.ContainsKey(key))
            {
                _cache.Remove(key);
                Save();
            }
        }

        /// <summary>
        /// 检查key是否存在
        /// </summary>
        public static bool HasKey(string key)
        {
            Load();
            return _cache.ContainsKey(key);
        }

        /// <summary>
        /// 清空所有设置
        /// </summary>
        public static void Clear()
        {
            _cache.Clear();
            Save();
        }
    }
}

