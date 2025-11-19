using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.UI.Models;

namespace Unity.MemoryProfiler.UI.ViewModels
{
    /// <summary>
    /// Comparison视图ViewModel - 快照对比
    /// Unity官方实现参考：ComparisonViewController.cs
    /// 核心逻辑100%等价Unity官方实现
    /// </summary>
    public partial class ComparisonViewModel : ObservableObject
    {
        private ComparisonModel? _model;
        private CachedSnapshot? _snapshotA;
        private CachedSnapshot? _snapshotB;

        [ObservableProperty]
        private ObservableCollection<ComparisonTreeNode> _items = new();

        [ObservableProperty]
        private string _snapshotAName = "";

        [ObservableProperty]
        private string _snapshotBName = "";

        [ObservableProperty]
        private string _totalSizeAFormatted = "0 B";

        [ObservableProperty]
        private string _totalSizeBFormatted = "0 B";

        [ObservableProperty]
        private string _sizeDeltaFormatted = "0 B";

        [ObservableProperty]
        private string _totalSnapshotSizeAFormatted = "0 B";

        [ObservableProperty]
        private string _totalSnapshotSizeBFormatted = "0 B";

        [ObservableProperty]
        private bool _includeUnchanged = false;

        [ObservableProperty]
        private string _baseDescriptionText = "";

        [ObservableProperty]
        private string _comparedDescriptionText = "";

        [ObservableProperty]
        private ComparisonTreeNode? _selectedItem;

        public ComparisonViewModel()
        {
            ExpandAllCommand = new RelayCommand(ExpandAll);
            CollapseAllCommand = new RelayCommand(CollapseAll);
            RefreshCommand = new RelayCommand(Refresh);
        }

        /// <summary>
        /// 选中项改变时触发
        /// </summary>
        partial void OnSelectedItemChanged(ComparisonTreeNode? value)
        {
            if (value == null)
            {
                BaseDescriptionText = "";
                ComparedDescriptionText = "";
                return;
            }

            // 更新Base和Compared的描述信息
            BaseDescriptionText = $"Base: {FormatBytes(value.TotalSizeInA)} ({value.CountInA:N0} items)";
            ComparedDescriptionText = $"Compared: {FormatBytes(value.TotalSizeInB)} ({value.CountInB:N0} items)";
        }

        /// <summary>
        /// 加载两个快照进行对比
        /// </summary>
        internal void LoadSnapshots(CachedSnapshot snapshotA, CachedSnapshot snapshotB)
        {
            _snapshotA = snapshotA;
            _snapshotB = snapshotB;

            // 设置快照名称（使用ProductName或默认名称）
            SnapshotAName = string.IsNullOrEmpty(snapshotA.MetaData.ProductName) ? "Snapshot A" : snapshotA.MetaData.ProductName;
            SnapshotBName = string.IsNullOrEmpty(snapshotB.MetaData.ProductName) ? "Snapshot B" : snapshotB.MetaData.ProductName;

            // 构建对比Model
            BuildModel();
        }

        /// <summary>
        /// 构建对比Model
        /// </summary>
        private void BuildModel()
        {
            if (_snapshotA == null || _snapshotB == null)
                return;

            // 使用AllTrackedMemoryComparisonModelBuilder构建对比模型
            _model = AllTrackedMemoryComparisonModelBuilder.Build(
                _snapshotA,
                _snapshotB,
                IncludeUnchanged);

            // 更新UI数据
            Items.Clear();
            foreach (var node in _model.RootNodes)
                Items.Add(node);

            // 更新统计信息
            TotalSizeAFormatted = FormatBytes(_model.TotalSizeA);
            TotalSizeBFormatted = FormatBytes(_model.TotalSizeB);
            
            long sizeDelta = (long)_model.TotalSizeB - (long)_model.TotalSizeA;
            SizeDeltaFormatted = FormatSizeDelta(sizeDelta);

            TotalSnapshotSizeAFormatted = FormatBytes(_model.TotalSnapshotSizeA);
            TotalSnapshotSizeBFormatted = FormatBytes(_model.TotalSnapshotSizeB);
        }

        /// <summary>
        /// 展开所有节点
        /// </summary>
        private void ExpandAll()
        {
            // DevExpress TreeListControl会有专门的API，这里保留接口
        }

        /// <summary>
        /// 折叠所有节点
        /// </summary>
        private void CollapseAll()
        {
            // DevExpress TreeListControl会有专门的API，这里保留接口
        }

        /// <summary>
        /// 刷新对比
        /// </summary>
        private void Refresh()
        {
            BuildModel();
        }

        /// <summary>
        /// IncludeUnchanged改变时重新构建
        /// </summary>
        partial void OnIncludeUnchangedChanged(bool value)
        {
            if (_snapshotA != null && _snapshotB != null)
                BuildModel();
        }

        /// <summary>
        /// 格式化字节大小
        /// </summary>
        private static string FormatBytes(ulong bytes)
        {
            if (bytes == 0) return "0 B";

            const ulong KB = 1024;
            const ulong MB = KB * 1024;
            const ulong GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";
            return $"{bytes} B";
        }

        /// <summary>
        /// 格式化大小差异（带符号）
        /// </summary>
        private static string FormatSizeDelta(long sizeDelta)
        {
            if (sizeDelta == 0)
                return "0 B";

            var absSize = (ulong)Math.Abs(sizeDelta);
            var sign = sizeDelta > 0 ? "+" : "-";
            return sign + FormatBytes(absSize);
        }

        // 命令
        public IRelayCommand ExpandAllCommand { get; }
        public IRelayCommand CollapseAllCommand { get; }
        public IRelayCommand RefreshCommand { get; }
    }
}

