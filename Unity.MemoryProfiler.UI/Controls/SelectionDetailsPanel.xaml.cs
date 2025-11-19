using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.UIContent;
using UnityEditor;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// 选择详情面板 - 显示所选项目的详细信息
    /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel
    /// </summary>
    public partial class SelectionDetailsPanel : UserControl
    {
        // 动态分组管理
        private readonly Dictionary<string, DetailsGroup> _groups = new Dictionary<string, DetailsGroup>();
        
        // 分组名称常量 (参考Unity)
        public const string GroupNameBasic = "Basic";
        public const string GroupNameMetaData = "MetaData";
        public const string GroupNameHelp = "Help";
        public const string GroupNameAdvanced = "Advanced";
        public const string GroupNameDebug = "Debug";
        public const string GroupNameCallStacks = "Call Stack Info";
        public const string GroupNameManagedFields = "Managed Fields";

        // UI状态持久化的key前缀
        private const string StateKeyPrefix = "SelectionDetailsPanel.";

        private CachedSnapshot? m_Snapshot;
        private Services.SelectedItemDetailsBuilder? m_DetailsBuilder;

        internal Services.SelectedItemDetailsBuilder? DetailsBuilder => m_DetailsBuilder;

        public SelectionDetailsPanel()
        {
            InitializeComponent();
            InitializeGroups();
            RestoreUIState();
            
            // 订阅PathsToRootView的选择变化事件
            PathsToRootViewControl.SelectionChanged += OnReferencesSelectionChanged;
            
            // 订阅Expander的展开/折叠事件以保存状态
            BasicInfoExpander.Expanded += (s, e) => SaveExpanderState(GroupNameBasic, true);
            BasicInfoExpander.Collapsed += (s, e) => SaveExpanderState(GroupNameBasic, false);
            MemoryInfoExpander.Expanded += (s, e) => SaveExpanderState("Memory", true);
            MemoryInfoExpander.Collapsed += (s, e) => SaveExpanderState("Memory", false);
            AdvancedInfoExpander.Expanded += (s, e) => SaveExpanderState(GroupNameAdvanced, true);
            AdvancedInfoExpander.Collapsed += (s, e) => SaveExpanderState(GroupNameAdvanced, false);
            DescriptionExpander.Expanded += (s, e) => SaveExpanderState("Description", true);
            DescriptionExpander.Collapsed += (s, e) => SaveExpanderState("Description", false);
            ReferencesExpander.Expanded += (s, e) => SaveExpanderState("References", true);
            ReferencesExpander.Collapsed += (s, e) => SaveExpanderState("References", false);
            ManagedFieldsExpander.Expanded += (s, e) => SaveExpanderState(GroupNameManagedFields, true);
            ManagedFieldsExpander.Collapsed += (s, e) => SaveExpanderState(GroupNameManagedFields, false);
        }
        
        /// <summary>
        /// 初始化预定义分组
        /// </summary>
        private void InitializeGroups()
        {
            // 将现有的Expander注册为分组
            RegisterGroup(GroupNameBasic, BasicInfoExpander, BasicInfoContent);
            RegisterGroup(GroupNameAdvanced, AdvancedInfoExpander, AdvancedInfoContent);
        }
        
        /// <summary>
        /// 注册一个已存在的分组
        /// </summary>
        private void RegisterGroup(string name, Expander expander, StackPanel content)
        {
            _groups[name] = new DetailsGroup
            {
                Name = name,
                Expander = expander,
                Content = content
            };
        }

        /// <summary>
        /// 设置快照（必须在显示详情之前调用）
        /// </summary>
        internal void SetSnapshot(CachedSnapshot snapshot)
        {
            m_Snapshot = snapshot;
            
            // 初始化SelectedItemDetailsBuilder
            if (m_Snapshot != null)
            {
                m_DetailsBuilder = new Services.SelectedItemDetailsBuilder(m_Snapshot, this);
            }
            else
            {
                m_DetailsBuilder = null;
            }
        }

        /// <summary>
        /// 清空所有详情信息
        /// </summary>
        public void ClearSelection()
        {
            TitleTextBlock.Text = "No Selection";
            NoSelectionMessage.Visibility = Visibility.Visible;
            DetailsContent.Visibility = Visibility.Collapsed;
            
            BasicInfoContent.Children.Clear();
            MemoryInfoContent.Children.Clear();
            AdvancedInfoContent.Children.Clear();
            DescriptionText.Text = string.Empty;
            
            // 清空并隐藏Managed对象检查器
            HideManagedObjectInspector();
            
            // 清空并隐藏引用浏览器
            HideReferences();
        }

        /// <summary>
        /// 显示简单详情（用于分组、类别等无对象实例的节点）
        /// 等价于Unity的SimpleDetailsViewController
        /// 参考: Unity.MemoryProfiler.Editor.UI.SimpleDetailsViewController
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="description">描述文本</param>
        /// <param name="documentationUrl">文档链接（可选）</param>
        public void ShowSimpleDetails(string title, string description, string? documentationUrl = null)
        {
            // 清空所有分组
            ClearAllGroups();

            // 设置标题
            TitleTextBlock.Text = title;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;

            // 显示描述
            if (!string.IsNullOrEmpty(description))
            {
                DescriptionExpander.IsExpanded = true;
                DescriptionExpander.Visibility = Visibility.Visible;
                DescriptionText.Text = description;
            }
            else
            {
                DescriptionExpander.Visibility = Visibility.Collapsed;
            }

            // 显示文档链接（如果有）
            if (!string.IsNullOrEmpty(documentationUrl))
            {
                // 在Basic分组添加文档链接
                AddDocumentationLink(BasicInfoContent, "Documentation", documentationUrl);
                BasicInfoExpander.Visibility = Visibility.Visible;
            }
            else
            {
                BasicInfoExpander.Visibility = Visibility.Collapsed;
            }

            // 隐藏不相关的Expander
            MemoryInfoExpander.Visibility = Visibility.Collapsed;
            AdvancedInfoExpander.Visibility = Visibility.Collapsed;
            ManagedFieldsExpander.Visibility = Visibility.Collapsed;
            ReferencesExpander.Visibility = Visibility.Collapsed;

            // 隐藏Managed对象检查器
            HideManagedObjectInspector();
        }

        /// <summary>
        /// 显示ITreeNode的详情（统一入口）
        /// 参考: Unity的SelectedItemDetailsForTypesAndObjects.SetSelection
        /// </summary>
        public void ShowDetails(ITreeNode node)
        {
            if (node == null)
            {
                ClearSelection();
                return;
            }

            // Phase 1: 尝试使用SelectedItemDetailsBuilder（新逻辑）
            if (m_DetailsBuilder != null && m_Snapshot != null)
            {
                bool handledByBuilder = false;
                
                if (node is AllTrackedMemoryTreeNode allTrackedNode && allTrackedNode.Source.Valid)
                {
                    m_DetailsBuilder.SetSelection(allTrackedNode.Source);
                    handledByBuilder = true;
                }
                else if (node is UnityObjectTreeNode unityObjectNode && unityObjectNode.Source.Valid)
                {
                    m_DetailsBuilder.SetSelection(unityObjectNode.Source);
                    handledByBuilder = true;
                }
                
                if (handledByBuilder)
                    return;
            }

            // 回退到旧逻辑（如果SelectedItemDetailsBuilder未初始化或Source无效）
            if (node is AllTrackedMemoryTreeNode allTrackedNodeFallback)
            {
                ShowAllTrackedMemoryDetails(allTrackedNodeFallback);
            }
            else if (node is UnityObjectTreeNode unityObjectNodeFallback)
            {
                ShowUnityObjectDetails(unityObjectNodeFallback);
            }
            else
            {
                // 未知类型
                ClearSelection();
            }
        }

        /// <summary>
        /// 显示AllTrackedMemoryTreeNode的详情
        /// 参考: Unity的HandleUnityObjectDetails / HandleNativeAllocationDetails
        /// </summary>
        internal void ShowAllTrackedMemoryDetails(AllTrackedMemoryTreeNode node)
        {
            if (node == null)
            {
                ClearSelection();
                return;
            }

            // 设置标题
            TitleTextBlock.Text = node.Name;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;
            BasicInfoExpander.Visibility = Visibility.Visible;
            MemoryInfoExpander.Visibility = Visibility.Visible;

            // 清空现有内容
            BasicInfoContent.Children.Clear();
            MemoryInfoContent.Children.Clear();
            AdvancedInfoContent.Children.Clear();

            // 基本信息
            AddPropertyRow(BasicInfoContent, "Name", node.Name);
            
            if (node.Category != CategoryType.None)
            {
                AddPropertyRow(BasicInfoContent, "Category", node.Category.ToString());
            }
            
            if (node.IsGroupNode)
            {
                AddPropertyRow(BasicInfoContent, "Type", "Group Node");
                if (node.ChildCount > 0)
                {
                    AddPropertyRow(BasicInfoContent, "Child Count", node.ChildCount.ToString(), 
                        $"This group contains {node.ChildCount} child items");
                }
            }

            // 内存信息 - 参考Unity的显示格式
            AddPropertyRow(MemoryInfoContent, "Allocated Size", node.FormattedAllocatedSize, 
                $"{node.AllocatedSize:N0} B\n\nAllocated (or Committed) memory is memory that the OS has allocated for the application.");
            
            AddPropertyRow(MemoryInfoContent, "Resident Size", node.FormattedResidentSize,
                $"{node.ResidentSize:N0} B\n\nResident memory is memory that is currently in physical RAM.");
            
            AddPropertyRow(MemoryInfoContent, "% Impact", node.FormattedPercentage,
                $"This represents {node.Percentage:P2} of the total memory");
            
            // 显示MB值（方便对比）
            if (node.AllocatedSize > 1024 * 1024)
            {
                AddPropertyRow(MemoryInfoContent, "Allocated (MB)", $"{node.AllocatedSizeMB:F2} MB");
            }
            if (node.ResidentSize > 1024 * 1024)
            {
                AddPropertyRow(MemoryInfoContent, "Resident (MB)", $"{node.ResidentSizeMB:F2} MB");
            }
            
            // Unreliable警告 - 使用InfoBox
            if (node.Unreliable)
            {
                AddInfoBox(MemoryInfoContent, InfoBox.IssueLevel.Warning, 
                    "This is an estimated value and may not be fully accurate. " +
                    "The actual value might differ from what is shown here.");
            }

            // 高级信息
            if (node.Source.Id != default)
            {
                AddPropertyRow(AdvancedInfoContent, "Source ID", node.Source.Id.ToString(),
                    $"Internal source identifier: {node.Source.Id}");
                AddPropertyRow(AdvancedInfoContent, "Source Valid", node.Source.Valid.ToString());
                AddPropertyRow(AdvancedInfoContent, "Node ID", node.Id.ToString(),
                    "Unique node identifier in the tree");
            }

            // 添加内存使用效率指标（如果有意义）
            if (node.AllocatedSize > 0 && node.ResidentSize > 0)
            {
                var efficiencyPercent = (node.ResidentSize * 100.0) / node.AllocatedSize;
                AddPropertyRow(AdvancedInfoContent, "Resident %", $"{efficiencyPercent:F1}%",
                    $"{efficiencyPercent:F2}% of allocated memory is currently resident in RAM");
            }

            // 根据是否有高级信息决定显示
            AdvancedInfoExpander.Visibility = AdvancedInfoContent.Children.Count > 0 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            
            // 描述：根据Category动态生成（参考Unity的BreakdownDetailsViewControllerFactory）
            var description = CategoryDescriptions.GetDescription(node.Category);
            if (!string.IsNullOrEmpty(description))
            {
                DescriptionText.Text = description;
                DescriptionExpander.Visibility = Visibility.Visible;

                // 添加文档链接（如果有相关Category）
                if (node.Category != CategoryType.None && BasicInfoContent.Children.Count > 0)
                {
                    AddSeparator(BasicInfoContent);
                    AddDocumentationLink(BasicInfoContent, "Documentation",
                        TextContent.DocumentationMemoryCategories);
                }
            }
            else
            {
                DescriptionExpander.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// 显示UnityObjectTreeNode的详情
        /// 参考: Unity的HandleUnityObjectDetails
        /// </summary>
        internal void ShowUnityObjectDetails(UnityObjectTreeNode node)
        {
            if (node == null)
            {
                ClearSelection();
                return;
            }

            // 设置标题
            TitleTextBlock.Text = node.Name;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;

            // 清空现有内容
            BasicInfoContent.Children.Clear();
            MemoryInfoContent.Children.Clear();
            AdvancedInfoContent.Children.Clear();

            // 基本信息
            AddPropertyRow(BasicInfoContent, "Name", node.Name);
            
            if (node.IsGroupNode)
            {
                AddPropertyRow(BasicInfoContent, "Type", "Group Node");
                if (node.ChildCount > 0)
                {
                    AddPropertyRow(BasicInfoContent, "Child Count", node.ChildCount.ToString(),
                        $"This group contains {node.ChildCount} child items");
                }
            }
            
            if (node.InstanceId != 0)
            {
                AddPropertyRow(BasicInfoContent, "Instance ID", node.InstanceId.ToString(),
                    "Unity Internal Instance ID");
            }

            // 内存信息 - 参考Unity的层级显示格式
            var hasMultipleSizes = (node.NativeSize > 0 ? 1 : 0) + 
                                  (node.ManagedSize > 0 ? 1 : 0) + 
                                  (node.GpuSize > 0 ? 1 : 0) > 1;

            if (hasMultipleSizes)
            {
                AddPropertyRow(MemoryInfoContent, "Total Allocated", node.FormattedTotalSize,
                    $"{node.TotalSize:N0} B\n\nTotal = Native + Managed + Graphics");
            }

            if (node.NativeSize > 0)
            {
                var label = hasMultipleSizes ? "├ Native Size" : "Native Size";
                AddPropertyRow(MemoryInfoContent, label, node.FormattedNativeSize,
                    $"{node.NativeSize:N0} B\n\nNative memory used by this Unity Object");
            }

            if (node.ManagedSize > 0)
            {
                var label = node.GpuSize > 0 ? "├ Managed Size" : 
                           node.NativeSize > 0 ? "└ Managed Size" : "Managed Size";
                AddPropertyRow(MemoryInfoContent, label, node.FormattedManagedSize,
                    $"{node.ManagedSize:N0} B\n\nManaged (C#) memory used by this Unity Object");
            }

            if (node.GpuSize > 0)
            {
                var label = hasMultipleSizes ? "└ Graphics Size" : "Graphics Size";
                AddPropertyRow(MemoryInfoContent, label, node.FormattedGpuSize,
                    $"{node.GpuSize:N0} B\n\nGPU memory used by this Unity Object");
            }
            
            AddPropertyRow(MemoryInfoContent, "% Impact", node.FormattedPercentage,
                $"This represents {node.Percentage:P2} of the total memory");
            
            // 显示MB值（方便对比）
            if (node.TotalSize > 1024 * 1024)
            {
                AddPropertyRow(MemoryInfoContent, "Total (MB)", $"{node.TotalSizeMB:F2} MB");
            }

            // 高级信息
            if (node.NativeTypeIndex >= 0)
                AddPropertyRow(AdvancedInfoContent, "Native Type Index", node.NativeTypeIndex.ToString(),
                    "Index of the native type in the snapshot");
            if (node.ManagedTypeIndex >= 0)
                AddPropertyRow(AdvancedInfoContent, "Managed Type Index", node.ManagedTypeIndex.ToString(),
                    "Index of the managed type in the snapshot");
            if (node.ObjectIndex >= 0)
                AddPropertyRow(AdvancedInfoContent, "Object Index", node.ObjectIndex.ToString(),
                    "Index of the object in the snapshot");
            
            AddPropertyRow(AdvancedInfoContent, "Node ID", node.Id.ToString(),
                "Unique node identifier in the tree");

            // 添加内存使用分布百分比
            if (node.TotalSize > 0)
            {
                if (node.NativeSize > 0)
                {
                    var nativePercent = (node.NativeSize * 100.0) / node.TotalSize;
                    AddPropertyRow(AdvancedInfoContent, "Native %", $"{nativePercent:F1}%",
                        $"Native memory represents {nativePercent:F2}% of total");
                }
                if (node.ManagedSize > 0)
                {
                    var managedPercent = (node.ManagedSize * 100.0) / node.TotalSize;
                    AddPropertyRow(AdvancedInfoContent, "Managed %", $"{managedPercent:F1}%",
                        $"Managed memory represents {managedPercent:F2}% of total");
                }
                if (node.GpuSize > 0)
                {
                    var gpuPercent = (node.GpuSize * 100.0) / node.TotalSize;
                    AddPropertyRow(AdvancedInfoContent, "Graphics %", $"{gpuPercent:F1}%",
                        $"Graphics memory represents {gpuPercent:F2}% of total");
                }
            }

            // 根据是否有高级信息决定显示
            AdvancedInfoExpander.Visibility = AdvancedInfoContent.Children.Count > 0 
                ? Visibility.Visible 
                : Visibility.Collapsed;
            
            // 添加Unity Objects相关描述
            DescriptionText.Text = TextContent.UnityObjectDescription;
            DescriptionExpander.Visibility = Visibility.Visible;

            // 添加文档链接
            if (BasicInfoContent.Children.Count > 0)
            {
                AddSeparator(BasicInfoContent);
                AddDocumentationLink(BasicInfoContent, "Documentation", TextContent.DocumentationUnityObjects);
            }

            // 集成Managed对象检查器（参考Unity的HandleUnityObjectDetails line 521-524）
            // 如果有Source且有效，尝试显示Managed字段
            if (node.Source.Valid && m_Snapshot != null)
            {
                // Managed Fields 现在由 SelectedItemDetailsBuilder 和 Presenter 系统处理
                // 不需要在这里设置占位内容
            }
        }

        /// <summary>
        /// 添加属性行（标签+值）
        /// </summary>
        private void AddPropertyRow(Panel parent, string label, string value, string tooltip = null)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var labelBlock = new TextBlock
            {
                Text = label + ":",
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(labelBlock, 0);
            grid.Children.Add(labelBlock);

            var valueBlock = new TextBlock
            {
                Text = value,
                Foreground = new SolidColorBrush(Color.FromRgb(51, 51, 51)),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            Grid.SetColumn(valueBlock, 1);
            grid.Children.Add(valueBlock);

            if (!string.IsNullOrEmpty(tooltip))
            {
                grid.ToolTip = tooltip;
            }

            parent.Children.Add(grid);
        }

        /// <summary>
        /// 添加InfoBox到指定组
        /// 参考: Unity的AddInfoBox方法
        /// </summary>
        public void AddInfoBox(Panel parent, InfoBox.IssueLevel level, string message)
        {
            var infoBox = new InfoBox
            {
                Level = level,
                Message = message,
                Margin = new Thickness(0, 5, 0, 5)
            };
            parent.Children.Add(infoBox);
        }

        /// <summary>
        /// 添加InfoBox到指定分组（接受InfoBox对象）
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.AddInfoBox(string groupName, InfoBox)
        /// </summary>
        internal void AddInfoBox(string groupName, InfoBox infoBox)
        {
            var group = GetOrCreateGroup(groupName);
            if (group != null)
            {
                infoBox.Margin = new Thickness(0, 5, 0, 5);
                group.Content.Children.Add(infoBox);
                group.Show();
            }
        }

        /// <summary>
        /// 添加按钮元素
        /// 参考: Unity的AddDynamicElement with Button option
        /// </summary>
        public void AddButton(Panel parent, string label, string text, string tooltip, Action onClick)
        {
            var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            
            if (!string.IsNullOrEmpty(label))
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var labelBlock = new TextBlock
                {
                    Text = label + ":",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(labelBlock, 0);
                grid.Children.Add(labelBlock);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            var button = new Button
            {
                Content = text,
                Padding = new Thickness(10, 5, 10, 5),
                ToolTip = tooltip,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            
            if (onClick != null)
                button.Click += (s, e) => onClick();

            Grid.SetColumn(button, string.IsNullOrEmpty(label) ? 0 : 1);
            grid.Children.Add(button);

            parent.Children.Add(grid);
        }

        /// <summary>
        /// 添加切换开关元素
        /// 参考: Unity的AddDynamicElement with Toggle option
        /// </summary>
        public void AddToggle(Panel parent, string label, string text, bool isChecked, string tooltip, Action<bool> onToggle)
        {
            var grid = new Grid { Margin = new Thickness(0, 5, 0, 5) };
            
            if (!string.IsNullOrEmpty(label))
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var labelBlock = new TextBlock
                {
                    Text = label + ":",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(labelBlock, 0);
                grid.Children.Add(labelBlock);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            var checkBox = new CheckBox
            {
                Content = text,
                IsChecked = isChecked,
                ToolTip = tooltip,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (onToggle != null)
                checkBox.Checked += (s, e) => onToggle(true);
            if (onToggle != null)
                checkBox.Unchecked += (s, e) => onToggle(false);

            Grid.SetColumn(checkBox, string.IsNullOrEmpty(label) ? 0 : 1);
            grid.Children.Add(checkBox);

            parent.Children.Add(grid);
        }

        /// <summary>
        /// 添加富文本内容（支持超链接）
        /// 参考: Unity的AddDynamicElement with CustomContent/RichText
        /// </summary>
        public void AddRichText(Panel parent, string label, string richText, string tooltip = null)
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            
            if (!string.IsNullOrEmpty(label))
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var labelBlock = new TextBlock
                {
                    Text = label + ":",
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)),
                    VerticalAlignment = VerticalAlignment.Top
                };
                Grid.SetColumn(labelBlock, 0);
                grid.Children.Add(labelBlock);
            }
            else
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            }

            var richTextBlock = new RichTextBlock
            {
                Text = richText,
                VerticalAlignment = VerticalAlignment.Top
            };

            if (!string.IsNullOrEmpty(tooltip))
            {
                richTextBlock.ToolTip = tooltip;
            }

            Grid.SetColumn(richTextBlock, string.IsNullOrEmpty(label) ? 0 : 1);
            grid.Children.Add(richTextBlock);

            parent.Children.Add(grid);
        }

        /// <summary>
        /// 添加简单的富文本块（无标签）
        /// 常用于添加多行说明文字
        /// </summary>
        public void AddRichTextBlock(Panel parent, string richText)
        {
            var richTextBlock = new RichTextBlock
            {
                Text = richText,
                Margin = new Thickness(0, 5, 0, 5)
            };
            parent.Children.Add(richTextBlock);
        }

        /// <summary>
        /// 添加分隔线
        /// </summary>
        public void AddSeparator(Panel parent)
        {
            var separator = new System.Windows.Shapes.Rectangle
            {
                Height = 1,
                Fill = new SolidColorBrush(Color.FromRgb(224, 224, 224)),
                Margin = new Thickness(0, 10, 0, 10)
            };
            parent.Children.Add(separator);
        }

        /// <summary>
        /// 添加文档链接
        /// 参考: Unity的Documentation button
        /// </summary>
        public void AddDocumentationLink(Panel parent, string label, string url)
        {
            AddRichText(parent, label, $"[Open Documentation]({url})", "Click to open documentation in browser");
        }

        #region 动态元素系统 (参考Unity: SelectedItemDetailsPanel)

        /// <summary>
        /// 获取或创建指定名称的分组
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.GetOrCreateGroupFromRoot
        /// </summary>
        public DetailsGroup GetOrCreateGroup(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var existingGroup))
            {
                return existingGroup;
            }

            // 创建新的分组
            var expander = new Expander
            {
                Header = groupName,
                IsExpanded = true,
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            // 尝试应用ModernExpanderStyle（如果存在）
            var expanderStyle = TryFindResource("ModernExpanderStyle") as Style;
            if (expanderStyle != null)
                expander.Style = expanderStyle;

            var content = new StackPanel
            {
                Margin = new Thickness(15, 5, 0, 5)
            };

            expander.Content = content;

            var newGroup = new DetailsGroup
            {
                Name = groupName,
                Expander = expander,
                Content = content
            };

            _groups[groupName] = newGroup;

            // 添加到主面板（在DescriptionExpander之前插入）
            int insertIndex = DetailsContent.Children.IndexOf(DescriptionExpander);
            if (insertIndex >= 0)
            {
                DetailsContent.Children.Insert(insertIndex, expander);
            }
            else
            {
                DetailsContent.Children.Add(expander);
            }

            return newGroup;
        }

        /// <summary>
        /// 添加动态元素到指定分组
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.AddDynamicElement
        /// </summary>
        /// <param name="groupName">分组名称</param>
        /// <param name="elementName">元素名称（标签）</param>
        /// <param name="value">元素值</param>
        /// <param name="tooltip">工具提示</param>
        /// <param name="options">动态元素选项</param>
        /// <param name="onInteraction">交互回调（Button点击或Toggle切换时触发）</param>
        /// <returns>创建的元素</returns>
        public UIElement AddDynamicElement(
            string groupName,
            string elementName,
            string value,
            string? tooltip = null,
            DynamicElementOptions options = DynamicElementOptions.None,
            Action? onInteraction = null)
        {
            var group = GetOrCreateGroup(groupName);
            UIElement element = null;

            // 根据选项创建不同类型的元素
            if (options.HasFlag(DynamicElementOptions.Button))
            {
                element = CreateButtonElement(elementName, value, tooltip, onInteraction);
            }
            else if (options.HasFlag(DynamicElementOptions.Toggle))
            {
                bool isOn = options.HasFlag(DynamicElementOptions.ToggleOn);
                element = CreateToggleElement(elementName, value, isOn, tooltip, onInteraction);
            }
            else if (options.HasFlag(DynamicElementOptions.SubFoldout))
            {
                element = CreateSubFoldoutElement(elementName, tooltip);
            }
            else if (options.HasFlag(DynamicElementOptions.EnableRichText))
            {
                element = CreateRichTextElement(elementName, value, tooltip);
            }
            else
            {
                // 默认：标签+值
                element = CreateLabelValueElement(elementName, value, tooltip, options);
            }

            // 应用 PlaceFirstInGroup 选项
            if (options.HasFlag(DynamicElementOptions.PlaceFirstInGroup))
            {
                group.Content.Children.Insert(0, element);
            }
            else
            {
                group.Content.Children.Add(element);
            }

            return element;
        }

        /// <summary>
        /// 创建按钮元素
        /// </summary>
        private UIElement CreateButtonElement(string label, string buttonText, string? tooltip, Action? onInteraction)
        {
            var grid = CreateTwoColumnGrid();

            // 左侧标签
            if (!string.IsNullOrEmpty(label))
            {
                var labelTextBlock = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5)
                };
                Grid.SetColumn(labelTextBlock, 0);
                grid.Children.Add(labelTextBlock);
            }

            // 右侧按钮
            var button = new Button
            {
                Content = buttonText,
                Padding = new Thickness(10, 3, 10, 3),
                HorizontalAlignment = string.IsNullOrEmpty(label) ? HorizontalAlignment.Stretch : HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 5)
            };

            if (!string.IsNullOrEmpty(tooltip))
                button.ToolTip = tooltip;

            // 连接交互回调
            if (onInteraction != null)
                button.Click += (s, e) => onInteraction();

            Grid.SetColumn(button, string.IsNullOrEmpty(label) ? 0 : 1);
            if (string.IsNullOrEmpty(label))
                Grid.SetColumnSpan(button, 2);

            grid.Children.Add(button);
            return grid;
        }

        /// <summary>
        /// 创建Toggle元素
        /// </summary>
        private UIElement CreateToggleElement(string label, string toggleText, bool isOn, string? tooltip, Action? onInteraction)
        {
            var grid = CreateTwoColumnGrid();

            // 左侧标签
            if (!string.IsNullOrEmpty(label))
            {
                var labelTextBlock = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5)
                };
                Grid.SetColumn(labelTextBlock, 0);
                grid.Children.Add(labelTextBlock);
            }

            // 右侧CheckBox
            var checkBox = new CheckBox
            {
                Content = toggleText,
                IsChecked = isOn,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5)
            };

            if (!string.IsNullOrEmpty(tooltip))
                checkBox.ToolTip = tooltip;

            // 连接交互回调（Checked和Unchecked都触发同一个回调）
            if (onInteraction != null)
            {
                checkBox.Checked += (s, e) => onInteraction();
                checkBox.Unchecked += (s, e) => onInteraction();
            }

            Grid.SetColumn(checkBox, string.IsNullOrEmpty(label) ? 0 : 1);
            if (string.IsNullOrEmpty(label))
                Grid.SetColumnSpan(checkBox, 2);

            grid.Children.Add(checkBox);
            return grid;
        }

        /// <summary>
        /// 创建SubFoldout（子折叠面板）元素
        /// </summary>
        private UIElement CreateSubFoldoutElement(string title, string? tooltip)
        {
            var expander = new Expander
            {
                Header = title,
                IsExpanded = false,
                Margin = new Thickness(0, 5, 0, 5)
            };
            
            // 尝试应用ModernExpanderStyle（如果存在）
            var expanderStyle = TryFindResource("ModernExpanderStyle") as Style;
            if (expanderStyle != null)
                expander.Style = expanderStyle;

            if (!string.IsNullOrEmpty(tooltip))
                expander.ToolTip = tooltip;

            var content = new StackPanel
            {
                Margin = new Thickness(15, 5, 0, 5)
            };

            expander.Content = content;
            return expander;
        }

        /// <summary>
        /// 创建富文本元素
        /// </summary>
        private UIElement CreateRichTextElement(string label, string richText, string? tooltip)
        {
            var grid = CreateTwoColumnGrid();

            // 左侧标签
            if (!string.IsNullOrEmpty(label))
            {
                var labelTextBlock = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 5, 10, 5),
                    FontWeight = FontWeights.Bold
                };
                Grid.SetColumn(labelTextBlock, 0);
                grid.Children.Add(labelTextBlock);
            }

            // 右侧富文本
            var richTextBlock = new RichTextBlock
            {
                Text = richText,
                Margin = new Thickness(0, 5, 0, 5)
            };

            if (!string.IsNullOrEmpty(tooltip))
                richTextBlock.ToolTip = tooltip;

            Grid.SetColumn(richTextBlock, string.IsNullOrEmpty(label) ? 0 : 1);
            if (string.IsNullOrEmpty(label))
                Grid.SetColumnSpan(richTextBlock, 2);

            grid.Children.Add(richTextBlock);
            return grid;
        }

        /// <summary>
        /// 创建标签+值元素
        /// </summary>
        private UIElement CreateLabelValueElement(string label, string value, string? tooltip, DynamicElementOptions options)
        {
            var grid = CreateTwoColumnGrid();

            // 左侧标签
            if (!string.IsNullOrEmpty(label))
            {
                var labelTextBlock = new TextBlock
                {
                    Text = label,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(0, 5, 10, 5),
                    FontWeight = FontWeights.Bold
                };
                Grid.SetColumn(labelTextBlock, 0);
                grid.Children.Add(labelTextBlock);
            }

            // 右侧值
            var valueTextBlock = new TextBlock
            {
                Text = value,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 5, 0, 5),
                TextWrapping = TextWrapping.Wrap
            };

            // SelectableLabel选项
            if (options.HasFlag(DynamicElementOptions.SelectableLabel))
            {
                valueTextBlock.Cursor = System.Windows.Input.Cursors.IBeam;
            }

            if (!string.IsNullOrEmpty(tooltip))
                valueTextBlock.ToolTip = tooltip;

            Grid.SetColumn(valueTextBlock, string.IsNullOrEmpty(label) ? 0 : 1);
            if (string.IsNullOrEmpty(label))
                Grid.SetColumnSpan(valueTextBlock, 2);

            grid.Children.Add(valueTextBlock);
            return grid;
        }

        /// <summary>
        /// 清空指定分组的内容
        /// </summary>
        public void ClearGroup(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Clear();
            }
        }

        /// <summary>
        /// 清空所有分组的内容
        /// 参考: Unity SelectedItemDetailsPanel.Clear() Line 836-867
        /// </summary>
        public void ClearAllGroups()
        {
            foreach (var group in _groups.Values)
            {
                group.Clear();
            }
        }

        /// <summary>
        /// 隐藏指定分组
        /// </summary>
        public void HideGroup(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Hide();
            }
        }

        /// <summary>
        /// 显示指定分组
        /// </summary>
        public void ShowGroup(string groupName)
        {
            if (_groups.TryGetValue(groupName, out var group))
            {
                group.Show();
            }
        }

        /// <summary>
        /// 获取SubFoldout的内容面板
        /// </summary>
        public StackPanel? GetSubFoldoutContent(UIElement element)
        {
            if (element is Expander expander && expander.Content is StackPanel panel)
            {
                return panel;
            }
            return null;
        }

        /// <summary>
        /// 创建两列Grid布局（标签列 + 值列）
        /// </summary>
        private Grid CreateTwoColumnGrid()
        {
            var grid = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            return grid;
        }

        #endregion

        #region Managed对象检查器集成 (参考Unity: SelectedItemDetailsPanel.SetManagedObjectInspector)

        /// <summary>
        /// 设置Managed对象检查器的内容
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.SetManagedObjectInspector
        /// </summary>
        /// <param name="fields">对象的字段列表</param>
        public void SetupManagedObjectInspector(List<ManagedFieldInfo> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                HideManagedObjectInspector();
                return;
            }

            ManagedObjectInspectorControl.SetupManagedObject(fields);
            ManagedFieldsExpander.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 隐藏Managed对象检查器
        /// </summary>
        public void HideManagedObjectInspector()
        {
            ManagedObjectInspectorControl.Clear();
            ManagedFieldsExpander.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 隐藏内存信息面板
        /// 用于 Summary 等不需要显示内存信息的场景
        /// </summary>
        public void HideMemoryInfo()
        {
            MemoryInfoContent.Children.Clear();
            MemoryInfoExpander.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 显示示例Managed对象（用于测试）
        /// </summary>
        public void ShowSampleManagedObject()
        {
            var sampleFields = ManagedObjectInspector.CreateSampleFields();
            SetupManagedObjectInspector(sampleFields);
        }

        #endregion

        #region 引用浏览器集成 (参考Unity: ObjectDetailsViewController)

        /// <summary>
        /// 设置引用浏览器的根对象
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectDetailsViewController.RefreshView
        /// </summary>
        /// <param name="source">要浏览引用的源对象</param>
        internal void SetupReferences(SourceIndex source)
        {
            if (m_Snapshot == null)
            {
                System.Diagnostics.Debug.WriteLine("SelectionDetailsPanel: Cannot setup references without snapshot");
                HideReferences();
                return;
            }

            if (!source.Valid)
            {
                HideReferences();
                return;
            }

            // 检查是否有引用数据可显示
            bool hasReferencesData = HasReferencesData(m_Snapshot, source);
            
            if (!hasReferencesData)
            {
                HideReferences();
                return;
            }

            // 设置PathsToRootView的根对象
            PathsToRootViewControl.SetRoot(m_Snapshot, source);
            
            // 显示引用浏览器
            ReferencesExpander.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 隐藏引用浏览器
        /// </summary>
        public void HideReferences()
        {
            PathsToRootViewControl.ClearSelection();
            ReferencesExpander.Visibility = Visibility.Collapsed;
        }

        /// <summary>
        /// 检查对象是否有引用数据
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectDetailsViewController.HasReferencesData
        /// </summary>
        private static bool HasReferencesData(CachedSnapshot snapshot, SourceIndex sourceIndex)
        {
            if (!sourceIndex.Valid)
                return false;

            var references = new List<ObjectData>();
            ObjectConnection.GetAllReferencingObjects(snapshot, sourceIndex, ref references);
            var refCount = references.Count;
            ObjectConnection.GetAllReferencedObjects(snapshot, sourceIndex, ref references);
            return refCount + references.Count > 0;
        }

        /// <summary>
        /// 处理引用浏览器中的选择变化事件
        /// 参考: Unity.MemoryProfiler.Editor.UI.ObjectDetailsViewController.UpdateSelectionDetails
        /// </summary>
        private void OnReferencesSelectionChanged(SourceIndex selectedSource)
        {
            if (m_Snapshot == null || !selectedSource.Valid)
                return;

            // 优先使用SelectedItemDetailsBuilder系统处理选择（包括Managed Fields）
            // 参考: ShowDetails方法使用DetailsBuilder.SetSelection
            if (m_DetailsBuilder != null)
            {
                m_DetailsBuilder.SetSelection(selectedSource);
                return;
            }

            // 回退到旧逻辑（如果DetailsBuilder未初始化）
            var objectData = ObjectData.FromSourceLink(m_Snapshot, selectedSource);
            if (!objectData.IsValid)
                return;

            // 清空当前详情
            BasicInfoContent.Children.Clear();
            MemoryInfoContent.Children.Clear();
            AdvancedInfoContent.Children.Clear();
            DescriptionExpander.Visibility = Visibility.Collapsed;
            HideManagedObjectInspector();

            // 根据对象类型显示详情
            if (objectData.isNativeObject)
            {
                TitleTextBlock.Text = $"Native Object: {objectData.GenerateObjectName(m_Snapshot)}";

                // 基本信息
                AddPropertyRow(BasicInfoContent, "Name", objectData.GenerateObjectName(m_Snapshot));
                AddPropertyRow(BasicInfoContent, "Type", objectData.GenerateTypeName(m_Snapshot, truncateTypeName: false));
                AddPropertyRow(BasicInfoContent, "Native Index", objectData.nativeObjectIndex.ToString());

                // 内存信息
                var nativeSize = m_Snapshot.NativeObjects.Size[objectData.nativeObjectIndex];
                AddPropertyRow(MemoryInfoContent, "Native Size", EditorUtility.FormatBytes((long)nativeSize),
                    $"{nativeSize:N0} B\n\nSize of the native C++ object");

                // 高级信息
                var nativeTypeIndex = m_Snapshot.NativeObjects.NativeTypeArrayIndex[objectData.nativeObjectIndex];
                if (nativeTypeIndex >= 0)
                {
                    AddPropertyRow(AdvancedInfoContent, "Native Type Index", nativeTypeIndex.ToString());
                }
                var instanceId = m_Snapshot.NativeObjects.InstanceId[objectData.nativeObjectIndex];
                AddPropertyRow(AdvancedInfoContent, "Instance ID", instanceId.ToString());
                AddPropertyRow(AdvancedInfoContent, "Native Address", $"0x{objectData.GetObjectPointer(m_Snapshot, false):X}");
            }
            else if (objectData.isManaged)
            {
                var managedObject = objectData.GetManagedObject(m_Snapshot);
                TitleTextBlock.Text = $"Managed Object: 0x{objectData.hostManagedObjectPtr:X}";

                // 基本信息
                AddPropertyRow(BasicInfoContent, "Type", objectData.GenerateTypeName(m_Snapshot, truncateTypeName: false));
                AddPropertyRow(BasicInfoContent, "Managed Type Index", objectData.managedTypeIndex.ToString());

                // 内存信息
                var managedSize = managedObject.Size;
                AddPropertyRow(MemoryInfoContent, "Managed Size", EditorUtility.FormatBytes(managedSize),
                    $"{managedSize:N0} B\n\nSize of the managed C# object on the managed heap");

                // 高级信息
                AddPropertyRow(AdvancedInfoContent, "Managed Address", $"0x{objectData.hostManagedObjectPtr:X}");
                AddPropertyRow(AdvancedInfoContent, "Ref Count", managedObject.RefCount.ToString());

                // Managed Fields 现在由 SelectedItemDetailsBuilder 和 Presenter 系统处理
                // 不需要在这里设置占位内容
            }
            else
            {
                TitleTextBlock.Text = "Unknown Object";
                AddPropertyRow(BasicInfoContent, "Type", objectData.GenerateTypeName(m_Snapshot, truncateTypeName: false));
            }

            // 显示/隐藏Expander
            MemoryInfoExpander.Visibility = MemoryInfoContent.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            AdvancedInfoExpander.Visibility = AdvancedInfoContent.Children.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

            // 显示详情内容
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 恢复UI状态（Expander折叠状态）
        /// 参考: Unity使用EditorPrefs保存UI状态
        /// </summary>
        private void RestoreUIState()
        {
            // 恢复Expander的展开状态（默认展开）
            BasicInfoExpander.IsExpanded = Services.UIStateManager.GetBool($"{StateKeyPrefix}BasicExpanded", true);
            MemoryInfoExpander.IsExpanded = Services.UIStateManager.GetBool($"{StateKeyPrefix}MemoryExpanded", true);
            AdvancedInfoExpander.IsExpanded = Services.UIStateManager.GetBool($"{StateKeyPrefix}AdvancedExpanded", false);
            DescriptionExpander.IsExpanded = Services.UIStateManager.GetBool($"{StateKeyPrefix}DescriptionExpanded", true);
            ReferencesExpander.IsExpanded = Services.UIStateManager.GetBool($"{StateKeyPrefix}ReferencesExpanded", true);
            ManagedFieldsExpander.IsExpanded = Services.UIStateManager.GetBool($"{StateKeyPrefix}ManagedFieldsExpanded", true);
        }

        /// <summary>
        /// 保存Expander的展开/折叠状态
        /// </summary>
        private void SaveExpanderState(string expanderName, bool isExpanded)
        {
            Services.UIStateManager.SetBool($"{StateKeyPrefix}{expanderName}Expanded", isExpanded);
        }

        #endregion

        #region Public API for SelectedItemDetailsBuilder (SelectedItemDetailsBuilder使用的公共API)

        /// <summary>
        /// 设置项目名称（标题） - 字符串版本
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.SetItemName
        /// </summary>
        public void SetItemName(string name)
        {
            TitleTextBlock.Text = name ?? "Unknown";
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 设置项目名称（标题） - UnifiedType版本
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.SetItemName(UnifiedType)
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

            TitleTextBlock.Text = displayName;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 设置项目名称（标题） - ObjectData + UnifiedType版本
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.SetItemName(ObjectData, UnifiedType)
        /// </summary>
        internal void SetItemName(ObjectData objectData, UnifiedType type)
        {
            if (!objectData.IsValid)
            {
                SetItemName("Unknown Object");
                return;
            }

            // 根据数据类型生成标题
            string displayName;
            if (objectData.isManaged && m_Snapshot != null)
            {
                // 对于Managed对象，显示地址 + 类型名
                displayName = $"Managed Object: 0x{objectData.hostManagedObjectPtr:X} ({objectData.GenerateTypeName(m_Snapshot, false)})";
            }
            else
            {
                displayName = objectData.GenerateTypeName(m_Snapshot, false) ?? "Unknown Object";
            }

            TitleTextBlock.Text = displayName;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 设置项目名称（标题） - UnifiedUnityObjectInfo版本
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.SetItemName(UnifiedUnityObjectInfo)
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

            TitleTextBlock.Text = displayName;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 设置项目名称（标题） - SourceIndex版本 (用于NativeAllocation/GfxResource等)
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.SetItemName(SourceIndex)
        /// </summary>
        internal void SetItemName(SourceIndex sourceIndex)
        {
            if (m_Snapshot == null)
            {
                SetItemName("Unknown");
                return;
            }

            string displayName = sourceIndex.Id switch
            {
                SourceIndex.SourceId.NativeAllocation => 
                    m_Snapshot.NativeAllocations.ProduceAllocationNameForRootReferenceId(
                        m_Snapshot, 
                        m_Snapshot.NativeAllocations.RootReferenceId[sourceIndex.Index], 
                        higlevelObjectNameOnlyIfAvailable: false),
                SourceIndex.SourceId.GfxResource => 
                    "Graphics Resource",  // TODO: 从GfxResource获取名称
                SourceIndex.SourceId.NativeRootReference => 
                    m_Snapshot.NativeRootReferences.AreaName[sourceIndex.Index] + " / " + 
                    m_Snapshot.NativeRootReferences.ObjectName[sourceIndex.Index],
                _ => "Unknown"
            };

            TitleTextBlock.Text = displayName;
            NoSelectionMessage.Visibility = Visibility.Collapsed;
            DetailsContent.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// 设置描述文本
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel.SetDescription
        /// </summary>
        public void SetDescription(string description)
        {
            if (!string.IsNullOrEmpty(description))
            {
                DescriptionText.Text = description;
                DescriptionExpander.Visibility = Visibility.Visible;
            }
            else
            {
                DescriptionExpander.Visibility = Visibility.Collapsed;
            }
        }

        #endregion
    }
}

