using System;
using System.Windows;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.ViewModels.SelectionDetails;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// SelectionDetailsPanel 适配器
    /// 提供向后兼容的 API，将旧的 API 调用转换为 ViewModel 操作
    /// </summary>
    public class SelectionDetailsPanelAdapter
    {
        private readonly SelectionDetailsViewModel _viewModel;
        private readonly SelectionDetailsPanel _panel;

        // 分组名称常量（向后兼容）
        public const string GroupNameBasic = "Basic";
        public const string GroupNameMemory = "Memory";
        public const string GroupNameDescription = "Description";
        public const string GroupNameMetaData = "MetaData";
        public const string GroupNameHelp = "Help";
        public const string GroupNameAdvanced = "Advanced";
        public const string GroupNameDebug = "Debug";
        public const string GroupNameCallStacks = "Call Stack Info";
        public const string GroupNameManagedFields = "Managed Fields";
        public const string GroupNameReferences = "References";

        public SelectionDetailsPanelAdapter(SelectionDetailsPanel panel)
        {
            _panel = panel ?? throw new ArgumentNullException(nameof(panel));
            _viewModel = panel.ViewModel;
        }

        #region 向后兼容 API

        /// <summary>
        /// 清空所有分组
        /// </summary>
        public void ClearAllGroups()
        {
            _viewModel.Clear();
        }

        /// <summary>
        /// 清空指定分组
        /// </summary>
        public void ClearGroup(string groupName)
        {
            GetSectionViewModel(groupName)?.Clear();
        }

        /// <summary>
        /// 设置标题 - 字符串版本
        /// </summary>
        public void SetItemName(string name)
        {
            _viewModel.Title = name ?? "Unknown";
            _viewModel.ShowDetails();
        }

        /// <summary>
        /// 设置标题 - UnifiedType版本
        /// </summary>
        internal void SetItemName(UnifiedType type)
        {
            string displayName;
            if (type.HasManagedType && type.HasNativeType)
                displayName = $"{type.ManagedTypeName} (Unity Type)";
            else if (type.HasManagedType)
                displayName = type.ManagedTypeName;
            else if (type.HasNativeType)
                displayName = type.NativeTypeName;
            else
                displayName = "Unknown Type";

            SetItemName(displayName);
        }

        /// <summary>
        /// 设置标题 - ObjectData + UnifiedType版本
        /// </summary>
        internal void SetItemName(ObjectData objectData, UnifiedType type)
        {
            if (!objectData.IsValid)
            {
                SetItemName("Unknown Object");
                return;
            }

            // TODO: 需要访问 CachedSnapshot 来生成类型名
            // 暂时使用简化版本
            string displayName = "Object";
            SetItemName(displayName);
        }

        /// <summary>
        /// 设置标题 - UnifiedUnityObjectInfo版本
        /// </summary>
        internal void SetItemName(UnifiedUnityObjectInfo unityObjectInfo)
        {
            string displayName;
            if (unityObjectInfo.HasNativeSide && !string.IsNullOrEmpty(unityObjectInfo.NativeObjectName))
                displayName = $"{unityObjectInfo.NativeObjectName} ({unityObjectInfo.NativeTypeName})";
            else if (unityObjectInfo.HasNativeSide)
                displayName = unityObjectInfo.NativeTypeName ?? "Unknown Unity Object";
            else if (unityObjectInfo.HasManagedSide)
                displayName = $"{unityObjectInfo.ManagedTypeName} (Managed Shell)";
            else
                displayName = "Unknown Unity Object";

            SetItemName(displayName);
        }

        /// <summary>
        /// 设置标题 - SourceIndex版本
        /// </summary>
        internal void SetItemName(SourceIndex sourceIndex)
        {
            // TODO: 需要访问 CachedSnapshot 来生成名称
            // 暂时使用简化版本
            string displayName = sourceIndex.Id switch
            {
                SourceIndex.SourceId.NativeAllocation => "Native Allocation",
                SourceIndex.SourceId.GfxResource => "Graphics Resource",
                SourceIndex.SourceId.NativeRootReference => "Native Root Reference",
                _ => "Unknown"
            };

            SetItemName(displayName);
        }

        /// <summary>
        /// 设置描述文本
        /// </summary>
        public void SetDescription(string description)
        {
            _viewModel.Description.Text = description ?? string.Empty;
        }

        /// <summary>
        /// 添加动态元素
        /// </summary>
        public UIElement? AddDynamicElement(
            string groupName,
            string elementName,
            string value,
            string? tooltip = null,
            DynamicElementOptions options = DynamicElementOptions.None,
            Action? onInteraction = null)
        {
            var section = GetSectionViewModel(groupName);
            if (section == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Adapter] Warning: Section '{groupName}' not found.");
                return null;
            }

            // 根据选项处理不同类型的元素
            if (options.HasFlag(DynamicElementOptions.Button))
            {
                // Button 暂时不支持，忽略
                System.Diagnostics.Debug.WriteLine($"[Adapter] Warning: Button not supported in MVVM mode: {elementName}");
                return null;
            }
            else if (options.HasFlag(DynamicElementOptions.Toggle))
            {
                // Toggle 暂时不支持，忽略
                System.Diagnostics.Debug.WriteLine($"[Adapter] Warning: Toggle not supported in MVVM mode: {elementName}");
                return null;
            }
            else if (options.HasFlag(DynamicElementOptions.SubFoldout))
            {
                // SubFoldout 暂时不支持，忽略
                System.Diagnostics.Debug.WriteLine($"[Adapter] Warning: SubFoldout not supported in MVVM mode: {elementName}");
                return null;
            }
            else
            {
                // 默认：添加为属性项
                AddPropertyToSection(groupName, elementName, value, tooltip);
            }

            return null;
        }

        /// <summary>
        /// 添加 InfoBox
        /// </summary>
        public void AddInfoBox(string groupName, InfoBox infoBox)
        {
            // InfoBox 暂时转换为普通属性
            var message = infoBox.Message ?? string.Empty;
            var label = infoBox.Level switch
            {
                InfoBox.IssueLevel.Info => "ℹ️ Info",
                InfoBox.IssueLevel.Warning => "⚠️ Warning",
                InfoBox.IssueLevel.Error => "❌ Error",
                _ => "Info"
            };

            AddPropertyToSection(groupName, label, message);
        }

        /// <summary>
        /// 设置 Managed 对象检查器
        /// </summary>
        public void SetupManagedObjectInspector(System.Collections.Generic.List<ManagedFieldInfo> fields)
        {
            _panel.SetupManagedObjectInspector(fields);
        }

        /// <summary>
        /// 设置引用浏览器
        /// </summary>
        internal void SetupReferences(SourceIndex source)
        {
            _panel.SetupReferences(source);
        }

        #endregion

        #region 辅助方法

        private SectionViewModel? GetSectionViewModel(string groupName)
        {
            return groupName switch
            {
                GroupNameBasic => _viewModel.BasicInfo,
                GroupNameMemory => _viewModel.MemoryInfo,
                GroupNameDescription => _viewModel.Description,
                GroupNameMetaData => _viewModel.MetaData,
                GroupNameHelp => _viewModel.Help,
                GroupNameAdvanced => _viewModel.AdvancedInfo,
                GroupNameCallStacks => _viewModel.CallStacks,
                _ => null
            };
        }

        private void AddPropertyToSection(string groupName, string label, string value, string? tooltip = null)
        {
            var section = GetSectionViewModel(groupName);
            if (section == null)
                return;

            // 根据 Section 类型添加属性
            switch (section)
            {
                case BasicInfoViewModel basicInfo:
                    basicInfo.AddProperty(label, value, tooltip);
                    break;
                case MemoryInfoViewModel memoryInfo:
                    memoryInfo.AddProperty(label, value, tooltip);
                    break;
                case MetaDataViewModel metaData:
                    metaData.AddProperty(label, value, tooltip);
                    break;
                case HelpViewModel help:
                    help.AddProperty(label, value, tooltip);
                    break;
                case AdvancedInfoViewModel advancedInfo:
                    advancedInfo.AddProperty(label, value, tooltip);
                    break;
                case DescriptionViewModel description:
                    // Description 是文本，不是属性列表
                    description.Text = value;
                    break;
                case CallStacksViewModel callStacks:
                    // CallStacks 需要特殊处理
                    HandleCallStackProperty(callStacks, label, value);
                    break;
            }
        }

        private void HandleCallStackProperty(CallStacksViewModel callStacks, string label, string value)
        {
            // 根据 label 设置不同的属性
            if (label == "Allocations Count" && int.TryParse(value, out var count))
            {
                callStacks.CallStackCount = count;
            }
            // 其他 CallStack 相关的属性暂时忽略
        }

        #endregion
    }
}

