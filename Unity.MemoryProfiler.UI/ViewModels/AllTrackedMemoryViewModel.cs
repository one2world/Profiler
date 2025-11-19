using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.UI.Models;
using Unity.MemoryProfiler.UI.ModelBuilders.Comparison;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Services;
using ComparisonTreeNode = Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode;
using ComparisonTableModel = Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTableModel;

namespace Unity.MemoryProfiler.UI.ViewModels
{
    public class AllTrackedMemoryViewModel : INotifyPropertyChanged
    {
        // 单快照模式字段
        private CachedSnapshot? _currentSnapshot;
        private AllTrackedMemoryData? _allTrackedMemoryData;
        private string _statusMessage = "No snapshot loaded";
        private AllTrackedMemoryTreeNode? _selectedNode;
        private bool _isDetailsPanelVisible = true; // 默认显示

        // 对比模式字段
        private CachedSnapshot? _comparedSnapshot;
        private ComparisonTableModel? _comparisonModel;
        private bool _isCompareMode;
        private ObservableCollection<ComparisonTreeNode> _comparisonRootNodes = new();
        private ComparisonTreeNode? _selectedComparisonNode;
        private AllTrackedMemoryData? _baseDetailedData;
        private AllTrackedMemoryData? _comparedDetailedData;
        private AllTrackedMemoryTreeNode? _baseSelectedNode;
        private AllTrackedMemoryTreeNode? _comparedSelectedNode;
        private bool _includeUnchanged = false;
        private CancellationTokenSource? _detailLoadingCts;

        /// <summary>
        /// 当前加载的快照（Base snapshot in compare mode）
        /// </summary>
        internal CachedSnapshot? CurrentSnapshot
        {
            get => _currentSnapshot;
        }

        /// <summary>
        /// 对比快照（Compared snapshot in compare mode）
        /// </summary>
        internal CachedSnapshot? ComparedSnapshot
        {
            get => _comparedSnapshot;
        }

        public AllTrackedMemoryData? AllTrackedMemoryData
        {
            get => _allTrackedMemoryData;
            private set => SetProperty(ref _allTrackedMemoryData, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public AllTrackedMemoryTreeNode? SelectedNode
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
        public bool IsCompareMode
        {
            get => _isCompareMode;
            private set => SetProperty(ref _isCompareMode, value);
        }

        public ObservableCollection<ComparisonTreeNode> ComparisonRootNodes
        {
            get => _comparisonRootNodes;
            private set => SetProperty(ref _comparisonRootNodes, value);
        }

        // 对比模式统计信息（便捷属性）
        public double TotalSnapshotSizeA_MB => _comparisonModel != null 
            ? _comparisonModel.TotalSnapshotSizeA / (1024.0 * 1024.0) 
            : 0;

        public double TotalSnapshotSizeB_MB => _comparisonModel != null 
            ? _comparisonModel.TotalSnapshotSizeB / (1024.0 * 1024.0) 
            : 0;

        public double TotalSnapshotSizeDiff_MB => TotalSnapshotSizeB_MB - TotalSnapshotSizeA_MB;

        public double TotalSizeInTableA_MB => _comparisonModel != null 
            ? _comparisonModel.TotalSizeA / (1024.0 * 1024.0) 
            : 0;

        public double TotalSizeInTableB_MB => _comparisonModel != null 
            ? _comparisonModel.TotalSizeB / (1024.0 * 1024.0) 
            : 0;

        public double TotalSizeInTableDiff_MB => TotalSizeInTableB_MB - TotalSizeInTableA_MB;

        public string FormattedTotalSnapshotSizeA => FormatBytes(_comparisonModel?.TotalSnapshotSizeA ?? 0);
        public string FormattedTotalSnapshotSizeB => FormatBytes(_comparisonModel?.TotalSnapshotSizeB ?? 0);
        public string FormattedTotalSnapshotSizeDiff => FormatBytesDelta((long)(_comparisonModel?.TotalSnapshotSizeB ?? 0) - (long)(_comparisonModel?.TotalSnapshotSizeA ?? 0));

        public string FormattedTotalSizeInTableA => FormatBytes(_comparisonModel?.TotalSizeA ?? 0);
        public string FormattedTotalSizeInTableB => FormatBytes(_comparisonModel?.TotalSizeB ?? 0);
        public string FormattedTotalSizeInTableDiff => FormatBytesDelta((long)(_comparisonModel?.TotalSizeB ?? 0) - (long)(_comparisonModel?.TotalSizeA ?? 0));

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

        public AllTrackedMemoryData? BaseDetailedData
        {
            get => _baseDetailedData;
            private set => SetProperty(ref _baseDetailedData, value);
        }

        public AllTrackedMemoryData? ComparedDetailedData
        {
            get => _comparedDetailedData;
            private set => SetProperty(ref _comparedDetailedData, value);
        }

        public bool IncludeUnchanged
        {
            get => _includeUnchanged;
            set
            {
                if (SetProperty(ref _includeUnchanged, value) && IsCompareMode)
                {
                    RebuildComparisonData();
                }
            }
        }

        /// <summary>
        /// Base 子表选中的节点
        /// </summary>
        public AllTrackedMemoryTreeNode? BaseSelectedNode
        {
            get => _baseSelectedNode;
            set
            {
                if (SetProperty(ref _baseSelectedNode, value) && value != null)
                {
                    // 清除 Compared 子表的选择
                    ComparedSelectedNode = null;
                }
            }
        }

        /// <summary>
        /// Compared 子表选中的节点
        /// </summary>
        public AllTrackedMemoryTreeNode? ComparedSelectedNode
        {
            get => _comparedSelectedNode;
            set
            {
                if (SetProperty(ref _comparedSelectedNode, value) && value != null)
                {
                    // 清除 Base 子表的选择
                    BaseSelectedNode = null;
                }
            }
        }

        public IRelayCommand RefreshCommand { get; }

        public AllTrackedMemoryViewModel()
        {
            RefreshCommand = new RelayCommand(RebuildData);
        }

        internal void LoadSnapshot(CachedSnapshot snapshot)
        {
            _currentSnapshot = snapshot;
            _comparedSnapshot = null;
            IsCompareMode = false;
            
            if (snapshot == null)
            {
                AllTrackedMemoryData = null;
                StatusMessage = "No snapshot loaded";
                return;
            }

            RebuildData();
        }

        /// <summary>
        /// 对比两个快照（对比模式）
        /// </summary>
        internal void CompareSnapshots(CachedSnapshot baseSnapshot, CachedSnapshot comparedSnapshot)
        {
            _currentSnapshot = baseSnapshot;
            _comparedSnapshot = comparedSnapshot;
            IsCompareMode = true;

            if (baseSnapshot == null || comparedSnapshot == null)
            {
                StatusMessage = "Invalid snapshots for comparison";
                return;
            }

            RebuildComparisonData();
        }

        private void RebuildData()
        {
            if (_currentSnapshot == null)
                return;

            if (IsCompareMode)
            {
                RebuildComparisonData();
                return;
            }

            try
            {
                var builder = new AllTrackedMemoryDataBuilder(_currentSnapshot);
                AllTrackedMemoryData = builder.Build();
                StatusMessage = $"Loaded {AllTrackedMemoryData.RootNodes.Count} memory categories";
            }
            catch (System.Exception ex)
            {
                AllTrackedMemoryData = null;
                StatusMessage = $"Error loading snapshot: {ex.Message}";
            }
        }

        /// <summary>
        /// 重建对比数据（对比模式）
        /// </summary>
        private void RebuildComparisonData()
        {
            if (_currentSnapshot == null || _comparedSnapshot == null)
                return;

            try
            {
                // 构建对比模型
                _comparisonModel = AllTrackedMemoryComparisonTableModelBuilder.Build(
                    _currentSnapshot,
                    _comparedSnapshot,
                    _includeUnchanged);

                ComparisonRootNodes = new ObservableCollection<ComparisonTreeNode>(_comparisonModel.RootNodes);

                // 清空详细表
                BaseDetailedData = null;
                ComparedDetailedData = null;

                // 通知统计属性变化
                OnPropertyChanged(nameof(TotalSnapshotSizeA_MB));
                OnPropertyChanged(nameof(TotalSnapshotSizeB_MB));
                OnPropertyChanged(nameof(TotalSnapshotSizeDiff_MB));
                OnPropertyChanged(nameof(TotalSizeInTableA_MB));
                OnPropertyChanged(nameof(TotalSizeInTableB_MB));
                OnPropertyChanged(nameof(TotalSizeInTableDiff_MB));
                OnPropertyChanged(nameof(FormattedTotalSnapshotSizeA));
                OnPropertyChanged(nameof(FormattedTotalSnapshotSizeB));
                OnPropertyChanged(nameof(FormattedTotalSnapshotSizeDiff));
                OnPropertyChanged(nameof(FormattedTotalSizeInTableA));
                OnPropertyChanged(nameof(FormattedTotalSizeInTableB));
                OnPropertyChanged(nameof(FormattedTotalSizeInTableDiff));

                StatusMessage = $"Comparison loaded: {ComparisonRootNodes.Count} categories";
            }
            catch (System.Exception ex)
            {
                ComparisonRootNodes.Clear();
                StatusMessage = $"Error comparing snapshots: {ex.Message}";
            }
        }

        /// <summary>
        /// 选中对比节点变化事件
        /// Unity官方实现参考：AllTrackedMemoryComparisonViewController.OnTreeItemSelected (Line 109-149)
        /// </summary>
        private void OnSelectedComparisonNodeChanged(ComparisonTreeNode? value)
        {
            // 取消之前的加载任务
            _detailLoadingCts?.Cancel();
            _detailLoadingCts = new CancellationTokenSource();

            if (value == null || !IsCompareMode)
            {
                BaseDetailedData = null;
                ComparedDetailedData = null;
                return;
            }

            // Unity逻辑：所有节点（包括分组节点）都显示详细表
            // 使用 ItemPath 过滤显示该节点下的所有子项
            _ = LoadDetailedDataAsync(value, _detailLoadingCts.Token);
        }

        /// <summary>
        /// 异步加载详细数据
        /// </summary>
        private async Task LoadDetailedDataAsync(ComparisonTreeNode node, CancellationToken cancellationToken)
        {
            try
            {
                // 提取 ItemPath
                var itemPath = node.Data.ItemPath;
                
                if (itemPath == null || itemPath.Count == 0)
                {
                    // 没有路径信息，显示空表
                    BaseDetailedData = null;
                    ComparedDetailedData = null;
                    return;
                }

                StatusMessage = "Loading detail tables...";

                // 在后台线程构建数据
                var (baseData, comparedData) = await Task.Run(() =>
                {
                    if (cancellationToken.IsCancellationRequested)
                        return (null, null);

                    // Unity Line 120-149: 使用 ItemPath 过滤 Base 和 Compared 表
                    var baseArgs = new AllTrackedMemoryBuildArgs(itemPathFilter: itemPath);
                    var baseBuilder = new AllTrackedMemoryDataBuilder(_currentSnapshot!);
                    var baseResult = baseBuilder.Build(baseArgs);

                    if (cancellationToken.IsCancellationRequested)
                        return (null, null);

                    var comparedArgs = new AllTrackedMemoryBuildArgs(itemPathFilter: itemPath);
                    var comparedBuilder = new AllTrackedMemoryDataBuilder(_comparedSnapshot!);
                    var comparedResult = comparedBuilder.Build(comparedArgs);

                    return (baseResult, comparedResult);
                }, cancellationToken);

                if (cancellationToken.IsCancellationRequested)
                    return;

                // 更新 UI
                BaseDetailedData = baseData;
                ComparedDetailedData = comparedData;
                StatusMessage = "Detail tables loaded";
            }
            catch (System.OperationCanceledException)
            {
                // 正常取消，不需要处理
                }
                catch (System.Exception ex)
                {
                    StatusMessage = $"Error loading detail tables: {ex.Message}";
                BaseDetailedData = null;
                ComparedDetailedData = null;
            }
        }

        public void Clear()
        {
            _currentSnapshot = null;
            _comparedSnapshot = null;
            IsCompareMode = false;
            AllTrackedMemoryData = null;
            SelectedNode = null;
            ComparisonRootNodes.Clear();
            SelectedComparisonNode = null;
            BaseDetailedData = null;
            ComparedDetailedData = null;
            BaseSelectedNode = null;
            ComparedSelectedNode = null;
            StatusMessage = "No snapshot loaded";
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

        // 格式化辅助方法
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

        private static string FormatBytesDelta(long bytes)
        {
            var sign = bytes > 0 ? "+" : "";
            var absBytes = (ulong)System.Math.Abs(bytes);
            
            if (absBytes >= 1024UL * 1024 * 1024)
                return $"{sign}{bytes / (1024.0 * 1024 * 1024):F2} GB";
            if (absBytes >= 1024UL * 1024)
                return $"{sign}{bytes / (1024.0 * 1024):F2} MB";
            if (absBytes >= 1024UL)
                return $"{sign}{bytes / 1024.0:F2} KB";
            return $"{sign}{bytes} B";
        }
    }
}

