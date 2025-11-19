using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.ModelBuilders.Comparison;
using Unity.MemoryProfiler.UI.Models.Comparison;

namespace Unity.MemoryProfiler.UI.ViewModels.Comparison
{
    /// <summary>
    /// AllTrackedMemory对比视图模型
    /// 等价于Unity的AllTrackedMemoryComparisonViewController
    /// 注意：此类已过时，将在阶段8删除
    /// </summary>
    internal class AllTrackedMemoryComparisonViewModel : INotifyPropertyChanged
    {
        private CachedSnapshot _snapshotA;
        private CachedSnapshot _snapshotB;
        private ComparisonTableModel _model;
        private ObservableCollection<ComparisonTreeNode> _comparisonRootNodes;
        private ComparisonTreeNode _selectedNode;
        private bool _showUnchanged = false;
        private string _searchText = string.Empty;
        private bool _isBusy = false;
        private string _statusMessage = string.Empty;
        private string _descriptionText = string.Empty;

        // Base和Compared视图的描述
        private string _baseDescriptionText = string.Empty;
        private string _comparedDescriptionText = string.Empty;

        internal AllTrackedMemoryComparisonViewModel(CachedSnapshot snapshotA, CachedSnapshot snapshotB)
        {
            _snapshotA = snapshotA ?? throw new ArgumentNullException(nameof(snapshotA));
            _snapshotB = snapshotB ?? throw new ArgumentNullException(nameof(snapshotB));

            _comparisonRootNodes = new ObservableCollection<ComparisonTreeNode>();

            // 命令
            ShowUnchangedCommand = new RelayCommand(() =>
            {
                ShowUnchanged = !ShowUnchanged;
                _ = BuildModelAsync();
            });

            SearchCommand = new RelayCommand(() =>
            {
                _ = BuildModelAsync();
            });

            RefreshCommand = new RelayCommand(async () =>
            {
                await BuildModelAsync();
            });

            // 设置描述文本
            var snapshotAName = string.IsNullOrEmpty(_snapshotA.MetaData.ProductName) ? "Snapshot A" : _snapshotA.MetaData.ProductName;
            var snapshotBName = string.IsNullOrEmpty(_snapshotB.MetaData.ProductName) ? "Snapshot B" : _snapshotB.MetaData.ProductName;
            _descriptionText = $"Comparing \"{snapshotAName}\" (Base) with \"{snapshotBName}\" (Compared)";

            // 初始加载
            _ = BuildModelAsync();
        }

        #region 属性

        /// <summary>
        /// 对比树根节点集合
        /// </summary>
        public ObservableCollection<ComparisonTreeNode> ComparisonRootNodes
        {
            get => _comparisonRootNodes;
            private set => SetProperty(ref _comparisonRootNodes, value);
        }

        /// <summary>
        /// 选中的节点
        /// </summary>
        public ComparisonTreeNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    OnNodeSelected(value);
                }
            }
        }

        /// <summary>
        /// 是否显示未变化的项
        /// </summary>
        public bool ShowUnchanged
        {
            get => _showUnchanged;
            set => SetProperty(ref _showUnchanged, value);
        }

        /// <summary>
        /// 搜索文本
        /// </summary>
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        /// <summary>
        /// 是否正在加载
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
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
        /// 描述文本
        /// </summary>
        public string DescriptionText
        {
            get => _descriptionText;
            private set => SetProperty(ref _descriptionText, value);
        }

        /// <summary>
        /// Base快照的总大小文本
        /// </summary>
        public string TotalSizeAText { get; private set; } = string.Empty;

        /// <summary>
        /// Compared快照的总大小文本
        /// </summary>
        public string TotalSizeBText { get; private set; } = string.Empty;

        /// <summary>
        /// Base快照在表中的大小（用于进度条）
        /// </summary>
        public ulong TotalSizeA { get; private set; }

        /// <summary>
        /// Compared快照在表中的大小（用于进度条）
        /// </summary>
        public ulong TotalSizeB { get; private set; }

        /// <summary>
        /// Base快照的总内存大小
        /// </summary>
        public ulong TotalSnapshotSizeA { get; private set; }

        /// <summary>
        /// Compared快照的总内存大小
        /// </summary>
        public ulong TotalSnapshotSizeB { get; private set; }

        /// <summary>
        /// 最大值（用于进度条比例）
        /// </summary>
        public ulong MaxValue => Math.Max(TotalSnapshotSizeA, TotalSnapshotSizeB);

        /// <summary>
        /// Base视图描述文本
        /// </summary>
        public string BaseDescriptionText
        {
            get => _baseDescriptionText;
            private set => SetProperty(ref _baseDescriptionText, value);
        }

        /// <summary>
        /// Compared视图描述文本
        /// </summary>
        public string ComparedDescriptionText
        {
            get => _comparedDescriptionText;
            private set => SetProperty(ref _comparedDescriptionText, value);
        }

        #endregion

        #region 命令

        public IRelayCommand ShowUnchangedCommand { get; }
        public IRelayCommand SearchCommand { get; }
        public IRelayCommand RefreshCommand { get; }

        #endregion

        #region 方法

        /// <summary>
        /// 异步构建对比模型
        /// </summary>
        public async Task BuildModelAsync()
        {
            IsBusy = true;
            StatusMessage = "Building comparison model...";

            try
            {
                // 在后台线程构建模型
                var model = await Task.Run(() =>
                {
                    return AllTrackedMemoryComparisonTableModelBuilder.Build(
                        _snapshotA,
                        _snapshotB,
                        _showUnchanged);
                });

                // 更新UI（在UI线程）
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _model = model;
                    ComparisonRootNodes = new ObservableCollection<ComparisonTreeNode>(model.RootNodes);

                    // 更新总大小信息
                    TotalSizeA = model.TotalSizeA;
                    TotalSizeB = model.TotalSizeB;
                    TotalSnapshotSizeA = model.TotalSnapshotSizeA;
                    TotalSnapshotSizeB = model.TotalSnapshotSizeB;

                    TotalSizeAText = FormatBytes((long)model.TotalSizeA);
                    TotalSizeBText = FormatBytes((long)model.TotalSizeB);

                    OnPropertyChanged(nameof(TotalSizeA));
                    OnPropertyChanged(nameof(TotalSizeB));
                    OnPropertyChanged(nameof(TotalSnapshotSizeA));
                    OnPropertyChanged(nameof(TotalSnapshotSizeB));
                    OnPropertyChanged(nameof(MaxValue));
                    OnPropertyChanged(nameof(TotalSizeAText));
                    OnPropertyChanged(nameof(TotalSizeBText));

                    var itemCount = CountTotalItems(model.RootNodes);
                    StatusMessage = $"Comparison model built: {itemCount} items";

                    // 清空Base和Compared的描述（等待用户选择）
                    BaseDescriptionText = string.Empty;
                    ComparedDescriptionText = string.Empty;
                });
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    StatusMessage = $"Error building comparison model: {ex.Message}";
                    ComparisonRootNodes = new ObservableCollection<ComparisonTreeNode>();
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 节点选中事件处理
        /// </summary>
        private void OnNodeSelected(ComparisonTreeNode node)
        {
            if (node == null)
            {
                BaseDescriptionText = string.Empty;
                ComparedDescriptionText = string.Empty;
                return;
            }

            // 根据选中节点更新Base和Compared的描述
            // 在Unity中，这里会过滤Base和Compared的详细表
            // 简化实现：仅显示描述信息
            var data = node.Data;
            
            BaseDescriptionText = $"Base: {FormatBytes((long)data.TotalSizeInA)} ({data.CountInA:N0} items)";
            ComparedDescriptionText = $"Compared: {FormatBytes((long)data.TotalSizeInB)} ({data.CountInB:N0} items)";
        }

        /// <summary>
        /// 清空ViewModel
        /// </summary>
        public void Clear()
        {
            ComparisonRootNodes = new ObservableCollection<ComparisonTreeNode>();
            SelectedNode = null;
            StatusMessage = "No snapshots loaded";
            BaseDescriptionText = string.Empty;
            ComparedDescriptionText = string.Empty;
        }

        /// <summary>
        /// 统计总项数（递归）
        /// </summary>
        private int CountTotalItems(System.Collections.Generic.List<ComparisonTreeNode> nodes)
        {
            int count = nodes.Count;
            foreach (var node in nodes)
            {
                if (node.HasChildren)
                    count += CountTotalItems(node.Children);
            }
            return count;
        }

        /// <summary>
        /// 格式化字节数
        /// </summary>
        private string FormatBytes(long bytes)
        {
            if (bytes < 0)
            {
                return $"-{FormatBytes(-bytes)}";
            }

            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;

            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";
            return $"{bytes} B";
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}

