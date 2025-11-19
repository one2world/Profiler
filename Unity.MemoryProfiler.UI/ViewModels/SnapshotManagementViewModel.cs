using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Unity.MemoryProfiler.UI.Configuration;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Services;

namespace Unity.MemoryProfiler.UI.ViewModels
{
    /// <summary>
    /// 快照管理ViewModel
    /// 基于Unity官方SnapshotDataService的逻辑
    /// </summary>
    public partial class SnapshotManagementViewModel : ObservableObject
    {
        private readonly string _snapshotDirectory;

        [ObservableProperty]
        private ObservableCollection<SnapshotSessionGroup> _snapshotTree = new();

        /// <summary>
        /// 扁平化的快照树（用于DevExpress TreeListControl绑定）
        /// 使用ParentId/Id实现分层结构
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<SnapshotTreeNode> _treeNodes = new();

        [ObservableProperty]
        private SnapshotFileModel? _selectedSnapshot;

        [ObservableProperty]
        private SnapshotFileModel? _baseSnapshot;

        [ObservableProperty]
        private SnapshotFileModel? _comparedSnapshot;

        [ObservableProperty]
        private bool _compareMode;

        [ObservableProperty]
        private bool _hasSnapshots;

        /// <summary>
        /// 是否有已加载的快照
        /// </summary>
        public bool HasLoadedSnapshots => BaseSnapshot != null || ComparedSnapshot != null;

        /// <summary>
        /// 是否显示单快照卡片（Single Snapshot模式 且 有BaseSnapshot）
        /// </summary>
        public bool ShowSingleSnapshotCard => !CompareMode && BaseSnapshot != null;

        /// <summary>
        /// 是否显示对比快照卡片（Compare Snapshots模式）
        /// </summary>
        public bool ShowCompareSnapshotCards => CompareMode;

        /// <summary>
        /// 快照加载请求事件（通知MainWindow加载快照）
        /// </summary>
        public event EventHandler<string>? SnapshotLoadRequested;

        /// <summary>
        /// 快照对比请求事件（通知MainWindow进行对比）
        /// </summary>
        public event EventHandler<(string, string)>? SnapshotCompareRequested;

        /// <summary>
        /// 快照关闭事件（通知MainWindow关闭并释放快照）
        /// </summary>
        public event EventHandler<SnapshotClosedEventArgs>? SnapshotClosed;

        /// <summary>
        /// 已加载快照变化事件（Unity架构：统一通知UI刷新）
        /// </summary>
        public event EventHandler? LoadedSnapshotsChanged;

        /// <summary>
        /// CompareMode变化事件
        /// </summary>
        public event EventHandler? CompareModeChanged;

        public ICommand RefreshCommand { get; }
        public ICommand RefreshSnapshotsCommand { get; }
        public ICommand LoadSnapshotCommand { get; }
        public ICommand CompareSnapshotsCommand { get; }
        public ICommand ClearComparisonCommand { get; }
        public ICommand SwitchToSingleModeCommand { get; }
        public ICommand SwitchToCompareModeCommand { get; }
        public RelayCommand CloseBaseSnapshotCommand { get; }
        public RelayCommand CloseComparedSnapshotCommand { get; }

        public SnapshotManagementViewModel(string? snapshotDirectory = null)
        {
            // 从配置文件读取快照目录（支持相对路径和绝对路径）
            _snapshotDirectory = snapshotDirectory ?? AppSettings.Instance.GetSnapshotDirectoryFullPath();

            RefreshCommand = new RelayCommand(RefreshSnapshots);
            RefreshSnapshotsCommand = new RelayCommand(RefreshSnapshots);
            LoadSnapshotCommand = new RelayCommand<SnapshotFileModel>(LoadSnapshot, CanLoadSnapshot);
            CompareSnapshotsCommand = new RelayCommand<SnapshotFileModel>(CompareSnapshot, CanCompareSnapshot);
            ClearComparisonCommand = new RelayCommand(ClearComparison, () => CompareMode);
            SwitchToSingleModeCommand = new RelayCommand(SwitchToSingleMode);
            SwitchToCompareModeCommand = new RelayCommand(SwitchToCompareMode);
            CloseBaseSnapshotCommand = new RelayCommand(CloseBaseSnapshot, () => BaseSnapshot != null);
            CloseComparedSnapshotCommand = new RelayCommand(CloseComparedSnapshot, () => ComparedSnapshot != null);

            // 初始化时扫描快照
            RefreshSnapshots();
        }

        /// <summary>
        /// 刷新快照列表
        /// </summary>
        private void RefreshSnapshots()
        {
            Console.WriteLine($"[SnapshotManagement] 扫描快照目录: {_snapshotDirectory}");

            var snapshots = SnapshotScanner.ScanDirectory(_snapshotDirectory);
            var sessionGroups = SnapshotScanner.GroupBySession(snapshots);

            SnapshotTree.Clear();
            TreeNodes.Clear();
            
            int nodeId = 1;
            foreach (var group in sessionGroups)
            {
                SnapshotTree.Add(group);
                
                // 创建Session节点
                int sessionNodeId = nodeId++;
                TreeNodes.Add(new SnapshotTreeNode
                {
                    Id = sessionNodeId,
                    ParentId = null,
                    NodeType = SnapshotNodeType.Session,
                    SessionData = group
                });
                
                // 创建Snapshot子节点
                foreach (var snapshot in group.Snapshots)
                {
                    TreeNodes.Add(new SnapshotTreeNode
                    {
                        Id = nodeId++,
                        ParentId = sessionNodeId,
                        NodeType = SnapshotNodeType.Snapshot,
                        SnapshotData = snapshot
                    });
                }
            }

            HasSnapshots = snapshots.Count > 0;
            Console.WriteLine($"[SnapshotManagement] 发现 {snapshots.Count} 个快照，分为 {sessionGroups.Count} 个Session");

            // 恢复Base/Compared标记
            UpdateLoadedStates();
        }

        /// <summary>
        /// 加载快照
        /// </summary>
        private void LoadSnapshot(SnapshotFileModel? snapshot)
        {
            if (snapshot == null)
                return;

            Console.WriteLine($"[SnapshotManagement] 请求加载快照: {snapshot.Name}");

            if (!CompareMode)
            {
                // 单快照模式：直接加载
                SetBaseSnapshot(snapshot);
                SnapshotLoadRequested?.Invoke(this, snapshot.FullPath);
            }
            else
            {
                // 对比模式：设置Base或Compared
                if (BaseSnapshot == null)
                {
                    SetBaseSnapshot(snapshot);
                    SnapshotLoadRequested?.Invoke(this, snapshot.FullPath);
                }
                else if (ComparedSnapshot == null)
                {
                    SetComparedSnapshot(snapshot);
                    if (BaseSnapshot != null && ComparedSnapshot != null)
                    {
                        SnapshotCompareRequested?.Invoke(this, (BaseSnapshot.FullPath, ComparedSnapshot.FullPath));
                    }
                }
                else
                {
                    // 已有两个快照，切换Base
                    SetBaseSnapshot(snapshot);
                    SnapshotLoadRequested?.Invoke(this, snapshot.FullPath);
                }
            }
        }

        /// <summary>
        /// 对比快照
        /// </summary>
        private void CompareSnapshot(SnapshotFileModel? snapshot)
        {
            if (snapshot == null)
                return;

            Console.WriteLine($"[SnapshotManagement] 请求对比快照: {snapshot.Name}");

            if (BaseSnapshot == null)
            {
                // 第一个快照设为Base
                SetBaseSnapshot(snapshot);
                SnapshotLoadRequested?.Invoke(this, snapshot.FullPath);
            }
            else if (ComparedSnapshot == null || ComparedSnapshot.FullPath != snapshot.FullPath)
            {
                // 第二个快照设为Compared
                SetComparedSnapshot(snapshot);
                CompareMode = true;
                CompareModeChanged?.Invoke(this, EventArgs.Empty);

                if (BaseSnapshot != null && ComparedSnapshot != null)
                {
                    SnapshotCompareRequested?.Invoke(this, (BaseSnapshot.FullPath, ComparedSnapshot.FullPath));
                }
            }
        }

        /// <summary>
        /// 清除对比模式
        /// </summary>
        private void ClearComparison()
        {
            Console.WriteLine($"[SnapshotManagement] 清除对比模式");
            CompareMode = false;
            SetComparedSnapshot(null);
            CompareModeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 设置Base快照
        /// </summary>
        private void SetBaseSnapshot(SnapshotFileModel? snapshot)
        {
            // 清除旧的标记
            if (BaseSnapshot != null)
                BaseSnapshot.IsBase = false;

            BaseSnapshot = snapshot;

            // 设置新的标记
            if (BaseSnapshot != null)
                BaseSnapshot.IsBase = true;

            UpdateLoadedStates();
            OnPropertyChanged(nameof(BaseSnapshot));
            OnPropertyChanged(nameof(HasLoadedSnapshots));
            OnPropertyChanged(nameof(ShowSingleSnapshotCard));
            OnPropertyChanged(nameof(ShowCompareSnapshotCards));
            
            // 刷新Close按钮的可用状态
            CloseBaseSnapshotCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// 设置Compared快照
        /// </summary>
        private void SetComparedSnapshot(SnapshotFileModel? snapshot)
        {
            // 清除旧的标记
            if (ComparedSnapshot != null)
                ComparedSnapshot.IsCompared = false;

            ComparedSnapshot = snapshot;

            // 设置新的标记
            if (ComparedSnapshot != null)
                ComparedSnapshot.IsCompared = true;

            UpdateLoadedStates();
            OnPropertyChanged(nameof(ComparedSnapshot));
            OnPropertyChanged(nameof(HasLoadedSnapshots));
            OnPropertyChanged(nameof(ShowSingleSnapshotCard));
            OnPropertyChanged(nameof(ShowCompareSnapshotCards));
            
            // 刷新Close按钮的可用状态
            CloseComparedSnapshotCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// 更新所有快照的加载状态标记
        /// </summary>
        private void UpdateLoadedStates()
        {
            foreach (var group in SnapshotTree)
            {
                foreach (var snapshot in group.Snapshots)
                {
                    snapshot.IsBase = BaseSnapshot != null && snapshot.FullPath == BaseSnapshot.FullPath;
                    snapshot.IsCompared = ComparedSnapshot != null && snapshot.FullPath == ComparedSnapshot.FullPath;
                }
            }
        }

        /// <summary>
        /// 从外部设置已加载的快照（兼容现有MainWindow逻辑）
        /// </summary>
        public void NotifySnapshotLoaded(string? filePath, bool isCompared = false)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            var snapshot = FindSnapshotByPath(filePath);
            if (snapshot != null)
            {
                if (isCompared)
                    SetComparedSnapshot(snapshot);
                else
                    SetBaseSnapshot(snapshot);
            }
        }

        /// <summary>
        /// 根据路径查找快照
        /// </summary>
        private SnapshotFileModel? FindSnapshotByPath(string fullPath)
        {
            foreach (var group in SnapshotTree)
            {
                var snapshot = group.Snapshots.FirstOrDefault(s => s.FullPath == fullPath);
                if (snapshot != null)
                    return snapshot;
            }
            return null;
        }

        private bool CanLoadSnapshot(SnapshotFileModel? snapshot) => snapshot != null;
        private bool CanCompareSnapshot(SnapshotFileModel? snapshot) => snapshot != null;

        /// <summary>
        /// 切换到单快照模式（Unity的Ribbon Tab切换逻辑）
        /// </summary>
        private void SwitchToSingleMode()
        {
            if (!CompareMode)
                return;

            Console.WriteLine($"[SnapshotManagement] 切换到Single Snapshot模式");
            CompareMode = false;

            // 如果有Compared快照，触发关闭事件
            if (ComparedSnapshot != null)
            {
                var path = ComparedSnapshot.FullPath;
                SetComparedSnapshot(null);
                SnapshotClosed?.Invoke(this, new SnapshotClosedEventArgs(path, true));
            }

            OnPropertyChanged(nameof(ShowSingleSnapshotCard));
            OnPropertyChanged(nameof(ShowCompareSnapshotCards));
            CompareModeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 切换到对比模式（Unity的Ribbon Tab切换逻辑）
        /// </summary>
        private void SwitchToCompareMode()
        {
            if (CompareMode)
                return;

            Console.WriteLine($"[SnapshotManagement] 切换到Compare Snapshots模式");
            CompareMode = true;

            OnPropertyChanged(nameof(ShowSingleSnapshotCard));
            OnPropertyChanged(nameof(ShowCompareSnapshotCards));
            CompareModeChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 关闭Base快照（Unity的Close按钮逻辑）
        /// </summary>
        private void CloseBaseSnapshot()
        {
            if (BaseSnapshot == null)
                return;

            Console.WriteLine($"[SnapshotManagement] 关闭Base快照: {BaseSnapshot.Name}");
            Unload(BaseSnapshot.FullPath, isBaseSnapshot: true);
        }

        /// <summary>
        /// 关闭Compared快照（Unity的Close按钮逻辑）
        /// </summary>
        private void CloseComparedSnapshot()
        {
            if (ComparedSnapshot == null)
                return;

            Console.WriteLine($"[SnapshotManagement] 关闭Compared快照: {ComparedSnapshot.Name}");
            Unload(ComparedSnapshot.FullPath, isBaseSnapshot: false);
        }

        /// <summary>
        /// 卸载快照（Unity架构：统一管理卸载逻辑）
        /// </summary>
        public void Unload(string filePath, bool isBaseSnapshot)
        {
            Console.WriteLine($"[SnapshotManagement] Unload快照: {filePath}, IsBase={isBaseSnapshot}");

            if (isBaseSnapshot && BaseSnapshot?.FullPath == filePath)
            {
                // 🔑 Unity智能交换逻辑：关闭Base时，Compared提升为Base
                var originalBase = BaseSnapshot;
                SetBaseSnapshot(ComparedSnapshot);
                SetComparedSnapshot(null);

                Console.WriteLine($"[SnapshotManagement] Base快照已卸载，Compared提升为Base");

                // 触发关闭事件，通知MainWindow释放内存
                SnapshotClosed?.Invoke(this, new SnapshotClosedEventArgs(filePath, false));
            }
            else if (!isBaseSnapshot && ComparedSnapshot?.FullPath == filePath)
            {
                // 关闭Compared快照
                SetComparedSnapshot(null);

                Console.WriteLine($"[SnapshotManagement] Compared快照已卸载");

                // 触发关闭事件，通知MainWindow释放内存
                SnapshotClosed?.Invoke(this, new SnapshotClosedEventArgs(filePath, true));
            }
            else
            {
                Console.WriteLine($"[SnapshotManagement] ⚠️ 尝试卸载未加载的快照: {filePath}");
                return;
            }

            // 触发LoadedSnapshotsChanged事件（Unity架构）
            LoadedSnapshotsChanged?.Invoke(this, EventArgs.Empty);

            OnPropertyChanged(nameof(ShowSingleSnapshotCard));
            OnPropertyChanged(nameof(ShowCompareSnapshotCards));
        }
    }

    /// <summary>
    /// 快照关闭事件参数
    /// </summary>
    public class SnapshotClosedEventArgs : EventArgs
    {
        public string FilePath { get; }
        public bool IsCompared { get; }

        public SnapshotClosedEventArgs(string filePath, bool isCompared)
        {
            FilePath = filePath;
            IsCompared = isCompared;
        }
    }
}

