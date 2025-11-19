using System;
using System.IO;
using System.Globalization;

namespace UnityEditor
{
    public enum ScriptingImplementation
    {
        Mono2x = 0,
        IL2CPP = 1,
        WinRTDotNET = 2
    }

    public static class EditorUtility
    {
        public static string SaveFilePanel(string title, string directory, string defaultName, string extension)
        {
            var safeDirectory = string.IsNullOrWhiteSpace(directory) ? Directory.GetCurrentDirectory() : directory;
            var fileName = string.IsNullOrWhiteSpace(defaultName) ? "export" : defaultName;
            var ext = string.IsNullOrWhiteSpace(extension) ? string.Empty : "." + extension.TrimStart('.');
            return Path.GetFullPath(Path.Combine(safeDirectory, fileName + ext));
        }

        public static void RevealInFinder(string path)
        {
            // no-op in compatibility layer
        }

        public static string FormatBytes(long bytes)
        {
            const long scale = 1024;
            if (bytes < scale)
                return string.Format(CultureInfo.InvariantCulture, "{0} B", bytes);

            var units = new[] { "KB", "MB", "GB", "TB", "PB", "EB" };
            double value = bytes;
            int unitIndex = -1;
            do
            {
                value /= scale;
                unitIndex++;
            }
            while (value >= scale && unitIndex < units.Length - 1);

            return string.Format(CultureInfo.InvariantCulture, "{0:F2} {1}", value, units[unitIndex]);
        }
    }

    public class EditorWindow
    {
        public virtual void Focus() { }

        public static EditorWindow GetWindow(Type type)
        {
            return (EditorWindow)Activator.CreateInstance(type) ?? new EditorWindow();
        }

        public static T GetWindow<T>() where T : EditorWindow, new()
        {
            return new T();
        }
    }

    public class SearchableEditorWindow : EditorWindow
    {
        public enum SearchMode
        {
            All,
            Filtered
        }
    }

    // ============================================================================
    // Unity 官方 API 等效实现
    // 
    // 官方命名空间: UnityEditor
    // 官方类型: EditorGUIUtility
    // 官方包版本: UnityEditor (Unity Engine 内置)
    // 
    // 实现说明:
    // - Unity 的 EditorGUIUtility 提供 Editor GUI 相关的工具方法
    // - PingObject 用于在 Unity Editor 中高亮显示对象
    // - isProSkin 用于判断当前是否使用 Pro 皮肤（影响颜色显示）
    // - 本实现提供空实现，因为 .NET 环境下不需要 Unity Editor 的 GUI 功能
    // 
    // 与官方的差异:
    // - PingObject 为空实现（.NET 环境无 Unity Editor GUI）
    // - isProSkin 固定返回 false（不影响解析逻辑，仅影响调试输出的颜色）
    // - 这些方法不参与核心解析逻辑，因此不影响功能正确性
    // ============================================================================
    public static class EditorGUIUtility
    {
        /// <summary>
        /// 在 Unity Editor 中高亮显示对象（.NET 实现中为空操作）
        /// </summary>
        public static void PingObject(object obj) { }
        
        /// <summary>
        /// 在 Unity Editor 中高亮显示对象（.NET 实现中为空操作）
        /// </summary>
        public static void PingObject(int instanceId) { }
        
        /// <summary>
        /// 判断是否使用 Pro 皮肤（.NET 实现中固定返回 false）
        /// </summary>
        public static bool isProSkin => false;
    }

    public static class Selection
    {
        static int[] s_InstanceIds = Array.Empty<int>();
        static int s_ActiveInstanceId;
        static UnityEngine.Object? s_ActiveObject;

        public static int[] instanceIDs
        {
            get => s_InstanceIds;
            set => s_InstanceIds = value ?? Array.Empty<int>();
        }

        public static int activeInstanceID
        {
            get => s_ActiveInstanceId;
            set => s_ActiveInstanceId = value;
        }

        public static UnityEngine.Object? activeObject
        {
            get => s_ActiveObject;
            set => s_ActiveObject = value;
        }
    }

    public static class PlayerSettings
    {
        public static string productName { get; set; } = "Application";
    }

    public class Editor
    {
        public static Editor CreateEditor(UnityEngine.Object obj) => new Editor();
    }
}

