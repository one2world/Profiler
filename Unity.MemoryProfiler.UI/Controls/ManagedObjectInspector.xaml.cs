using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// Managed对象检查器 - 显示对象的字段层级结构
    /// 参考: Unity.MemoryProfiler.Editor.UI.ManagedObjectInspector
    /// </summary>
    public partial class ManagedObjectInspector : UserControl
    {
        private List<ManagedFieldInfo> _rootFields = new List<ManagedFieldInfo>();

        public ManagedObjectInspector()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 设置要检查的Managed对象
        /// 参考: Unity.MemoryProfiler.Editor.UI.ManagedObjectInspector.SetupManagedObject
        /// </summary>
        /// <param name="fields">对象的字段列表</param>
        public void SetupManagedObject(List<ManagedFieldInfo> fields)
        {
            Clear();

            if (fields == null || fields.Count == 0)
            {
                ShowNoDataMessage();
                return;
            }

            _rootFields = fields;

            FieldsTreeList.ItemsSource = _rootFields;

            // DevExpress TreeListControl 需要显式刷新数据
            FieldsTreeList.RefreshData();

            NoDataMessage.Visibility = Visibility.Collapsed;
            FieldsTreeList.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 清空检查器内容
        /// 参考: Unity.MemoryProfiler.Editor.UI.ManagedObjectInspector.Clear
        /// </summary>
        public void Clear()
        {
            _rootFields.Clear();
            FieldsTreeList.ItemsSource = null;
            ShowNoDataMessage();
        }

        /// <summary>
        /// 显示无数据提示
        /// </summary>
        private void ShowNoDataMessage()
        {
            NoDataMessage.Visibility = Visibility.Visible;
            FieldsTreeList.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 展开所有节点（DevExpress API）
        /// </summary>
        public void ExpandAll()
        {
            var view = FieldsTreeList.View as DevExpress.Xpf.Grid.TreeListView;
            view?.ExpandAllNodes();
        }

        /// <summary>
        /// 折叠所有节点（DevExpress API）
        /// </summary>
        public void CollapseAll()
        {
            var view = FieldsTreeList.View as DevExpress.Xpf.Grid.TreeListView;
            view?.CollapseAllNodes();
        }

        #region 辅助方法 (用于从CachedSnapshot构建字段)

        /// <summary>
        /// 从简单数据创建字段列表（用于测试和演示）
        /// 未来可以扩展为从CachedSnapshot读取真实数据
        /// </summary>
        public static List<ManagedFieldInfo> CreateSampleFields()
        {
            var fields = new List<ManagedFieldInfo>();

            // 示例：基本类型字段
            fields.Add(ManagedFieldInfo.CreateSimpleField(
                "m_Name", 
                "\"MyObject\"", 
                "System.String", 
                false, 
                "16 B"));

            fields.Add(ManagedFieldInfo.CreateSimpleField(
                "m_InstanceID", 
                "12345", 
                "System.Int32", 
                false, 
                "4 B"));

            fields.Add(ManagedFieldInfo.CreateSimpleField(
                "m_IsActive", 
                "true", 
                "System.Boolean", 
                false, 
                "1 B"));

            // 示例：复杂类型字段（可展开）
            var complexField = ManagedFieldInfo.CreateComplexField(
                "m_Transform",
                "(UnityEngine.Transform)",
                "UnityEngine.Transform",
                100, // managedTypeIndex
                0x12345678, // pointer
                false,
                "48 B");

            // 添加子字段
            complexField.AddChild(ManagedFieldInfo.CreateSimpleField(
                "m_LocalPosition",
                "(0.0, 1.0, 0.0)",
                "UnityEngine.Vector3",
                false,
                "12 B"));

            complexField.AddChild(ManagedFieldInfo.CreateSimpleField(
                "m_LocalRotation",
                "(0.0, 0.0, 0.0, 1.0)",
                "UnityEngine.Quaternion",
                false,
                "16 B"));

            fields.Add(complexField);

            // 示例：静态字段
            fields.Add(ManagedFieldInfo.CreateSimpleField(
                "s_GlobalCounter",
                "999",
                "System.Int32",
                true, // isStatic
                "4 B"));

            return fields;
        }

        /// <summary>
        /// 创建一个递归引用示例（用于演示循环引用检测）
        /// </summary>
        public static List<ManagedFieldInfo> CreateRecursiveSample()
        {
            var fields = new List<ManagedFieldInfo>();

            var parentField = ManagedFieldInfo.CreateComplexField(
                "m_Parent",
                "(MyClass)",
                "MyNamespace.MyClass",
                200,
                0xAABBCCDD,
                false,
                "32 B");

            // 添加一个循环引用标记
            parentField.AddChild(ManagedFieldInfo.CreateRecursiveField(
                "m_Child",
                "MyNamespace.MyClass",
                1));

            fields.Add(parentField);

            return fields;
        }

        #endregion
    }
}

