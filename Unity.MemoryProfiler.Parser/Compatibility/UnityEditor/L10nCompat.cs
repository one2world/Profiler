// ============================================================================
// Unity 官方 API 等效实现
// 
// 官方命名空间: UnityEditor
// 官方类型: L10n
// 官方包版本: UnityEditor (Unity Engine 内置)
// 
// 实现说明:
// - Unity 的 L10n.Tr() 是本地化翻译接口，用于多语言支持
// - 在 Unity Editor 中，L10n.Tr() 会根据当前语言设置返回翻译后的字符串
// - 本实现直接返回原始字符串，因为 .NET 环境下不需要 Unity Editor 的本地化功能
// - 这不影响解析逻辑，因为这些字符串仅用于调试输出和日志，不参与数据解析
// 
// 与官方的差异:
// - 移除了多语言翻译功能（.NET 环境不支持 Unity 的本地化系统）
// - 保持了方法签名一致，确保调用方代码无需修改
// - 核心解析逻辑不依赖翻译结果，因此不影响功能正确性
// ============================================================================

namespace UnityEditor
{
    /// <summary>
    /// Unity Editor 本地化工具类的 .NET 等效实现
    /// </summary>
    public static class L10n
    {
        /// <summary>
        /// 翻译字符串（.NET 实现中直接返回原始字符串）
        /// </summary>
        /// <param name="str">要翻译的字符串</param>
        /// <returns>原始字符串（未翻译）</returns>
        public static string Tr(string str) => str;
    }
}

