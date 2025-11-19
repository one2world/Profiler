using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Managed对象字段信息
    /// 参考: Unity.MemoryProfiler.Editor.UI.ManagedObjectInspectorItem
    /// </summary>
    public class ManagedFieldInfo
    {
        /// <summary>
        /// 字段显示名称
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 字段值
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// 类型名称
        /// </summary>
        public string TypeName { get; set; } = string.Empty;

        /// <summary>
        /// 字段大小（格式化后的字符串，如 "4 B"）
        /// </summary>
        public string Size { get; set; } = string.Empty;

        /// <summary>
        /// 备注（如 "Static", "Circular Reference"等）
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// 是否为静态字段
        /// </summary>
        public bool IsStatic { get; set; }

        /// <summary>
        /// 是否可展开（有子字段）
        /// </summary>
        public bool HasChildren => Children != null && Children.Count > 0;

        /// <summary>
        /// 子字段列表（用于嵌套对象）
        /// </summary>
        public List<ManagedFieldInfo>? Children { get; set; }

        /// <summary>
        /// 层级深度（用于UI缩进显示）
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Managed类型索引（用于引用Unity快照数据）
        /// </summary>
        public int ManagedTypeIndex { get; set; } = -1;

        /// <summary>
        /// 是否为递归引用
        /// </summary>
        public bool IsRecursive { get; set; }

        /// <summary>
        /// 是否为重复引用
        /// </summary>
        public bool IsDuplicate { get; set; }

        /// <summary>
        /// 是否正在等待处理（用于延迟加载）
        /// </summary>
        public bool IsPending { get; set; }

        /// <summary>
        /// 标识指针（用于检测循环引用）
        /// </summary>
        public ulong IdentifyingPointer { get; set; }

        /// <summary>
        /// 添加子字段
        /// </summary>
        public void AddChild(ManagedFieldInfo child)
        {
            if (Children == null)
                Children = new List<ManagedFieldInfo>();
            
            child.Depth = this.Depth + 1;
            Children.Add(child);
        }

        /// <summary>
        /// 创建一个简单字段（基本类型）
        /// </summary>
        public static ManagedFieldInfo CreateSimpleField(string name, string value, string typeName, bool isStatic = false, string size = "")
        {
            return new ManagedFieldInfo
            {
                DisplayName = name,
                Value = value,
                TypeName = typeName,
                IsStatic = isStatic,
                Size = size,
                Notes = isStatic ? "Static" : string.Empty,
                Depth = 0
            };
        }

        /// <summary>
        /// 创建一个复杂字段（对象/数组）
        /// </summary>
        public static ManagedFieldInfo CreateComplexField(string name, string value, string typeName, int managedTypeIndex, ulong pointer, bool isStatic = false, string size = "")
        {
            return new ManagedFieldInfo
            {
                DisplayName = name,
                Value = value,
                TypeName = typeName,
                ManagedTypeIndex = managedTypeIndex,
                IdentifyingPointer = pointer,
                IsStatic = isStatic,
                Size = size,
                Notes = isStatic ? "Static" : string.Empty,
                Depth = 0,
                Children = new List<ManagedFieldInfo>() // 标记为可展开
            };
        }

        /// <summary>
        /// 创建一个"继续加载"占位符
        /// </summary>
        public static ManagedFieldInfo CreatePendingField(int depth = 0)
        {
            return new ManagedFieldInfo
            {
                DisplayName = "Continue ...",
                Value = "(Click to load more)",
                TypeName = "",
                IsPending = true,
                Depth = depth
            };
        }

        /// <summary>
        /// 创建一个循环引用标记字段
        /// </summary>
        public static ManagedFieldInfo CreateRecursiveField(string name, string typeName, int depth = 0)
        {
            return new ManagedFieldInfo
            {
                DisplayName = name,
                Value = "",
                TypeName = typeName,
                IsRecursive = true,
                Notes = "Circular Reference",
                Depth = depth
            };
        }

        /// <summary>
        /// 展平树形结构为列表（用于UI显示）
        /// </summary>
        public List<ManagedFieldInfo> Flatten(bool expandAll = false)
        {
            var result = new List<ManagedFieldInfo>();
            FlattenRecursive(this, result, expandAll);
            return result;
        }

        private static void FlattenRecursive(ManagedFieldInfo field, List<ManagedFieldInfo> result, bool expandAll)
        {
            result.Add(field);

            if (expandAll && field.HasChildren && field.Children != null)
            {
                foreach (var child in field.Children)
                {
                    FlattenRecursive(child, result, expandAll);
                }
            }
        }
    }
}

