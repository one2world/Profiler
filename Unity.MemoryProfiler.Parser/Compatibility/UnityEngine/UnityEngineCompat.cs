using System;
using System.Globalization;

namespace UnityEngine
{
    // ============================================================================
    // Unity 官方 API 等效实现
    // 
    // 官方命名空间: UnityEngine
    // 官方类型: UnityException
    // 官方包版本: UnityEngine.CoreModule (Unity Engine 内置)
    // 
    // 实现说明:
    // - Unity 的 UnityException 是 Unity 特定异常的基类
    // - 本实现继承自 System.Exception，保持异常处理的一致性
    // - 提供与官方相同的构造函数重载
    // 
    // 与官方的差异:
    // - 无实质差异，仅是命名空间的映射
    // ============================================================================
    public class UnityException : Exception
    {
        public UnityException() : base() { }
        public UnityException(string message) : base(message) { }
        public UnityException(string message, Exception innerException) : base(message, innerException) { }
    }
}

namespace UnityEngine
{
    public class Object
    {
        public string name { get; set; } = string.Empty;

        public override string ToString() => name ?? base.ToString();
    }

    public class ScriptableObject : Object { }

    public class MonoBehaviour : Object { }

    public class Component : Object { }

    public class GameObject : Object { }

    public class GUIContent
    {
        public string text;
        public string tooltip;

        public GUIContent(string text)
        {
            this.text = text;
            tooltip = string.Empty;
        }

        public GUIContent(string text, string tooltip)
        {
            this.text = text;
            this.tooltip = tooltip;
        }
    }

    // ============================================================================
    // Unity 官方 API 等效实现
    // 
    // 官方命名空间: UnityEngine
    // 官方类型: Debug
    // 官方包版本: UnityEngine.CoreModule (Unity Engine 内置)
    // 
    // 实现说明:
    // - Unity 的 Debug 类提供日志和断言功能
    // - 在 Unity 中，这些方法会输出到 Unity Console 窗口
    // - 本实现使用 System.Console 和 System.Diagnostics.Debug 进行输出
    // - Assert 方法在条件为 false 时抛出异常，保持与 Unity 相同的行为
    // 
    // 与官方的差异:
    // - 输出目标从 Unity Console 改为 System.Console
    // - Assert 失败时抛出 InvalidOperationException 而非 Unity 的 AssertionException
    // - 保持了所有方法签名和行为逻辑的一致性
    // ============================================================================
    public static class Debug
    {
        public static void Log(object message) => Console.WriteLine(message);
        
        public static void LogWarning(object message) => Console.WriteLine($"[Warning] {message}");
        
        public static void LogWarningFormat(string format, params object[] args) 
            => Console.WriteLine("[Warning] " + string.Format(CultureInfo.InvariantCulture, format, args));
        
        public static void LogError(object message) => Console.Error.WriteLine(message);
        
        public static void LogErrorFormat(string format, params object[] args)
            => Console.Error.WriteLine(string.Format(CultureInfo.InvariantCulture, format, args));
        
        public static void LogException(Exception exception) => Console.Error.WriteLine(exception);
        
        /// <summary>
        /// 断言条件为真，否则抛出异常
        /// </summary>
        public static void Assert(bool condition)
        {
            if (!condition)
                throw new InvalidOperationException("Assertion failed");
        }
        
        /// <summary>
        /// 断言条件为真，否则抛出异常并附带消息
        /// </summary>
        public static void Assert(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException($"Assertion failed: {message}");
        }
        
        /// <summary>
        /// 记录断言失败消息（与 LogError 行为相同）
        /// </summary>
        public static void LogAssertion(object message) => LogError($"[Assertion] {message}");
    }

    public static class Mathf
    {
        public static int RoundToInt(float value) => (int)MathF.Round(value, MidpointRounding.AwayFromZero);
        public static int FloorToInt(float value) => (int)MathF.Floor(value);
        public static int CeilToInt(float value) => (int)MathF.Ceiling(value);
        public static float Clamp(float value, float min, float max) => MathF.Max(min, MathF.Min(max, value));
        public static int Clamp(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
    }

    [Flags]
    public enum HideFlags
    {
        None = 0,
        HideInHierarchy = 1 << 0,
        HideInInspector = 1 << 1,
        DontSaveInEditor = 1 << 2,
        NotEditable = 1 << 3,
        DontSaveInBuild = 1 << 4,
        DontUnloadUnusedAsset = 1 << 5,
        DontSave = DontSaveInEditor | DontSaveInBuild,
        HideAndDontSave = HideInHierarchy | HideInInspector | DontSaveInEditor | DontSaveInBuild | DontUnloadUnusedAsset
    }

    public enum RuntimePlatform
    {
        WindowsPlayer = 0,
        OSXPlayer = 1,
        LinuxPlayer = 2,
        WindowsEditor = 7,
        OSXEditor = 8,
        LinuxEditor = 24,
        IPhonePlayer = 9,
        Android = 11,
        WebGLPlayer = 17,
        WSAPlayerX86 = 18,
        WSAPlayerX64 = 19,
        WSAPlayerARM = 20,
        PS4 = 25,
        XboxOne = 27,
        Switch = 32,
        Stadia = 33,
        CloudRendering = 1_000_000,
    }

    public enum TextureFormat
    {
        Alpha8 = 1,
        ARGB4444 = 2,
        RGB24 = 3,
        RGBA32 = 4,
        ARGB32 = 5,
        RGB565 = 7,
        R16 = 9,
        DXT1 = 10,
        DXT5 = 12,
        RGBA4444 = 13,
        BGRA32 = 14,
        RHalf = 15,
        RGHalf = 16,
        RGBAHalf = 17,
        RFloat = 18,
        RGFloat = 19,
        RGBAFloat = 20,
        YUY2 = 21,
        BC4 = 26,
        BC5 = 27,
        BC6H = 24,
        BC7 = 25,
        ETC_RGB4 = 34,
        ETC2_RGBA8 = 45,
        ASTC_4x4 = 48,
        ASTC_12x12 = 63
    }

    public enum TextureWrapMode
    {
        Repeat,
        Clamp,
        Mirror,
        MirrorOnce
    }

    public enum FilterMode
    {
        Point = 0,
        Bilinear = 1,
        Trilinear = 2
    }

    [Flags]
    public enum RenderTextureMemoryless
    {
        None = 0,
        Color = 1,
        Depth = 2,
        MSAA = 4
    }

    public enum AudioCompressionFormat
    {
        PCM,
        Vorbis,
        ADPCM,
        MP3,
        VAG,
        HEVAG,
        XMA,
        AAC,
        GCADPCM,
        ATRAC9,
        OPUS,
    }

    public enum AudioDataLoadState
    {
        Unloaded,
        Loading,
        Loaded,
        Failed
    }

    public enum AudioClipLoadType
    {
        DecompressOnLoad,
        CompressedInMemory,
        Streaming
    }

    public enum ScriptingImplementation
    {
        Mono2x = 0,
        IL2CPP = 1,
        WinRTDotNET = 2
    }
}

namespace UnityEngine.Rendering
{
    public enum GraphicsDeviceType
    {
        Direct3D11 = 2,
        Direct3D12 = 18,
        Metal = 16,
        Vulkan = 21,
        OpenGLCore = 17,
        OpenGLES3 = 11,
        PlayStation5 = 28,
        XboxOne = 14,
        Switch = 22,
        Null = 4
    }

    public enum IndexFormat
    {
        UInt16 = 0,
        UInt32 = 1
    }

    public enum VertexAttribute
    {
        Position = 0,
        Normal = 1,
        Tangent = 2,
        Color = 3,
        TexCoord0 = 4,
        TexCoord1 = 5,
        TexCoord2 = 6,
        TexCoord3 = 7,
        TexCoord4 = 8,
        TexCoord5 = 9,
        TexCoord6 = 10,
        TexCoord7 = 11,
        BlendWeight = 12,
        BlendIndices = 13
    }

    public enum VertexAttributeFormat
    {
        Float32 = 0,
        Float16 = 1,
        UNorm8 = 2,
        SNorm8 = 3,
        UNorm16 = 4,
        SNorm16 = 5,
        UInt8 = 6,
        SInt8 = 7,
        UInt16 = 8,
        SInt16 = 9,
        UInt32 = 10,
        SInt32 = 11
    }

    public enum TextureDimension
    {
        Unknown = 0,
        Any = 1,
        Tex2D = 2,
        Tex3D = 3,
        Cube = 4,
        Tex2DArray = 5,
        CubeArray = 6
    }
}

namespace UnityEngine.Experimental.Rendering
{
    public enum GraphicsFormat
    {
        None = 0,
        R8G8B8A8_SRGB = 29,
        R8G8B8A8_UNorm = 37,
        R8_UNorm = 9,
        R16_UNorm = 56,
        R32_SFloat = 73,
        R16G16B16A16_SFloat = 90,
        R32G32B32A32_SFloat = 109,
        B8G8R8A8_SRGB = 24,
        B8G8R8A8_UNorm = 27
    }
}

