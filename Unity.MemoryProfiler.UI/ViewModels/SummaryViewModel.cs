using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Services;

namespace Unity.MemoryProfiler.UI.ViewModels
{
    public class SummaryViewModel : INotifyPropertyChanged
    {
        const string MemoryUsageTitle = "Memory Usage On Device";
        const string MemoryUsageDescription = "Displays how much memory you have allocated to the system, and how much of that memory is currently resident on device.";
        const string AllocatedDistributionTitle = "Allocated Memory Distribution";
        const string AllocatedDistributionDescription = "Displays how your allocated memory is distributed across memory areas.";
        const string ManagedHeapTitle = "Managed Heap Utilization";
        const string ManagedHeapDescription = "Displays a breakdown of the memory that Unity's Scripting VM manages.";
        const string UnityObjectsTitle = "Top Unity Objects Categories";
        const string UnityObjectsDescription = "Displays which types of Unity Objects use the most memory in the snapshot.";
        const string DocumentationUrl = "https://docs.unity3d.com/Packages/com.unity.memoryprofiler@1.1/manual/index.html";

        private SummaryData? _summaryData;
        private string _statusMessage = "No snapshot loaded";
        private CachedSnapshot? _currentSnapshot;
        private CachedSnapshot? _comparedSnapshot;
        private bool _isNormalized;
        private SummarySelectionNode? _selectedNode;
        private SummarySelectionKind? _lastSelectionKind;
        private MemoryCategory? _lastMemoryCategory;
        private string? _lastManagedSegmentKey;
        private UnityObjectCategory? _lastUnityCategory;
        private int _selectionIdCounter;
        private readonly List<SummaryIssue> _issues = new();
        private MemoryCategory? _selectedMemoryCategory;

        public SummaryViewModel()
        {
            ShowMemoryUsageDetailsCommand = new RelayCommand(() => SelectMemoryUsageDetails(updateContext: true));
            ShowMemoryCategoryDetailsCommand = new RelayCommand<MemoryCategory?>(category =>
            {
                if (category != null)
                    SelectMemoryDistributionCategory(category, updateContext: true);
            });
            ShowManagedSegmentDetailsCommand = new RelayCommand<string?>(segmentKey =>
            {
                if (!string.IsNullOrEmpty(segmentKey))
                    SelectManagedSegment(segmentKey!, updateContext: true);
            });
            ShowUnityCategoryDetailsCommand = new RelayCommand<UnityObjectCategory?>(category =>
            {
                if (category != null)
                    SelectUnityCategory(category, updateContext: true);
            });
            InspectAllOfMemoryCommand = new RelayCommand(() => RaiseInspectRequested(SummaryInspectTarget.AllOfMemory));
            InspectUnityObjectsCommand = new RelayCommand(() => RaiseInspectRequested(SummaryInspectTarget.UnityObjects));
            InspectManagedCommand = new RelayCommand(() => RaiseInspectRequested(SummaryInspectTarget.AllOfMemory));
        }

        public SummaryData? SummaryData
        {
            get => _summaryData;
            private set => SetProperty(ref _summaryData, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        internal CachedSnapshot? CurrentSnapshot
        {
            get => _currentSnapshot;
            private set => SetProperty(ref _currentSnapshot, value);
        }

        internal CachedSnapshot? ComparedSnapshot
        {
            get => _comparedSnapshot;
            private set
        {
                if (SetProperty(ref _comparedSnapshot, value))
                {
                    OnPropertyChanged(nameof(ShowNormalizedToggle));
                    OnPropertyChanged(nameof(NormalizedToggleEnabled));
                }
            }
        }

        public IReadOnlyList<SummaryIssue> Issues => _issues;

        public bool HasIssues => _issues.Count > 0;

        public bool IsNormalized
        {
            get => _isNormalized;
            set => SetNormalized(value, refreshSelection: true);
        }

        public string NormalizedLabel => IsNormalized ? "Normalized" : "Actual Values";

        public bool ShowNormalizedToggle => ComparedSnapshot != null;

        public bool NormalizedToggleEnabled => ComparedSnapshot != null;

        internal SummarySelectionNode? SelectedNode
        {
            get => _selectedNode;
            private set => SetProperty(ref _selectedNode, value);
        }

        /// <summary>
        /// 选中的内存类别（用于 DevExpress GridControl 绑定）
        /// 参考: Unity 的 GenericMemorySummaryViewController.RowClicked
        /// </summary>
        public MemoryCategory? SelectedMemoryCategory
        {
            get => _selectedMemoryCategory;
            set
            {
                if (SetProperty(ref _selectedMemoryCategory, value) && value != null)
                {
                    SelectMemoryDistributionCategory(value, updateContext: true);
                }
            }
        }

        private ManagedHeapRow? _selectedManagedHeapRow;
        /// <summary>
        /// 选中的托管堆行（用于 DevExpress GridControl 绑定）
        /// </summary>
        public ManagedHeapRow? SelectedManagedHeapRow
        {
            get => _selectedManagedHeapRow;
            set
            {
                if (SetProperty(ref _selectedManagedHeapRow, value) && value != null)
                {
                    SelectManagedHeapRow(value);
                }
            }
        }

        private UnityObjectCategory? _selectedUnityObjectCategory;
        /// <summary>
        /// 选中的 Unity 对象类别（用于 DevExpress GridControl 绑定）
        /// </summary>
        public UnityObjectCategory? SelectedUnityObjectCategory
        {
            get => _selectedUnityObjectCategory;
            set
            {
                if (SetProperty(ref _selectedUnityObjectCategory, value) && value != null)
                {
                    SelectUnityObjectCategory(value);
                }
            }
        }

        private MemoryUsageRow? _selectedMemoryUsageRow;
        /// <summary>
        /// 选中的内存使用行（用于 DevExpress GridControl 绑定）
        /// </summary>
        public MemoryUsageRow? SelectedMemoryUsageRow
        {
            get => _selectedMemoryUsageRow;
            set
            {
                if (SetProperty(ref _selectedMemoryUsageRow, value) && value != null)
                {
                    SelectMemoryUsageRow(value);
                }
            }
        }

        public IRelayCommand ShowMemoryUsageDetailsCommand { get; }
        public IRelayCommand<MemoryCategory?> ShowMemoryCategoryDetailsCommand { get; }
        public IRelayCommand<string?> ShowManagedSegmentDetailsCommand { get; }
        public IRelayCommand<UnityObjectCategory?> ShowUnityCategoryDetailsCommand { get; }
        public IRelayCommand InspectAllOfMemoryCommand { get; }
        public IRelayCommand InspectUnityObjectsCommand { get; }
        public IRelayCommand InspectManagedCommand { get; }

        public event EventHandler<SummaryInspectRequestEventArgs>? InspectRequested;

        internal void LoadSnapshot(CachedSnapshot snapshot)
        {
            CurrentSnapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            ComparedSnapshot = null;

                var builder = new SummaryDataBuilder(snapshot);
                SummaryData = builder.Build();

            BuildIssues(snapshot, null);

                StatusMessage = "Snapshot loaded successfully";
            _lastSelectionKind = null;
            _lastMemoryCategory = null;
            _lastManagedSegmentKey = null;
            _lastUnityCategory = null;
            SelectedNode = null;
            SetNormalized(false, refreshSelection: false);
        }

        internal void CompareSnapshots(CachedSnapshot baseSnapshot, CachedSnapshot comparedSnapshot)
        {
            CurrentSnapshot = baseSnapshot ?? throw new ArgumentNullException(nameof(baseSnapshot));
            ComparedSnapshot = comparedSnapshot ?? throw new ArgumentNullException(nameof(comparedSnapshot));

            // 构建对比数据（传递两个 snapshot）
            var builder = new SummaryDataBuilder(baseSnapshot, comparedSnapshot);
            SummaryData = builder.Build();

            BuildIssues(baseSnapshot, comparedSnapshot);

            StatusMessage = "Comparison loaded successfully";
            _lastSelectionKind = null;
            _lastMemoryCategory = null;
            _lastManagedSegmentKey = null;
            _lastUnityCategory = null;
            SelectedNode = null;
            SetNormalized(false, refreshSelection: false);
        }

        public void Clear()
        {
            SummaryData = null;
            CurrentSnapshot = null;
            ComparedSnapshot = null;
            _issues.Clear();
            OnPropertyChanged(nameof(Issues));
            OnPropertyChanged(nameof(HasIssues));
            StatusMessage = "No snapshot loaded";
            _lastSelectionKind = null;
            _lastMemoryCategory = null;
            _lastManagedSegmentKey = null;
            _lastUnityCategory = null;
            SelectedNode = null;
            SetNormalized(false, refreshSelection: false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        void BuildIssues(CachedSnapshot baseSnapshot, CachedSnapshot? comparedSnapshot)
        {
            _issues.Clear();

            try
            {
                var builder = new SnapshotIssuesModelBuilder(baseSnapshot, comparedSnapshot);
                var model = builder.Build();
                if (model != null)
                {
                    foreach (var issue in model.Issues)
                    {
                        _issues.Add(new SummaryIssue(issue.Summary, issue.Details, ConvertLevel(issue.IssueLevel)));
                    }
                }
            }
            catch
            {
                // Ignore issue builder failures, Issues list remains empty
            }

            OnPropertyChanged(nameof(Issues));
            OnPropertyChanged(nameof(HasIssues));
        }

        void SetNormalized(bool value, bool refreshSelection)
        {
            if (_isNormalized == value)
                return;

            _isNormalized = value;
            OnPropertyChanged(nameof(IsNormalized));
            OnPropertyChanged(nameof(NormalizedLabel));

            if (refreshSelection)
                RefreshSelectionFromContext();
        }

        void RefreshSelectionFromContext()
        {
            if (_lastSelectionKind == null)
                return;

            switch (_lastSelectionKind)
            {
                case SummarySelectionKind.MemoryUsage:
                    SelectMemoryUsageDetails(updateContext: false);
                    break;
                case SummarySelectionKind.MemoryDistributionCategory:
                    if (_lastMemoryCategory != null)
                        SelectMemoryDistributionCategory(_lastMemoryCategory, updateContext: false);
                    break;
                case SummarySelectionKind.ManagedHeapSegment:
                    if (!string.IsNullOrEmpty(_lastManagedSegmentKey))
                        SelectManagedSegment(_lastManagedSegmentKey!, updateContext: false);
                    break;
                case SummarySelectionKind.UnityObjectCategory:
                    if (_lastUnityCategory != null)
                        SelectUnityCategory(_lastUnityCategory, updateContext: false);
                    break;
            }
        }

        void SelectMemoryUsageDetails(bool updateContext)
        {
            if (SummaryData == null)
                return;

            if (updateContext)
            {
                _lastSelectionKind = SummarySelectionKind.MemoryUsage;
                _lastMemoryCategory = null;
                _lastManagedSegmentKey = null;
                _lastUnityCategory = null;
            }

            var metrics = new List<SummarySelectionMetric>
            {
                new("Total Resident", FormatMegabytes(SummaryData.MemoryUsage.TotalResidentMB), "Resident memory currently on device", selectable: true),
                new("Total Allocated", FormatMegabytes(SummaryData.MemoryUsage.TotalAllocatedMB), "Total memory allocated by the application", selectable: true),
                new("Resident Percentage", FormatPercentage(SummaryData.MemoryUsage.ResidentPercentage))
            };
            metrics.Add(new SummarySelectionMetric("Normalized", IsNormalized ? "Enabled" : "Disabled"));

            SelectedNode = new SummarySelectionNode(GetNextSelectionId(), SummarySelectionKind.MemoryUsage, MemoryUsageTitle, MemoryUsageDescription, metrics, DocumentationUrl);
        }

        void SelectMemoryDistributionCategory(MemoryCategory category, bool updateContext)
        {
            if (SummaryData == null)
                return;

            if (updateContext)
            {
                _lastSelectionKind = SummarySelectionKind.MemoryDistributionCategory;
                _lastMemoryCategory = category;
                _lastManagedSegmentKey = null;
                _lastUnityCategory = null;
            }

            // 参考: Unity 的 GenericMemorySummaryViewController.MakeSelection()
            // 使用 category.Description 和 category.DocumentationUrl
            var metrics = new List<SummarySelectionMetric>
            {
                new("Allocated", FormatMegabytes(category.SizeMB), selectable: true),
                new("Share", FormatPercentage(category.Percentage))
            };
            
            // 对比模式显示 A/B/Diff 数据
            if (ComparedSnapshot != null)
            {
                metrics.Add(new SummarySelectionMetric("Allocated (B)", FormatMegabytes(category.SizeMB_B), selectable: true));
                metrics.Add(new SummarySelectionMetric("Difference", category.FormattedDiff));
            }
            
            metrics.Add(new SummarySelectionMetric("Normalized", IsNormalized ? "Enabled" : "Disabled"));

            SelectedNode = new SummarySelectionNode(
                GetNextSelectionId(), 
                SummarySelectionKind.MemoryDistributionCategory, 
                category.Name, 
                category.Description, // 使用 category.Description 而不是 AllocatedDistributionDescription
                metrics, 
                category.DocumentationUrl); // 使用 category.DocumentationUrl 而不是常量
        }

        void SelectManagedSegment(string segmentKey, bool updateContext)
        {
            if (SummaryData == null)
                return;

            if (updateContext)
            {
                _lastSelectionKind = SummarySelectionKind.ManagedHeapSegment;
                _lastMemoryCategory = null;
                _lastManagedSegmentKey = segmentKey;
                _lastUnityCategory = null;
            }

            var heap = SummaryData.HeapUtilization;
            string title;
            double value;
            string tooltip;

            switch (segmentKey)
            {
                case ManagedSegmentVirtualMachine:
                    title = "Virtual Machine";
                    value = heap.VirtualMachineMB;
                    tooltip = "Managed memory used by the scripting virtual machine.";
                    break;
                case ManagedSegmentEmptyHeap:
                    title = "Empty Heap Space";
                    value = heap.EmptyHeapSpaceMB;
                    tooltip = "Reserved managed heap space currently unused.";
                    break;
                case ManagedSegmentObjects:
                    title = "Objects";
                    value = heap.ObjectsMB;
                    tooltip = "Managed objects currently allocated.";
                    break;
                default:
                    return;
            }

            var metrics = new List<SummarySelectionMetric>
            {
                new("Size", FormatMegabytes(value), tooltip, selectable: true),
                new("Total Managed Heap", FormatMegabytes(heap.TotalMB), selectable: true)
            };
            metrics.Add(new SummarySelectionMetric("Normalized", IsNormalized ? "Enabled" : "Disabled"));

            SelectedNode = new SummarySelectionNode(GetNextSelectionId(), SummarySelectionKind.ManagedHeapSegment, title, ManagedHeapDescription, metrics, DocumentationUrl);
        }

        void SelectUnityCategory(UnityObjectCategory category, bool updateContext)
        {
            if (SummaryData == null)
                return;

            if (updateContext)
            {
                _lastSelectionKind = SummarySelectionKind.UnityObjectCategory;
                _lastMemoryCategory = null;
                _lastManagedSegmentKey = null;
                _lastUnityCategory = category;
            }

            var metrics = new List<SummarySelectionMetric>
            {
                new("Size", FormatMegabytes(category.SizeMB), selectable: true),
                new("Share", FormatPercentage(category.Percentage))
            };
            
            // 对比模式显示 A/B/Diff 数据
            if (ComparedSnapshot != null)
            {
                metrics.Add(new SummarySelectionMetric("Size (B)", FormatMegabytes(category.SizeMB_B), selectable: true));
                metrics.Add(new SummarySelectionMetric("Difference", category.FormattedDiff));
            }
            
            metrics.Add(new SummarySelectionMetric("Normalized", IsNormalized ? "Enabled" : "Disabled"));

            SelectedNode = new SummarySelectionNode(GetNextSelectionId(), SummarySelectionKind.UnityObjectCategory, category.Name, UnityObjectsDescription, metrics, DocumentationUrl);
        }

        /// <summary>
        /// 选择托管堆行（对比模式）
        /// </summary>
        void SelectManagedHeapRow(ManagedHeapRow row)
        {
            if (SummaryData == null)
                return;

            var metrics = new List<SummarySelectionMetric>
            {
                new("Allocated (A)", row.FormattedSize, selectable: true),
                new("Allocated (B)", row.FormattedSize_B, selectable: true),
                new("Difference", row.FormattedDiff)
            };

            string description = row.Name switch
            {
                "Virtual Machine" => "Managed memory used by the scripting virtual machine.",
                "Empty Heap Space" => "Reserved managed heap space currently unused.",
                "Objects" => "Managed objects currently allocated.",
                _ => ManagedHeapDescription
            };

            SelectedNode = new SummarySelectionNode(
                GetNextSelectionId(), 
                SummarySelectionKind.ManagedHeapSegment, 
                row.Name, 
                description, 
                metrics, 
                DocumentationUrl);
        }

        /// <summary>
        /// 选择 Unity 对象类别（对比模式）
        /// </summary>
        void SelectUnityObjectCategory(UnityObjectCategory category)
        {
            if (SummaryData == null)
                return;

            var metrics = new List<SummarySelectionMetric>
            {
                new("Size (A)", FormatMegabytes(category.SizeMB), selectable: true),
                new("Share (A)", FormatPercentage(category.Percentage)),
                new("Size (B)", FormatMegabytes(category.SizeMB_B), selectable: true),
                new("Share (B)", FormatPercentage(category.Percentage_B)),
                new("Difference", category.FormattedDiff)
            };

            SelectedNode = new SummarySelectionNode(
                GetNextSelectionId(), 
                SummarySelectionKind.UnityObjectCategory, 
                category.Name, 
                UnityObjectsDescription, 
                metrics, 
                DocumentationUrl);
        }

        /// <summary>
        /// 选择内存使用行（对比模式）
        /// </summary>
        void SelectMemoryUsageRow(MemoryUsageRow row)
        {
            if (SummaryData == null)
                return;

            var metrics = new List<SummarySelectionMetric>
            {
                new("Size (A)", row.FormattedSize, selectable: true),
                new("Size (B)", row.FormattedSize_B, selectable: true),
                new("Difference", row.FormattedDiff)
            };

            string description = row.Name switch
            {
                "Total Resident" => "Resident memory is memory that is currently in physical RAM.",
                "Total Allocated" => "Allocated (or Committed) memory is memory that the OS has allocated for the application.",
                _ => MemoryUsageDescription
            };

            SelectedNode = new SummarySelectionNode(
                GetNextSelectionId(), 
                SummarySelectionKind.MemoryUsage, 
                row.Name, 
                description, 
                metrics, 
                DocumentationUrl);
        }

        void RaiseInspectRequested(SummaryInspectTarget target)
        {
            InspectRequested?.Invoke(this, new SummaryInspectRequestEventArgs(target));
        }

        int GetNextSelectionId() => ++_selectionIdCounter;

        static string FormatMegabytes(double megaBytes) => $"{megaBytes:F2} MB";

        static string FormatPercentage(double percentage) => $"{percentage * 100:F2}%";

        static SummaryIssueLevel ConvertLevel(SnapshotIssuesModel.IssueLevel level) => level switch
        {
            SnapshotIssuesModel.IssueLevel.Error => SummaryIssueLevel.Error,
            SnapshotIssuesModel.IssueLevel.Warning => SummaryIssueLevel.Warning,
            _ => SummaryIssueLevel.Info
        };

        private void SelectMemoryUsageDetails() => SelectMemoryUsageDetails(updateContext: true);

        private void SelectUnityCategory(UnityObjectCategory category) => SelectUnityCategory(category, updateContext: true);

        public const string ManagedSegmentVirtualMachine = "VM";
        public const string ManagedSegmentEmptyHeap = "EMPTY";
        public const string ManagedSegmentObjects = "OBJECTS";

        public readonly record struct SummaryIssue(string Summary, string Details, SummaryIssueLevel Level);

        public enum SummaryIssueLevel
        {
            Info,
            Warning,
            Error
        }

        public sealed class SummaryInspectRequestEventArgs : EventArgs
        {
            public SummaryInspectRequestEventArgs(SummaryInspectTarget target)
            {
                Target = target;
            }

            public SummaryInspectTarget Target { get; }
        }

        public enum SummaryInspectTarget
        {
            AllOfMemory,
            UnityObjects
        }
    }
}

