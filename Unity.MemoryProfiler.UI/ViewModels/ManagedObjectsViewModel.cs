using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.ModelBuilders.Comparison;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Models.Comparison;
using Unity.MemoryProfiler.UI.Services;
using UnityEditor;

namespace Unity.MemoryProfiler.UI.ViewModels
{
    public class ManagedObjectsViewModel : INotifyPropertyChanged
    {
        private CachedSnapshot? _currentSnapshot;
        private ManagedObjectsData? _managedObjectsData;
        private string _statusMessage = "No snapshot loaded";
        private ManagedCallStackNode? _selectedCallStackNode;
        private List<ManagedObjectDetailNode>? _detailNodes;
        private ManagedObjectDetailNode? _selectedDetailNode;
        private List<AllocationSite>? _allocationSites;
        private AllocationSite? _selectedAllocationSite;
        private bool _isReversedMode = false;

        // 对比模式相关字段
        private bool _isComparisonMode;
        private CachedSnapshot? _snapshotA;
        private CachedSnapshot? _snapshotB;
        private ComparisonTableModel? _comparisonModel;
        private ComparisonTreeNode? _selectedComparisonNode;

        // 完整的子表数据（初始化时构建一次）
        private ManagedObjectsData? _baseFullData;
        private ManagedObjectsData? _comparedFullData;

        // 过滤后的子表数据（用于显示 - Objects Grouped by Type）
        private List<ManagedObjectDetailNode>? _baseFilteredNodes;
        private List<ManagedObjectDetailNode>? _comparedFilteredNodes;

        private ManagedObjectDetailNode? _selectedBaseNode;
        private ManagedObjectDetailNode? _selectedComparedNode;
        
        // 子表的 AllocationSites
        private List<AllocationSite>? _baseAllocationSites;
        private List<AllocationSite>? _comparedAllocationSites;
        private string _baseDescription = string.Empty;
        private string _comparedDescription = string.Empty;

        // 对比模式的 Toggle 选项
        private bool _includeUnchanged = false;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 当前加载的快照（单快照模式）或 Base 快照（对比模式）
        /// </summary>
        internal CachedSnapshot? CurrentSnapshot
        {
            get => _currentSnapshot;
        }

        /// <summary>
        /// 对比快照（仅对比模式）
        /// </summary>
        internal CachedSnapshot? ComparedSnapshot
        {
            get => _snapshotB;
        }

        /// <summary>
        /// 是否为对比模式
        /// </summary>
        public bool IsComparisonMode
        {
            get => _isComparisonMode;
            private set
            {
                if (SetProperty(ref _isComparisonMode, value))
                {
                    OnPropertyChanged(nameof(IsSingleSnapshotMode));
                }
            }
        }

        /// <summary>
        /// 是否为单快照模式
        /// </summary>
        public bool IsSingleSnapshotMode => !_isComparisonMode;

        /// <summary>
        /// 对比模型
        /// </summary>
        public ComparisonTableModel? ComparisonModel
        {
            get => _comparisonModel;
            private set => SetProperty(ref _comparisonModel, value);
        }

        /// <summary>
        /// 选中的对比节点
        /// </summary>
        public ComparisonTreeNode? SelectedComparisonNode
        {
            get => _selectedComparisonNode;
            set
            {
                if (SetProperty(ref _selectedComparisonNode, value))
                {
                    OnComparisonNodeSelected(value);
                }
            }
        }

        /// <summary>
        /// Base 子表过滤后的节点（Objects Grouped by Type）
        /// </summary>
        public List<ManagedObjectDetailNode>? BaseFilteredNodes
        {
            get => _baseFilteredNodes;
            private set => SetProperty(ref _baseFilteredNodes, value);
        }

        /// <summary>
        /// Compared 子表过滤后的节点（Objects Grouped by Type）
        /// </summary>
        public List<ManagedObjectDetailNode>? ComparedFilteredNodes
        {
            get => _comparedFilteredNodes;
            private set => SetProperty(ref _comparedFilteredNodes, value);
        }

        /// <summary>
        /// Base 子表选中的节点
        /// </summary>
        public ManagedObjectDetailNode? SelectedBaseNode
        {
            get => _selectedBaseNode;
            set
            {
                if (SetProperty(ref _selectedBaseNode, value))
                {
                    // 互斥选择：选择 Base 时清空 Compared
                    if (value != null && _selectedComparedNode != null)
                    {
                        SelectedComparedNode = null;
                    }
                    OnBaseNodeSelected(value);
                }
            }
        }

        /// <summary>
        /// Compared 子表选中的节点
        /// </summary>
        public ManagedObjectDetailNode? SelectedComparedNode
        {
            get => _selectedComparedNode;
            set
            {
                if (SetProperty(ref _selectedComparedNode, value))
                {
                    // 互斥选择：选择 Compared 时清空 Base
                    if (value != null && _selectedBaseNode != null)
                    {
                        SelectedBaseNode = null;
                    }
                    OnComparedNodeSelected(value);
                }
            }
        }

        /// <summary>
        /// Base 子表的 AllocationSites
        /// </summary>
        public List<AllocationSite>? BaseAllocationSites
        {
            get => _baseAllocationSites;
            private set => SetProperty(ref _baseAllocationSites, value);
        }

        /// <summary>
        /// Compared 子表的 AllocationSites
        /// </summary>
        public List<AllocationSite>? ComparedAllocationSites
        {
            get => _comparedAllocationSites;
            private set => SetProperty(ref _comparedAllocationSites, value);
        }

        /// <summary>
        /// Base 描述文本
        /// </summary>
        public string BaseDescription
        {
            get => _baseDescription;
            private set
            {
                if (SetProperty(ref _baseDescription, value))
                {
                    OnPropertyChanged(nameof(ShowBaseDescription));
                }
            }
        }

        /// <summary>
        /// Compared 描述文本
        /// </summary>
        public string ComparedDescription
        {
            get => _comparedDescription;
            private set
            {
                if (SetProperty(ref _comparedDescription, value))
                {
                    OnPropertyChanged(nameof(ShowComparedDescription));
                }
            }
        }

        /// <summary>
        /// 是否显示 Base 描述
        /// </summary>
        public bool ShowBaseDescription => !string.IsNullOrEmpty(_baseDescription);

        /// <summary>
        /// 是否显示 Compared 描述
        /// </summary>
        public bool ShowComparedDescription => !string.IsNullOrEmpty(_comparedDescription);

        /// <summary>
        /// 是否包含未改变的项
        /// </summary>
        public bool IncludeUnchanged
        {
            get => _includeUnchanged;
            set
            {
                if (SetProperty(ref _includeUnchanged, value))
                {
                    RebuildComparisonData();
                }
            }
        }

        /// <summary>
        /// 格式化的快照 A 总大小
        /// </summary>
        public string FormattedTotalSnapshotSizeA => _comparisonModel != null
            ? EditorUtility.FormatBytes((long)_comparisonModel.TotalSnapshotSizeA)
            : "0 B";

        /// <summary>
        /// 格式化的快照 B 总大小
        /// </summary>
        public string FormattedTotalSnapshotSizeB => _comparisonModel != null
            ? EditorUtility.FormatBytes((long)_comparisonModel.TotalSnapshotSizeB)
            : "0 B";

        /// <summary>
        /// 格式化的快照总大小差异
        /// </summary>
        public string FormattedTotalSnapshotSizeDiff
        {
            get
            {
                if (_comparisonModel == null)
                    return "0 B";

                var diff = (long)_comparisonModel.TotalSnapshotSizeB - (long)_comparisonModel.TotalSnapshotSizeA;
                var sign = diff >= 0 ? "+" : "";
                return $"{sign}{EditorUtility.FormatBytes(diff)}";
            }
        }

        /// <summary>
        /// 格式化的表中 A 总大小
        /// </summary>
        public string FormattedTotalSizeInTableA => _comparisonModel != null
            ? EditorUtility.FormatBytes((long)_comparisonModel.TotalSizeA)
            : "0 B";

        /// <summary>
        /// 格式化的表中 B 总大小
        /// </summary>
        public string FormattedTotalSizeInTableB => _comparisonModel != null
            ? EditorUtility.FormatBytes((long)_comparisonModel.TotalSizeB)
            : "0 B";

        /// <summary>
        /// 格式化的表中总大小差异
        /// </summary>
        public string FormattedTotalSizeInTableDiff
        {
            get
            {
                if (_comparisonModel == null)
                    return "0 B";

                var diff = (long)_comparisonModel.TotalSizeB - (long)_comparisonModel.TotalSizeA;
                var sign = diff >= 0 ? "+" : "";
                return $"{sign}{EditorUtility.FormatBytes(diff)}";
            }
        }

        /// <summary>
        /// Managed Objects 数据
        /// </summary>
        public ManagedObjectsData? ManagedObjectsData
        {
            get => _managedObjectsData;
            private set => SetProperty(ref _managedObjectsData, value);
        }

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 选中的调用栈节点
        /// </summary>
        public ManagedCallStackNode? SelectedCallStackNode
        {
            get => _selectedCallStackNode;
            set
            {
                if (SetProperty(ref _selectedCallStackNode, value))
                {
                    OnCallStackNodeSelected(value);
                }
            }
        }

        /// <summary>
        /// 详情节点列表（选中调用栈的对象列表）
        /// </summary>
        public List<ManagedObjectDetailNode>? DetailNodes
        {
            get => _detailNodes;
            private set => SetProperty(ref _detailNodes, value);
        }

        /// <summary>
        /// 选中的详情节点
        /// </summary>
        public ManagedObjectDetailNode? SelectedDetailNode
        {
            get => _selectedDetailNode;
            set
            {
                if (SetProperty(ref _selectedDetailNode, value))
                {
                    OnDetailNodeSelected(value);
                }
            }
        }

        /// <summary>
        /// 分配站点列表（文件路径和行号）
        /// </summary>
        public List<AllocationSite>? AllocationSites
        {
            get => _allocationSites;
            private set => SetProperty(ref _allocationSites, value);
        }

        /// <summary>
        /// 选中的分配站点
        /// </summary>
        public AllocationSite? SelectedAllocationSite
        {
            get => _selectedAllocationSite;
            set => SetProperty(ref _selectedAllocationSite, value);
        }

        /// <summary>
        /// 是否为 Reversed 模式（按堆栈反向聚合）
        /// </summary>
        public bool IsReversedMode
        {
            get => _isReversedMode;
            set
            {
                if (SetProperty(ref _isReversedMode, value))
                {
                    RebuildData();
                }
            }
        }

        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand ToggleReversedModeCommand { get; }

        public ManagedObjectsViewModel()
        {
            RefreshCommand = new RelayCommand(RebuildData);
            ToggleReversedModeCommand = new RelayCommand(ToggleReversedMode);
        }

        private void ToggleReversedMode()
        {
            IsReversedMode = !IsReversedMode;
        }

        /// <summary>
        /// 加载快照
        /// </summary>
        internal void LoadSnapshot(CachedSnapshot snapshot)
        {
            _currentSnapshot = snapshot;

            if (snapshot == null)
            {
                ManagedObjectsData = null;
                DetailNodes = null;
                StatusMessage = "No snapshot loaded";
                return;
            }

            RebuildData();
        }

        /// <summary>
        /// 重新构建数据
        /// </summary>
        private void RebuildData()
        {
            // 如果是对比模式，重新构建对比数据
            if (_isComparisonMode && _snapshotA != null && _snapshotB != null)
            {
                RebuildComparisonData();
                return;
            }

            // 单快照模式
            if (_currentSnapshot == null)
                return;

            try
            {
                var builder = new ManagedObjectsDataBuilder(_currentSnapshot);
                ManagedObjectsData = _isReversedMode ? builder.BuildReversed() : builder.Build();
                var modeText = _isReversedMode ? " (Reversed)" : "";
                StatusMessage = $"Loaded {ManagedObjectsData.RootNodes.Count} call stacks{modeText}, {ManagedObjectsData.TotalCount} objects, {ManagedObjectsData.FormattedTotalSize}";
            }
            catch (System.Exception ex)
            {
                ManagedObjectsData = null;
                StatusMessage = $"Error loading snapshot: {ex.Message}";
            }
        }

        /// <summary>
        /// 调用栈节点选中事件处理
        /// </summary>
        private void OnCallStackNodeSelected(ManagedCallStackNode? node)
        {
            if (node == null || _currentSnapshot == null)
            {
                DetailNodes = null;
                AllocationSites = null;
                return;
            }

            try
            {
                var builder = new ManagedObjectsDataBuilder(_currentSnapshot);
                DetailNodes = builder.BuildDetailForCallStack(node);
                AllocationSites = node.AllocationSites?.Count > 0 ? node.AllocationSites : null;
            }
            catch (System.Exception ex)
            {
                DetailNodes = null;
                AllocationSites = null;
                StatusMessage = $"Error loading details: {ex.Message}";
            }
        }

        /// <summary>
        /// 详情节点选中事件处理
        /// </summary>
        private void OnDetailNodeSelected(ManagedObjectDetailNode? node)
        {
            // 可以在这里添加详情节点选中后的逻辑
            // 例如：触发 SelectionDetailsService 显示对象详情
        }

        /// <summary>
        /// 对比节点选中事件处理
        /// </summary>
        private void OnComparisonNodeSelected(ComparisonTreeNode? node)
        {
            if (node == null || _baseFullData == null || _comparedFullData == null || _snapshotA == null || _snapshotB == null)
            {
                BaseFilteredNodes = null;
                ComparedFilteredNodes = null;
                BaseDescription = string.Empty;
                ComparedDescription = string.Empty;
                BaseAllocationSites = null;
                ComparedAllocationSites = null;
                return;
            }

            // 使用 ItemPath 来精确定位节点
            var itemPath = node.Data.ItemPath;
            
            // Base 子表：通过 ItemPath 找到对应的调用栈节点
            var baseCallStackNode = FindCallStackNodeByItemPath(_baseFullData.RootNodes, itemPath);
            if (baseCallStackNode != null)
            {
                var builderA = new ManagedObjectsDataBuilder(_snapshotA);
                BaseFilteredNodes = builderA.BuildDetailForCallStack(baseCallStackNode);
                BaseAllocationSites = baseCallStackNode.AllocationSites?.Count > 0 ? baseCallStackNode.AllocationSites : null;
                BaseDescription = $"Objects in '{node.Name}' (Base snapshot)";
            }
            else
            {
                BaseFilteredNodes = null;
                BaseAllocationSites = null;
                BaseDescription = $"No call stack found for '{node.Name}' in Base snapshot";
            }

            // Compared 子表：通过 ItemPath 找到对应的调用栈节点
            var comparedCallStackNode = FindCallStackNodeByItemPath(_comparedFullData.RootNodes, itemPath);
            if (comparedCallStackNode != null)
            {
                var builderB = new ManagedObjectsDataBuilder(_snapshotB);
                ComparedFilteredNodes = builderB.BuildDetailForCallStack(comparedCallStackNode);
                ComparedAllocationSites = comparedCallStackNode.AllocationSites?.Count > 0 ? comparedCallStackNode.AllocationSites : null;
                ComparedDescription = $"Objects in '{node.Name}' (Compared snapshot)";
            }
            else
            {
                ComparedFilteredNodes = null;
                ComparedAllocationSites = null;
                ComparedDescription = $"No call stack found for '{node.Name}' in Compared snapshot";
            }
        }

        /// <summary>
        /// 根据 ItemPath 查找调用栈节点
        /// </summary>
        private ManagedCallStackNode? FindCallStackNodeByItemPath(List<ManagedCallStackNode> nodes, List<string> itemPath)
        {
            if (nodes == null || itemPath == null || itemPath.Count == 0)
                return null;

            // ItemPath 是从根到当前节点的路径
            // 例如：["Function1", "Function2", "Function3"]
            // 我们需要逐层匹配
            
            var currentNodes = nodes;
            ManagedCallStackNode? currentNode = null;
            
            foreach (var pathSegment in itemPath)
            {
                bool found = false;
                foreach (var node in currentNodes)
                {
                    if (node.Description.Equals(pathSegment, System.StringComparison.OrdinalIgnoreCase))
                    {
                        currentNode = node;
                        currentNodes = node.Children ?? new List<ManagedCallStackNode>();
                        found = true;
                        break;
                    }
                }
                
                if (!found)
                {
                    // 路径中断，返回 null
                    return null;
                }
            }
            
            return currentNode;
        }

        /// <summary>
        /// Base 节点选中事件处理
        /// </summary>
        private void OnBaseNodeSelected(ManagedObjectDetailNode? node)
        {
            // 用于 Detail 联动
        }

        /// <summary>
        /// Compared 节点选中事件处理
        /// </summary>
        private void OnComparedNodeSelected(ManagedObjectDetailNode? node)
        {
            // 用于 Detail 联动
        }

        /// <summary>
        /// 对比两个快照
        /// Unity 逻辑：初始化时构建完整的 Base/Compared 数据，然后通过过滤器显示
        /// </summary>
        internal void CompareSnapshots(CachedSnapshot snapshotA, CachedSnapshot snapshotB)
        {
            // 清空单快照模式的数据
            _currentSnapshot = null;
            ManagedObjectsData = null;
            SelectedCallStackNode = null;
            DetailNodes = null;
            AllocationSites = null;

            _snapshotA = snapshotA;
            _snapshotB = snapshotB;

            if (snapshotA == null || snapshotB == null)
            {
                StatusMessage = "Both snapshots are required for comparison";
                return;
            }

            IsComparisonMode = true;
            RebuildComparisonData();
        }

        /// <summary>
        /// 重新构建对比数据（当 IsReversedMode 或 IncludeUnchanged 变化时）
        /// </summary>
        private void RebuildComparisonData()
        {
            if (_snapshotA == null || _snapshotB == null)
                return;

            try
            {
                // 步骤1：构建对比模型（主表）
                ComparisonModel = ManagedObjectsComparisonTableModelBuilder.Build(
                    _snapshotA,
                    _snapshotB,
                    _isReversedMode,
                    _includeUnchanged);
                IsComparisonMode = true;

                // 步骤2：构建完整的 Base 和 Compared 数据（一次性，后续只过滤）
                var builderA = new ManagedObjectsDataBuilder(_snapshotA);
                _baseFullData = _isReversedMode ? builderA.BuildReversed() : builderA.Build();

                var builderB = new ManagedObjectsDataBuilder(_snapshotB);
                _comparedFullData = _isReversedMode ? builderB.BuildReversed() : builderB.Build();

                // 步骤3：初始状态显示空表（Unity 使用 ShowNoObjectsAtAllFilter）
                BaseFilteredNodes = null;
                ComparedFilteredNodes = null;
                BaseDescription = string.Empty;
                ComparedDescription = string.Empty;
                OnPropertyChanged(nameof(ShowBaseDescription));
                OnPropertyChanged(nameof(ShowComparedDescription));

                // 触发统计信息属性更新
                OnPropertyChanged(nameof(FormattedTotalSnapshotSizeA));
                OnPropertyChanged(nameof(FormattedTotalSnapshotSizeB));
                OnPropertyChanged(nameof(FormattedTotalSnapshotSizeDiff));
                OnPropertyChanged(nameof(FormattedTotalSizeInTableA));
                OnPropertyChanged(nameof(FormattedTotalSizeInTableB));
                OnPropertyChanged(nameof(FormattedTotalSizeInTableDiff));

                var modeText = _isReversedMode ? " (Reversed)" : "";
                StatusMessage = $"Comparison loaded{modeText}: {ComparisonModel.RootNodes.Count} differences";
            }
            catch (System.Exception ex)
            {
                ComparisonModel = null;
                StatusMessage = $"Error comparing snapshots: {ex.Message}";
            }
        }

        /// <summary>
        /// 清空数据
        /// </summary>
        public void Clear()
        {
            _currentSnapshot = null;
            ManagedObjectsData = null;
            DetailNodes = null;
            AllocationSites = null;
            SelectedCallStackNode = null;
            SelectedDetailNode = null;
            SelectedAllocationSite = null;
            StatusMessage = "No snapshot loaded";

            // 清除对比模式数据
            IsComparisonMode = false;
            _snapshotA = null;
            _snapshotB = null;
            ComparisonModel = null;
            SelectedComparisonNode = null;
            _baseFullData = null;
            _comparedFullData = null;
            BaseFilteredNodes = null;
            ComparedFilteredNodes = null;
            SelectedBaseNode = null;
            SelectedComparedNode = null;
            BaseDescription = string.Empty;
            ComparedDescription = string.Empty;
            BaseAllocationSites = null;
            ComparedAllocationSites = null;
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

