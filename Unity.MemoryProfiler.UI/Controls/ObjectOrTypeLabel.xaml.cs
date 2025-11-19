using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// 对象或类型标签控件 - 显示对象/类型名称及其数据类型指示器
    /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectOrTypeLabel
    /// </summary>
    public partial class ObjectOrTypeLabel : UserControl
    {
        /// <summary>
        /// 数据类型枚举
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectOrTypeLabel.DataType
        /// </summary>
        public enum DataType
        {
            /// <summary>
            /// 纯C#类型（Managed）
            /// </summary>
            PureCSharpType,

            /// <summary>
            /// Unity Native类型
            /// </summary>
            NativeUnityType,

            /// <summary>
            /// 统一的Unity类型（同时有Managed和Native）
            /// </summary>
            UnifiedUnityType,

            /// <summary>
            /// Managed对象
            /// </summary>
            ManagedObject,

            /// <summary>
            /// Native对象
            /// </summary>
            NativeObject,

            /// <summary>
            /// 统一的Unity对象（同时有Managed和Native对象）
            /// </summary>
            UnifiedUnityObject,

            /// <summary>
            /// 泄漏的Shell对象（Native对象已被销毁，但Managed wrapper还存在）
            /// </summary>
            LeakedShell,
        }

        private DataType _dataType = DataType.UnifiedUnityObject;
        private string _managedTypeName = string.Empty;
        private string _nativeTypeName = string.Empty;
        private string _nativeObjectName = string.Empty;

        public ObjectOrTypeLabel()
        {
            InitializeComponent();
            UpdateDataTypeIndicator();
        }

        /// <summary>
        /// 设置数据类型
        /// </summary>
        public void SetDataType(DataType dataType)
        {
            _dataType = dataType;
            UpdateDataTypeIndicator();
        }

        /// <summary>
        /// 设置标签文本（简化版本）
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectOrTypeLabel.SetLabelData
        /// </summary>
        /// <param name="managedTypeName">Managed类型名</param>
        /// <param name="nativeTypeName">Native类型名</param>
        /// <param name="nativeObjectName">Native对象名</param>
        /// <param name="dataType">数据类型</param>
        public void SetLabelData(string managedTypeName = null, string nativeTypeName = null, string nativeObjectName = null, DataType? dataType = null)
        {
            _managedTypeName = managedTypeName ?? string.Empty;
            _nativeTypeName = nativeTypeName ?? string.Empty;
            _nativeObjectName = nativeObjectName ?? string.Empty;

            if (dataType.HasValue)
            {
                SetDataType(dataType.Value);
            }

            UpdateLabelContent();
        }

        /// <summary>
        /// 设置为"无对象选择"状态
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectOrTypeLabel.SetToNoObjectSelected
        /// </summary>
        public void SetToNoObjectSelected()
        {
            _managedTypeName = string.Empty;
            _nativeTypeName = "No Object Selected";
            _nativeObjectName = string.Empty;
            DataTypeIndicator.Visibility = Visibility.Collapsed;
            TypeIconPlaceholder.Visibility = Visibility.Collapsed;
            UpdateLabelContent();
        }

        /// <summary>
        /// 更新标签内容
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectOrTypeLabel.UpdateLabelContent
        /// </summary>
        private void UpdateLabelContent()
        {
            string text = string.Empty;

            // 添加对象名称（如果有）
            if (!string.IsNullOrEmpty(_nativeObjectName))
            {
                text = $"\"{_nativeObjectName}\" ";
            }

            // 添加Managed类型名
            if (!string.IsNullOrEmpty(_managedTypeName))
            {
                text += _managedTypeName;
            }

            // 添加Native类型名（如果与Managed类型名不同）
            if (!string.IsNullOrEmpty(_nativeTypeName) && _nativeTypeName != _managedTypeName)
            {
                var separator = (!string.IsNullOrEmpty(_managedTypeName) && !string.IsNullOrEmpty(_nativeTypeName)) ? " : " : string.Empty;
                text += separator + _nativeTypeName;
            }

            // 如果没有任何文本，显示Native类型名
            if (string.IsNullOrEmpty(text) && !string.IsNullOrEmpty(_nativeTypeName))
            {
                text = _nativeTypeName;
            }

            LabelText.Text = text;
        }

        /// <summary>
        /// 更新数据类型指示器的颜色
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectOrTypeLabel.Type (setter)
        /// </summary>
        private void UpdateDataTypeIndicator()
        {
            Color indicatorColor;
            string tooltip;

            switch (_dataType)
            {
                case DataType.ManagedObject:
                case DataType.PureCSharpType:
                    // Managed - 蓝色
                    indicatorColor = Color.FromRgb(0, 122, 204);
                    tooltip = "Managed (C#)";
                    break;

                case DataType.NativeUnityType:
                case DataType.NativeObject:
                    // Native - 紫色
                    indicatorColor = Color.FromRgb(156, 39, 176);
                    tooltip = "Native (C++)";
                    break;

                case DataType.UnifiedUnityType:
                case DataType.UnifiedUnityObject:
                    // Unified - 绿色
                    indicatorColor = Color.FromRgb(76, 175, 80);
                    tooltip = "Unity Object (Native + Managed)";
                    break;

                case DataType.LeakedShell:
                    // Leaked Shell - 红色
                    indicatorColor = Color.FromRgb(244, 67, 54);
                    tooltip = "Leaked Shell (Native destroyed, Managed wrapper remains)";
                    break;

                default:
                    // 默认 - 灰色
                    indicatorColor = Color.FromRgb(136, 136, 136);
                    tooltip = "Unknown";
                    break;
            }

            DataTypeIndicator.Fill = new SolidColorBrush(indicatorColor);
            DataTypeIndicator.ToolTip = tooltip;
            DataTypeIndicator.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 获取完整标题文本
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectOrTypeLabel.GetTitle
        /// </summary>
        public string GetTitle()
        {
            string text = string.Empty;

            if (!string.IsNullOrEmpty(_nativeObjectName))
            {
                text = $"\"{_nativeObjectName}\" ";
            }

            if (!string.IsNullOrEmpty(_managedTypeName))
            {
                text += _managedTypeName;
            }

            if (_nativeTypeName != _managedTypeName)
            {
                text += (string.IsNullOrEmpty(_managedTypeName) || string.IsNullOrEmpty(_nativeTypeName) ? string.Empty : " : ") + _nativeTypeName;
            }

            return text;
        }
    }
}

