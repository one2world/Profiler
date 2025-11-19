using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.ModelBuilders.Comparison;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Models.Comparison;
using Unity.MemoryProfiler.UI.Services;

namespace Unity.MemoryProfiler.UI.ViewModels
{
    public class UnityObjectsViewModel : INotifyPropertyChanged
    {
        private CachedSnapshot? _currentSnapshot;
        private UnityObjectsData? _unityObjectsData;
        private string _statusMessage = "No snapshot loaded";
        private bool _flattenHierarchy;
        private bool _showPotentialDuplicatesOnly;
        private UnityObjectTreeNode? _selectedNode;
        private bool _isDetailsPanelVisible = true; // 默认显示

        // 对比模式相关字段
        private bool _isComparisonMode;
        private CachedSnapshot? _snapshotA;
        private CachedSnapshot? _snapshotB;
        private ComparisonTableModel? _comparisonModel;
        private ComparisonTreeNode? _selectedComparisonNode;
        
        // 完整的子表数据（初始化时构建一次）
        private UnityObjectsData? _baseFullData;
        private UnityObjectsData? _comparedFullData;
        
        // 过滤后的子表数据（用于显示）
        private List<UnityObjectTreeNode>? _baseFilteredNodes;
        private List<UnityObjectTreeNode>? _comparedFilteredNodes;
        
        private UnityObjectTreeNode? _selectedBaseNode;
        private UnityObjectTreeNode? _selectedComparedNode;
        private string _baseDescription = string.Empty;
        private string _comparedDescription = string.Empty;
        
        // 对比模式的 Toggle 选项
        private bool _includeUnchanged = false;

        /// <summary>
        /// 当前加载的快照（单快照模式）或 Base 快照（对比模式）
        /// </summary>
        internal CachedSnapshot? CurrentSnapshot
        {
            get => _currentSnapshot ?? _snapshotA;
        }

        /// <summary>
        /// Compared 快照（对比模式）
        /// </summary>
        internal CachedSnapshot? ComparedSnapshot
        {
            get => _snapshotB;
        }

        public UnityObjectsData? UnityObjectsData
        {
            get => _unityObjectsData;
            private set => SetProperty(ref _unityObjectsData, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool FlattenHierarchy
        {
            get => _flattenHierarchy;
            set
            {
                if (SetProperty(ref _flattenHierarchy, value))
                {
                    // 单快照模式或对比模式都需要重建数据
                    if (_isComparisonMode)
                        RebuildComparisonData();
                    else
                    RebuildData();
                }
            }
        }

        public bool ShowPotentialDuplicatesOnly
        {
            get => _showPotentialDuplicatesOnly;
            set
            {
                if (SetProperty(ref _showPotentialDuplicatesOnly, value))
                {
                    // 只在单快照模式下有效
                    if (!_isComparisonMode)
                    RebuildData();
                }
            }
        }
        
        /// <summary>
        /// 是否包含未变化的项（对比模式）
        /// </summary>
        public bool IncludeUnchanged
        {
            get => _includeUnchanged;
            set
            {
                if (SetProperty(ref _includeUnchanged, value))
                {
                    // 只在对比模式下有效
                    if (_isComparisonMode)
                        RebuildComparisonData();
                }
            }
        }

        public UnityObjectTreeNode? SelectedNode
        {
            get => _selectedNode;
            set => SetProperty(ref _selectedNode, value);
        }

        public bool IsDetailsPanelVisible
        {
            get => _isDetailsPanelVisible;
            set => SetProperty(ref _isDetailsPanelVisible, value);
        }

        // 对比模式属性
        public bool IsComparisonMode
        {
            get => _isComparisonMode;
            private set => SetProperty(ref _isComparisonMode, value);
        }

        public ComparisonTableModel? ComparisonModel
        {
            get => _comparisonModel;
            private set => SetProperty(ref _comparisonModel, value);
        }

        public ComparisonTreeNode? SelectedComparisonNode
        {
            get => _selectedComparisonNode;
            set
            {
                if (SetProperty(ref _selectedComparisonNode, value))
                {
                    OnSelectedComparisonNodeChanged(value);
                }
            }
        }

        /// <summary>
        /// Base 表的过滤后节点（用于 UI 绑定）
        /// </summary>
        public List<UnityObjectTreeNode>? BaseFilteredNodes
        {
            get => _baseFilteredNodes;
            private set => SetProperty(ref _baseFilteredNodes, value);
        }

        /// <summary>
        /// Compared 表的过滤后节点（用于 UI 绑定）
        /// </summary>
        public List<UnityObjectTreeNode>? ComparedFilteredNodes
        {
            get => _comparedFilteredNodes;
            private set => SetProperty(ref _comparedFilteredNodes, value);
        }

        /// <summary>
        /// Base 表选中的节点
        /// Unity 逻辑：选择 Base 表时，清空 Compared 表的选择（互斥）
        /// </summary>
        public UnityObjectTreeNode? SelectedBaseNode
        {
            get => _selectedBaseNode;
            set
            {
                // 先清空 Compared 表的选择（直接修改字段，不触发事件）
                if (value != null && _selectedComparedNode != null)
                {
                    _selectedComparedNode = null;
                    OnPropertyChanged(nameof(SelectedComparedNode)); // 通知 UI 更新
                }
                
                // 再设置 Base 表的选择
                SetProperty(ref _selectedBaseNode, value);
            }
        }

        /// <summary>
        /// Compared 表选中的节点
        /// Unity 逻辑：选择 Compared 表时，清空 Base 表的选择（互斥）
        /// </summary>
        public UnityObjectTreeNode? SelectedComparedNode
        {
            get => _selectedComparedNode;
            set
            {
                // 先清空 Base 表的选择（直接修改字段，不触发事件）
                if (value != null && _selectedBaseNode != null)
                {
                    _selectedBaseNode = null;
                    OnPropertyChanged(nameof(SelectedBaseNode)); // 通知 UI 更新
                }
                
                // 再设置 Compared 表的选择
                SetProperty(ref _selectedComparedNode, value);
            }
        }

        /// <summary>
        /// Base 表的描述文本
        /// </summary>
        public string BaseDescription
        {
            get => _baseDescription;
            private set => SetProperty(ref _baseDescription, value);
        }

        /// <summary>
        /// Compared 表的描述文本
        /// </summary>
        public string ComparedDescription
        {
            get => _comparedDescription;
            private set => SetProperty(ref _comparedDescription, value);
        }

        /// <summary>
        /// 是否显示 Base 描述
        /// </summary>
        public bool ShowBaseDescription => !string.IsNullOrEmpty(_baseDescription);

        /// <summary>
        /// 是否显示 Compared 描述
        /// </summary>
        public bool ShowComparedDescription => !string.IsNullOrEmpty(_comparedDescription);

        // 对比模式统计信息属性
        public string FormattedTotalSnapshotSizeA => FormatBytes(ComparisonModel?.TotalSnapshotSizeA ?? 0);
        public string FormattedTotalSnapshotSizeB => FormatBytes(ComparisonModel?.TotalSnapshotSizeB ?? 0);
        public string FormattedTotalSnapshotSizeDiff
        {
            get
            {
                if (ComparisonModel == null) return "0 B";
                var diff = (long)ComparisonModel.TotalSnapshotSizeB - (long)ComparisonModel.TotalSnapshotSizeA;
                var sign = diff > 0 ? "+" : "";
                return $"{sign}{FormatBytes((ulong)Math.Abs(diff))}";
            }
        }

        public string FormattedTotalSizeInTableA => FormatBytes(_baseFullData?.TotalSnapshotMemory ?? 0);
        public string FormattedTotalSizeInTableB => FormatBytes(_comparedFullData?.TotalSnapshotMemory ?? 0);
        public string FormattedTotalSizeInTableDiff
        {
            get
            {
                var sizeA = _baseFullData?.TotalSnapshotMemory ?? 0;
                var sizeB = _comparedFullData?.TotalSnapshotMemory ?? 0;
                var diff = (long)sizeB - (long)sizeA;
                var sign = diff > 0 ? "+" : "";
                return $"{sign}{FormatBytes((ulong)Math.Abs(diff))}";
            }
        }

        public IRelayCommand RefreshCommand { get; }
        public IRelayCommand CopyNameCommand { get; }
        public IRelayCommand CopyTypeCommand { get; }

        public UnityObjectsViewModel()
        {
            RefreshCommand = new RelayCommand(RebuildData);
            CopyNameCommand = new RelayCommand(CopyName, CanCopyName);
            CopyTypeCommand = new RelayCommand(CopyType, CanCopyType);
        }

        internal void LoadSnapshot(CachedSnapshot snapshot)
        {
            _currentSnapshot = snapshot;
            
            if (snapshot == null)
            {
                UnityObjectsData = null;
                StatusMessage = "No snapshot loaded";
                return;
            }

            RebuildData();
        }

        private void RebuildData()
        {
            if (_currentSnapshot == null)
                return;

            try
            {
                var builder = new UnityObjectsDataBuilder(_currentSnapshot);
                var options = new UnityObjectsDataBuilder.BuildOptions
                {
                    FlattenHierarchy = _flattenHierarchy,
                    ShowPotentialDuplicatesOnly = _showPotentialDuplicatesOnly,
                    DisambiguateByInstanceId = false
                };

                UnityObjectsData = builder.Build(options);
                StatusMessage = $"Loaded {UnityObjectsData.RootNodes.Count} Unity Object nodes";
            }
            catch (System.Exception ex)
            {
                UnityObjectsData = null;
                StatusMessage = $"Error loading snapshot: {ex.Message}";
            }
        }

        public void Clear()
        {
            _currentSnapshot = null;
            UnityObjectsData = null;
            SelectedNode = null;
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
        }

        /// <summary>
        /// 对比两个快照
        /// Unity 逻辑：初始化时构建完整的 Base/Compared 数据，然后通过过滤器显示
        /// </summary>
        internal void CompareSnapshots(CachedSnapshot snapshotA, CachedSnapshot snapshotB)
        {
            // 清空单快照模式的数据
            _currentSnapshot = null;
            UnityObjectsData = null;
            SelectedNode = null;
            
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
        /// 重新构建对比数据（当 FlattenHierarchy 或 IncludeUnchanged 变化时）
        /// </summary>
        private void RebuildComparisonData()
        {
            if (_snapshotA == null || _snapshotB == null)
                return;

            try
            {
                // 步骤1：构建对比模型（主表）
                ComparisonModel = UnityObjectsComparisonTableModelBuilder.Build(
                    _snapshotA, 
                    _snapshotB, 
                    _flattenHierarchy,
                    _includeUnchanged);
                IsComparisonMode = true;

                // 步骤2：构建完整的 Base 和 Compared 数据（一次性，后续只过滤）
                // Unity 逻辑：子表总是扁平化显示
                var options = new UnityObjectsDataBuilder.BuildOptions
                {
                    FlattenHierarchy = true, // 对比模式下子表总是扁平化
                    ShowPotentialDuplicatesOnly = false,
                    DisambiguateByInstanceId = false
                };

                var builderA = new UnityObjectsDataBuilder(_snapshotA);
                _baseFullData = builderA.Build(options);

                var builderB = new UnityObjectsDataBuilder(_snapshotB);
                _comparedFullData = builderB.Build(options);

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

                StatusMessage = $"Comparing {ComparisonModel.RootNodes.Count} Unity Object types";
            }
            catch (System.Exception ex)
            {
                ComparisonModel = null;
                IsComparisonMode = false;
                StatusMessage = $"Error comparing snapshots: {ex.Message}";
            }
        }

        /// <summary>
        /// 当选择对比节点时，应用过滤器
        /// Unity 逻辑：
        /// - 选择类型节点 → ShowNoObjectsAtAllFilter（显示空表）
        /// - 选择对象组节点 → 设置 Name/Type 过滤器
        /// - 选择具体对象节点（ID: xxx）→ 设置 InstanceID 过滤器
        /// </summary>
        private void OnSelectedComparisonNodeChanged(ComparisonTreeNode? node)
        {
            if (node == null || _baseFullData == null || _comparedFullData == null)
            {
                // 清空子表
                BaseFilteredNodes = null;
                ComparedFilteredNodes = null;
                BaseDescription = string.Empty;
                ComparedDescription = string.Empty;
                OnPropertyChanged(nameof(ShowBaseDescription));
                OnPropertyChanged(nameof(ShowComparedDescription));
                return;
            }

            // Unity 逻辑：直接从节点中获取原始对象列表
            // 注意：SourceNodesA 和 SourceNodesB 可能为 null（对象只在一个快照中存在）
            var nodesA = node.SourceNodesA as List<UnityObjectTreeNode>;
            var nodesB = node.SourceNodesB as List<UnityObjectTreeNode>;

            // 直接显示原始对象列表（允许为 null）
            BaseFilteredNodes = nodesA;
            ComparedFilteredNodes = nodesB;

            // 更新描述
            UpdateDescription(nodesA, nodesB, node.Data.Name);
        }

        /// <summary>
        /// 查找父节点的类型名称
        /// </summary>
        private string FindParentTypeName(ComparisonTreeNode node)
        {
            // 在 ComparisonModel.RootNodes 中查找包含此节点的父节点
            if (ComparisonModel?.RootNodes == null)
                return string.Empty;

            foreach (var rootNode in ComparisonModel.RootNodes)
            {
                if (rootNode.Children != null && rootNode.Children.Contains(node))
                {
                    return rootNode.Data.Name;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 应用过滤器到子表（对象组节点）
        /// Unity 逻辑：同时匹配 ObjectName 和 TypeName
        /// </summary>
        private void ApplyFilters(string objectName, string typeName)
        {
            if (_baseFullData == null || _comparedFullData == null)
                return;

            // 过滤 Base 表
            var baseFiltered = FilterNodesByNameAndType(_baseFullData.RootNodes, objectName, typeName);
            BaseFilteredNodes = baseFiltered;

            // 过滤 Compared 表
            var comparedFiltered = FilterNodesByNameAndType(_comparedFullData.RootNodes, objectName, typeName);
            ComparedFilteredNodes = comparedFiltered;

            // 更新描述
            UpdateDescription(baseFiltered, comparedFiltered, objectName);
        }

        /// <summary>
        /// 应用 InstanceID 过滤器到子表（具体对象节点）
        /// Unity 逻辑：只显示匹配 InstanceID 的对象
        /// </summary>
        private void ApplyInstanceIdFilter(string instanceIdString, string typeName)
        {
            if (_baseFullData == null || _comparedFullData == null)
                return;

            // 解析 InstanceID（格式："ID: 5558"）
            if (!TryParseInstanceId(instanceIdString, out var instanceId))
            {
                // 解析失败，显示空表
                BaseFilteredNodes = null;
                ComparedFilteredNodes = null;
                BaseDescription = string.Empty;
                ComparedDescription = string.Empty;
                OnPropertyChanged(nameof(ShowBaseDescription));
                OnPropertyChanged(nameof(ShowComparedDescription));
                return;
            }

            // 过滤 Base 表
            var baseFiltered = FilterNodesByInstanceId(_baseFullData.RootNodes, instanceId);
            BaseFilteredNodes = baseFiltered;

            // 过滤 Compared 表
            var comparedFiltered = FilterNodesByInstanceId(_comparedFullData.RootNodes, instanceId);
            ComparedFilteredNodes = comparedFiltered;

            // 更新描述（使用 "ID" 作为过滤名称）
            UpdateDescription(baseFiltered, comparedFiltered, "ID");
        }

        /// <summary>
        /// 解析 InstanceID 字符串
        /// </summary>
        private bool TryParseInstanceId(string instanceIdString, out int instanceId)
        {
            instanceId = 0;
            
            // 格式："ID: 5558"
            if (string.IsNullOrEmpty(instanceIdString) || !instanceIdString.StartsWith("ID: "))
                return false;

            var idPart = instanceIdString.Substring(4).Trim(); // 跳过 "ID: "
            return int.TryParse(idPart, out instanceId);
        }

        /// <summary>
        /// 根据名称和类型过滤节点（对象组节点）
        /// Unity 逻辑：
        /// - 匹配 ObjectName（node.Name）
        /// - 由于 FlattenHierarchy = true，所有节点都是扁平的
        /// - Unity 使用 Name + Type 过滤器，但在扁平结构中，我们只需匹配 Name
        /// </summary>
        private List<UnityObjectTreeNode> FilterNodesByNameAndType(List<UnityObjectTreeNode> nodes, string objectName, string typeName)
        {
            if (nodes == null || nodes.Count == 0)
                return new List<UnityObjectTreeNode>();

            var result = new List<UnityObjectTreeNode>();

            foreach (var node in nodes)
            {
                // Unity Objects 的树结构是扁平的（FlattenHierarchy = true）
                // 每个节点都是叶子节点，Name 包含了对象名称
                // Unity 的过滤逻辑是：同时匹配 Name 和 Type
                // 在扁平结构中，Name 通常是 "ObjectName (TypeName)" 或只是 "ObjectName"
                bool nameMatches = node.Name != null && node.Name.Contains(objectName, System.StringComparison.OrdinalIgnoreCase);

                if (nameMatches)
                {
                    result.Add(node);
                }

                // 如果有子节点（理论上不应该有，因为 FlattenHierarchy = true），递归检查
                if (node.Children != null && node.Children.Count > 0)
                {
                    var filteredChildren = FilterNodesByNameAndType(node.Children, objectName, typeName);
                    result.AddRange(filteredChildren);
                }
            }

            return result;
        }

        /// <summary>
        /// 根据 InstanceID 过滤节点（具体对象节点）
        /// Unity 逻辑：只显示匹配 InstanceID 的对象
        /// </summary>
        private List<UnityObjectTreeNode> FilterNodesByInstanceId(List<UnityObjectTreeNode> nodes, int instanceId)
        {
            if (nodes == null || nodes.Count == 0)
                return new List<UnityObjectTreeNode>();

            var result = new List<UnityObjectTreeNode>();

            foreach (var node in nodes)
            {
                // 检查节点的 InstanceId 是否匹配
                if (node.InstanceId == instanceId)
                {
                    result.Add(node);
                }

                // 如果有子节点，递归检查
                if (node.Children != null && node.Children.Count > 0)
                {
                    var filteredChildren = FilterNodesByInstanceId(node.Children, instanceId);
                    result.AddRange(filteredChildren);
                }
            }

            return result;
        }

        /// <summary>
        /// 更新描述文本
        /// Unity 逻辑：显示对象数量和总大小
        /// </summary>
        private void UpdateDescription(List<UnityObjectTreeNode> baseNodes, List<UnityObjectTreeNode> comparedNodes, string filterName)
        {
            // Base 描述
            if (baseNodes != null && baseNodes.Count > 0)
            {
                var objectCount = baseNodes.Count;
                var totalSize = baseNodes.Sum(n => (long)n.TotalSize);
                BaseDescription = $"{objectCount:N0} object{(objectCount != 1 ? "s" : string.Empty)} with same name | Group size: {FormatBytes((ulong)totalSize)}";
            }
            else
            {
                BaseDescription = string.Empty;
            }

            // Compared 描述
            if (comparedNodes != null && comparedNodes.Count > 0)
            {
                var objectCount = comparedNodes.Count;
                var totalSize = comparedNodes.Sum(n => (long)n.TotalSize);
                ComparedDescription = $"{objectCount:N0} object{(objectCount != 1 ? "s" : string.Empty)} with same name | Group size: {FormatBytes((ulong)totalSize)}";
            }
            else
            {
                ComparedDescription = string.Empty;
            }

            OnPropertyChanged(nameof(ShowBaseDescription));
            OnPropertyChanged(nameof(ShowComparedDescription));
        }

        private static string FormatBytes(ulong bytes)
        {
            if (bytes >= 1024UL * 1024 * 1024)
                return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (bytes >= 1024UL * 1024)
                return $"{bytes / (1024.0 * 1024):F2} MB";
            if (bytes >= 1024UL)
                return $"{bytes / 1024.0:F2} KB";
            return $"{bytes} B";
        }

        /// <summary>
        /// 复制节点名称到剪贴板
        /// </summary>
        private void CopyName()
        {
            string? name = null;

            if (_isComparisonMode && _selectedComparisonNode != null)
            {
                name = _selectedComparisonNode.Name;
            }
            else if (!_isComparisonMode && _selectedNode != null)
            {
                name = _selectedNode.Name;
            }

            if (!string.IsNullOrEmpty(name))
            {
                System.Windows.Clipboard.SetText(name);
            }
        }

        /// <summary>
        /// 判断是否可以复制名称
        /// </summary>
        private bool CanCopyName()
        {
            return (_isComparisonMode && _selectedComparisonNode != null) ||
                   (!_isComparisonMode && _selectedNode != null);
        }

        /// <summary>
        /// 复制节点类型到剪贴板
        /// Unity Objects的Name字段本身就包含了类型信息（如"Camera"、"Transform"等）
        /// </summary>
        private void CopyType()
        {
            string? typeName = null;

            if (_isComparisonMode && _selectedComparisonNode != null)
            {
                // 对比模式下，Name包含类型信息（分组是按类型的）
                typeName = _selectedComparisonNode.Name;
            }
            else if (!_isComparisonMode && _selectedNode != null)
            {
                // 单快照模式下，Name也包含类型信息
                typeName = _selectedNode.Name;
            }

            if (!string.IsNullOrEmpty(typeName))
            {
                System.Windows.Clipboard.SetText(typeName);
            }
        }

        /// <summary>
        /// 判断是否可以复制类型
        /// </summary>
        private bool CanCopyType()
        {
            return (_isComparisonMode && _selectedComparisonNode != null) ||
                   (!_isComparisonMode && _selectedNode != null);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
