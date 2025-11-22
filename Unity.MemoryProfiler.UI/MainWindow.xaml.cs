using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Format;
using Unity.MemoryProfiler.Editor.Format.QueriedSnapshot;
using Unity.MemoryProfiler.UI.Services.SelectionDetails;
using Unity.MemoryProfiler.UI.ViewModels;

namespace Unity.MemoryProfiler.UI;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window, INotifyPropertyChanged
{
    private string _snapshotPath = "";
    
    // 快照所有权管理（方案 B：MainWindow 拥有所有快照的所有权）
    private CachedSnapshot? _currentSnapshot;        // 单快照模式
    private CachedSnapshot? _comparedSnapshotA;      // 对比模式 - Base (A)
    private CachedSnapshot? _comparedSnapshotB;      // 对比模式 - Compared (B)
    
    private bool _isLoading;
    private double _loadingProgress;
    private string _loadingStatusText = "";
    private string _loadingElapsedTime = "";
    private CancellationTokenSource? _loadingCancellationTokenSource;
    private Stopwatch? _loadingStopwatch;
    private readonly SelectionDetailsService _selectionDetailsService;

    public string SnapshotPath
    {
        get => _snapshotPath;
        set => SetProperty(ref _snapshotPath, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public double LoadingProgress
    {
        get => _loadingProgress;
        set => SetProperty(ref _loadingProgress, value);
    }

    public string LoadingStatusText
    {
        get => _loadingStatusText;
        set => SetProperty(ref _loadingStatusText, value);
    }

    public string LoadingElapsedTime
    {
        get => _loadingElapsedTime;
        set => SetProperty(ref _loadingElapsedTime, value);
    }

    public SummaryViewModel SummaryViewModel { get; }
    public UnityObjectsViewModel UnityObjectsViewModel { get; }
    public AllTrackedMemoryViewModel AllTrackedMemoryViewModel { get; }
    public ManagedObjectsViewModel ManagedObjectsViewModel { get; }
    public ComparisonViewModel ComparisonViewModel { get; }
    public SnapshotManagementViewModel SnapshotManagementViewModel { get; }

    public IRelayCommand LoadSnapshotCommand { get; }
    public IRelayCommand BrowseSnapshotCommand { get; }
    public IRelayCommand CancelLoadingCommand { get; }
    public IRelayCommand CompareSnapshotsCommand { get; }
    public IRelayCommand CloseSnapshotCommand { get; }

    public MainWindow() : this(null, null)
    {
    }

    public MainWindow(string? snapshotPath, string? compareSnapshotPath = null)
    {
        InitializeComponent();
        DataContext = this;

        SummaryViewModel = new SummaryViewModel();
        UnityObjectsViewModel = new UnityObjectsViewModel();
        AllTrackedMemoryViewModel = new AllTrackedMemoryViewModel();
        ManagedObjectsViewModel = new ManagedObjectsViewModel();
        ComparisonViewModel = new ComparisonViewModel();
        SnapshotManagementViewModel = new SnapshotManagementViewModel();

        _selectionDetailsService = new SelectionDetailsService(new ISelectionDetailsPresenter[]
        {
            new UnityObjectsSelectionDetailsPresenter(),
            new AllTrackedMemorySelectionDetailsPresenter(),
            new ManagedObjectsSelectionDetailsPresenter(),
            new SummarySelectionDetailsPresenter()
        });
        
        SummaryViewModel.PropertyChanged += OnViewModelPropertyChanged;
        ManagedObjectsViewModel.PropertyChanged += OnViewModelPropertyChanged;
        SummaryViewModel.InspectRequested += OnSummaryInspectRequested;
        UnityObjectsViewModel.PropertyChanged += OnViewModelPropertyChanged;
        AllTrackedMemoryViewModel.PropertyChanged += OnViewModelPropertyChanged;
        
        // 订阅快照管理事件（Unity架构）
        SnapshotManagementViewModel.SnapshotLoadRequested += OnSnapshotLoadRequested;
        SnapshotManagementViewModel.SnapshotCompareRequested += OnSnapshotCompareRequested;
        SnapshotManagementViewModel.SnapshotClosed += OnSnapshotClosed;
        SnapshotManagementViewModel.LoadedSnapshotsChanged += OnLoadedSnapshotsChanged;
        
        LoadSnapshotCommand = new RelayCommand(() => _ = LoadSnapshotAsync());
        BrowseSnapshotCommand = new RelayCommand(BrowseSnapshot);
        CancelLoadingCommand = new RelayCommand(CancelLoading);
        CompareSnapshotsCommand = new RelayCommand(() => _ = CompareSnapshotsAsync());
        CloseSnapshotCommand = new RelayCommand(CloseSnapshot);

        // 如果提供了两个快照路径，自动进入对比模式
        if (!string.IsNullOrWhiteSpace(snapshotPath) && File.Exists(snapshotPath) &&
            !string.IsNullOrWhiteSpace(compareSnapshotPath) && File.Exists(compareSnapshotPath))
        {
            SnapshotPath = snapshotPath;
            // 延迟加载对比，让UI先渲染完成
            Dispatcher.BeginInvoke(new Action(async () => await CompareSnapshotsAsync(snapshotPath, compareSnapshotPath)), 
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
        // 否则如果只提供了一个快照路径，正常加载
        else if (!string.IsNullOrWhiteSpace(snapshotPath) && File.Exists(snapshotPath))
        {
            SnapshotPath = snapshotPath;
            // 延迟加载，让UI先渲染完成
            Dispatcher.BeginInvoke(new Action(async () => await LoadSnapshotAsync()), 
                System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void BrowseSnapshot()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Unity Memory Snapshot File",
            Filter = "Unity Memory Snapshot Files (*.snap)|*.snap|All Files (*.*)|*.*",
            FilterIndex = 1,
            CheckFileExists = true,
            CheckPathExists = true
        };

        if (openFileDialog.ShowDialog() == true)
        {
            SnapshotPath = openFileDialog.FileName;
            _ = LoadSnapshotAsync();
        }
    }

    private void CancelLoading()
    {
        _loadingCancellationTokenSource?.Cancel();
        LoadingStatusText = "正在取消...";
    }

    private async Task LoadSnapshotAsync()
    {
        if (IsLoading)
        {
            Console.WriteLine("[警告] 快照正在加载中，请等待当前加载完成。");
            return;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(SnapshotPath))
            {
                Console.WriteLine("[错误] 请选择一个快照文件。");
                return;
            }

            if (!File.Exists(SnapshotPath))
            {
                Console.WriteLine($"[错误] 文件不存在: {SnapshotPath}");
                return;
            }

            // 初始化加载状态
            IsLoading = true;
            LoadingProgress = 0;
            LoadingStatusText = "正在准备加载...";
            LoadingElapsedTime = "00:00";
            _loadingCancellationTokenSource = new CancellationTokenSource();
            _loadingStopwatch = Stopwatch.StartNew();

            // 启动计时器更新已用时间
            var timerTask = Task.Run(async () =>
            {
                while (IsLoading && !_loadingCancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(100);
                    var elapsed = _loadingStopwatch?.Elapsed ?? TimeSpan.Zero;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        LoadingElapsedTime = $"{elapsed:mm\\:ss}";
                    });
                }
            });

            // 🔑 关键修复：在加载新快照前，先释放所有旧快照
            // 这确保了从对比模式切换到单快照模式时，对比快照会被正确释放
            DisposeAllSnapshots();

            // 阶段1：打开文件 (0-20%)
            LoadingStatusText = "正在打开快照文件...";
            LoadingProgress = 0;

            // 使用FileReader（与Unity官方一致，避免过度包装）
            await Task.Run(() =>
            {
                var fileReader = new FileReader();
                var openResult = fileReader.Open(SnapshotPath);

                if (openResult != ReadError.Success)
                {
                    throw new Exception($"无法打开快照: {openResult}");
                }

                Dispatcher.Invoke(() =>
                {
                    LoadingProgress = 10;
                    LoadingStatusText = "快照文件打开成功！正在创建CachedSnapshot...";
                });

                try
                {
                    _currentSnapshot = new CachedSnapshot(fileReader);
                }
                catch (NullReferenceException ex) when (ex.StackTrace?.Contains("ProcessDynamicSizeElement") == true)
                {
                    var fileInfo = new FileInfo(SnapshotPath);
                    throw new Exception(
                        $"快照文件格式不兼容或已损坏。\n\n" +
                        $"此快照可能由不同版本的Unity生成，或文件在捕获过程中损坏。\n\n" +
                        $"建议：\n" +
                        $"1. 使用Unity Editor 2023.2或更高版本重新捕获快照\n" +
                        $"2. 使用Unity Memory Profiler Package 1.1.6或更高版本\n" +
                        $"3. 在Unity Editor中验证文件可以正常打开\n\n" +
                        $"文件信息：\n" +
                        $"  路径: {SnapshotPath}\n" +
                        $"  大小: {fileInfo.Length / 1024 / 1024:N2} MB\n" +
                        $"  修改时间: {fileInfo.LastWriteTime:yyyy-MM-dd HH:mm:ss}\n\n" +
                        $"技术细节: {ex.Message}\n" +
                        $"错误位置: ProcessDynamicSizeElement",
                        ex
                    );
                }

                Dispatcher.Invoke(() =>
                {
                    LoadingProgress = 40;
                    LoadingStatusText = "快照数据加载完成";
                });
            }, _loadingCancellationTokenSource.Token);

            // 阶段3：后处理 (40-80%)
            LoadingStatusText = "正在处理快照数据...";

            await Task.Run(() =>
            {
                var enumerator = _currentSnapshot.PostProcess();
                int stepCount = 0;
                int totalSteps = 100; // 估计步数

                while (enumerator.MoveNext())
                {
                    if (_loadingCancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        throw new OperationCanceledException("用户取消了加载操作。");
                    }

                    stepCount++;
                    var progress = 40 + (40 * Math.Min(stepCount, totalSteps) / totalSteps);
                    Dispatcher.Invoke(() => LoadingProgress = progress);
                }

                Dispatcher.Invoke(() => LoadingProgress = 80);
            }, _loadingCancellationTokenSource.Token);

            // 阶段4：加载到 ViewModels (80-100%)
            LoadingStatusText = "正在更新视图...";
            LoadingProgress = 80;

            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    SummaryViewModel.LoadSnapshot(_currentSnapshot);
                    LoadingProgress = 85;

                    UnityObjectsViewModel.LoadSnapshot(_currentSnapshot);
                    LoadingProgress = 88;

                    AllTrackedMemoryViewModel.LoadSnapshot(_currentSnapshot);
                    LoadingProgress = 92;

                    ManagedObjectsViewModel.LoadSnapshot(_currentSnapshot);
                    LoadingProgress = 95;
                });
            });

            LoadingProgress = 100;
            LoadingStatusText = "加载完成！";

            // 延迟一小段时间让用户看到100%
            await Task.Delay(500);

            // 🔑 通知快照管理面板更新状态（显示A标签和卡片）
            SnapshotManagementViewModel.NotifySnapshotLoaded(SnapshotPath, isCompared: false);
            Console.WriteLine($"[MainWindow] 已通知SnapshotManagementViewModel: {SnapshotPath}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[提示] 快照加载已取消。");
            _currentSnapshot?.Dispose();
            _currentSnapshot = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 加载快照时出错: {ex.Message}");
            Console.WriteLine($"堆栈跟踪:\n{ex.StackTrace}");
            _currentSnapshot?.Dispose();
            _currentSnapshot = null;
        }
        finally
        {
            _loadingStopwatch?.Stop();
            IsLoading = false;
            _loadingCancellationTokenSource?.Dispose();
            _loadingCancellationTokenSource = null;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        
        // 🔑 关键修复：窗口关闭时释放所有快照
        // 确保应用程序退出时所有资源都被正确释放
        DisposeAllSnapshots();
    }

    /// <summary>
    /// 对比两个快照（重载：带路径参数）
    /// </summary>
    private async Task CompareSnapshotsAsync(string snapshotPathA, string snapshotPathB)
    {
        if (IsLoading)
        {
            Console.WriteLine("[警告] 快照正在加载中，请等待当前加载完成。");
            return;
        }

        await CompareSnapshotsInternalAsync(snapshotPathA, snapshotPathB);
    }

    /// <summary>
    /// 对比两个快照（通过对话框选择）
    /// </summary>
    private async Task CompareSnapshotsAsync()
    {
        if (IsLoading)
        {
            Console.WriteLine("[警告] 快照正在加载中，请等待当前加载完成。");
            return;
        }

        // 选择第一个快照
        var openFileDialog1 = new OpenFileDialog
        {
            Title = "Select First Snapshot File (A)",
            Filter = "Unity Memory Snapshot Files (*.snap)|*.snap|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(SnapshotPath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (openFileDialog1.ShowDialog() != true)
            return;

        // 选择第二个快照
        var openFileDialog2 = new OpenFileDialog
        {
            Title = "Select Second Snapshot File (B)",
            Filter = "Unity Memory Snapshot Files (*.snap)|*.snap|All Files (*.*)|*.*",
            InitialDirectory = Path.GetDirectoryName(openFileDialog1.FileName) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (openFileDialog2.ShowDialog() != true)
            return;

        var snapshotPathA = openFileDialog1.FileName;
        var snapshotPathB = openFileDialog2.FileName;

        await CompareSnapshotsInternalAsync(snapshotPathA, snapshotPathB);
    }

    /// <summary>
    /// 对比两个快照的内部实现
    /// </summary>
    private async Task CompareSnapshotsInternalAsync(string snapshotPathA, string snapshotPathB)
    {
        // 🔑 关键修复：在加载新快照前，先释放所有旧快照
        // 这确保了从单快照模式切换到对比模式，或重新对比时，旧快照会被正确释放
        DisposeAllSnapshots();

        // 🔑 关键：在 try 外部定义，以便在 catch 块中访问
        CachedSnapshot? snapshotA = null;
        CachedSnapshot? snapshotB = null;

        try
        {
            // 初始化加载状态
            IsLoading = true;
            LoadingProgress = 0;
            LoadingStatusText = "正在加载快照A...";
            LoadingElapsedTime = "00:00";
            _loadingCancellationTokenSource = new CancellationTokenSource();
            _loadingStopwatch = Stopwatch.StartNew();

            // 启动计时器
            var timerTask = Task.Run(async () =>
            {
                while (IsLoading && !_loadingCancellationTokenSource.Token.IsCancellationRequested)
                {
                    await Task.Delay(100);
                    var elapsed = _loadingStopwatch?.Elapsed ?? TimeSpan.Zero;
                    await Dispatcher.InvokeAsync(() =>
                    {
                        LoadingElapsedTime = $"{elapsed:mm\\:ss}";
                    });
                }
            });

            // 阶段1：加载快照A (0-45%)
            LoadingStatusText = $"正在加载快照A: {Path.GetFileName(snapshotPathA)}";
            LoadingProgress = 0;

            await Task.Run(() =>
            {
                var fileReaderA = new FileReader();
                var openResultA = fileReaderA.Open(snapshotPathA);
                if (openResultA != ReadError.Success)
                {
                    throw new Exception($"无法打开快照A: {openResultA}");
                }

                Dispatcher.Invoke(() =>
                {
                    LoadingProgress = 15;
                    LoadingStatusText = "正在读取快照A数据...";
                });

                snapshotA = new CachedSnapshot(fileReaderA);

                Dispatcher.Invoke(() =>
                {
                    LoadingProgress = 30;
                    LoadingStatusText = "正在处理快照A数据...";
                });

                var enumerator = snapshotA.PostProcess();
                while (enumerator.MoveNext())
                {
                    if (_loadingCancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        throw new OperationCanceledException("用户取消了加载操作。");
                    }
                }

                Dispatcher.Invoke(() => LoadingProgress = 45);
            }, _loadingCancellationTokenSource.Token);

            // 阶段2：加载快照B (45-90%)
            LoadingStatusText = $"正在加载快照B: {Path.GetFileName(snapshotPathB)}";
            LoadingProgress = 45;

            await Task.Run(() =>
            {
                var fileReaderB = new FileReader();
                var openResultB = fileReaderB.Open(snapshotPathB);
                if (openResultB != ReadError.Success)
                {
                    throw new Exception($"无法打开快照B: {openResultB}");
                }

                Dispatcher.Invoke(() =>
                {
                    LoadingProgress = 60;
                    LoadingStatusText = "正在读取快照B数据...";
                });

                snapshotB = new CachedSnapshot(fileReaderB);

                Dispatcher.Invoke(() =>
                {
                    LoadingProgress = 75;
                    LoadingStatusText = "正在处理快照B数据...";
                });

                var enumerator = snapshotB.PostProcess();
                while (enumerator.MoveNext())
                {
                    if (_loadingCancellationTokenSource?.Token.IsCancellationRequested == true)
                    {
                        throw new OperationCanceledException("用户取消了加载操作。");
                    }
                }

                Dispatcher.Invoke(() => LoadingProgress = 90);
            }, _loadingCancellationTokenSource.Token);

            // 阶段3：保存快照引用并通知各ViewModel切换到对比模式 (90-100%)
            LoadingStatusText = "正在构建对比数据...";
            LoadingProgress = 90;

            // 🔑 关键修复：MainWindow 获得快照所有权
            // 保存到成员变量，确保快照不会被 GC 回收，且可以在需要时正确释放
            _comparedSnapshotA = snapshotA;
            _comparedSnapshotB = snapshotB;

            // ✅ 正确的架构：通知所有ViewModel切换到对比模式（而不是创建独立ComparisonTab）
            // ViewModel 只持有只读引用，不负责释放
            // Summary（已实现）
            SummaryViewModel.CompareSnapshots(_comparedSnapshotA, _comparedSnapshotB);
            
            // Unity Objects（已实现）
            UnityObjectsViewModel.CompareSnapshots(_comparedSnapshotA, _comparedSnapshotB);
            
            // All Of Memory（已实现）
            AllTrackedMemoryViewModel.CompareSnapshots(_comparedSnapshotA, _comparedSnapshotB);
            
            // Managed Objects（新增）
            ManagedObjectsViewModel.CompareSnapshots(_comparedSnapshotA, _comparedSnapshotB);

            LoadingProgress = 100;
            LoadingStatusText = "对比加载完成！";
            await Task.Delay(500);

            // 对比模式加载完成，不自动跳转标签页，让用户自己选择查看哪个视图
            // 用户可以在 Summary、Unity Objects 或 All Of Memory 中查看对比数据

            // 🔑 通知快照管理面板更新状态（显示A/B标签和卡片）
            SnapshotManagementViewModel.NotifySnapshotLoaded(snapshotPathA, isCompared: false);
            SnapshotManagementViewModel.NotifySnapshotLoaded(snapshotPathB, isCompared: true);
            // 切换到对比模式
            SnapshotManagementViewModel.CompareMode = true;
            Console.WriteLine($"[MainWindow] 已通知SnapshotManagementViewModel: A={Path.GetFileName(snapshotPathA)}, B={Path.GetFileName(snapshotPathB)}");

            Console.WriteLine($"[成功] 快照对比完成！");
            Console.WriteLine($"  快照A: {Path.GetFileName(snapshotPathA)}");
            Console.WriteLine($"  快照B: {Path.GetFileName(snapshotPathB)}");
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("[提示] 快照对比已取消。");
            
            // 🔑 关键修复：异常时释放已创建的快照
            // 如果加载过程中取消，需要释放已经创建的快照
            snapshotA?.Dispose();
            snapshotB?.Dispose();
            _comparedSnapshotA = null;
            _comparedSnapshotB = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[错误] 对比快照时出错: {ex.Message}");
            Console.WriteLine($"堆栈跟踪:\n{ex.StackTrace}");
            
            // 🔑 关键修复：异常时释放已创建的快照
            // 如果加载失败，需要释放已经创建的快照
            snapshotA?.Dispose();
            snapshotB?.Dispose();
            _comparedSnapshotA = null;
            _comparedSnapshotB = null;
        }
        finally
        {
            _loadingStopwatch?.Stop();
            IsLoading = false;
            _loadingCancellationTokenSource?.Dispose();
            _loadingCancellationTokenSource = null;
        }
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

    /// <summary>
    /// 处理来自快照管理面板的加载请求
    /// </summary>
    private void OnSnapshotLoadRequested(object? sender, string snapshotPath)
    {
        SnapshotPath = snapshotPath;
        _ = LoadSnapshotAsync();
    }

    /// <summary>
    /// 处理来自快照管理面板的对比请求
    /// </summary>
    private void OnSnapshotCompareRequested(object? sender, (string, string) snapshots)
    {
        _ = CompareSnapshotsAsync(snapshots.Item1, snapshots.Item2);
    }

    /// <summary>
    /// 处理来自快照管理面板的关闭快照请求（释放CachedSnapshot）
    /// </summary>
    private void OnSnapshotClosed(object? sender, SnapshotClosedEventArgs e)
    {
        Console.WriteLine($"[MainWindow] 收到快照关闭请求: {e.FilePath}, IsCompared={e.IsCompared}");

        // 释放CachedSnapshot（具体是哪个快照由ViewModel管理）
        if (_currentSnapshot != null)
        {
            Console.WriteLine($"[MainWindow] 释放CachedSnapshot: {e.FilePath}");
            _currentSnapshot.Dispose();
            _currentSnapshot = null;
        }

        // 视图清空由OnLoadedSnapshotsChanged统一处理
        Console.WriteLine($"[MainWindow] CachedSnapshot已释放，等待LoadedSnapshotsChanged事件刷新UI");
    }

    /// <summary>
    /// 处理LoadedSnapshotsChanged事件（Unity架构：统一刷新UI）
    /// </summary>
    private void OnLoadedSnapshotsChanged(object? sender, EventArgs e)
    {
        Console.WriteLine($"[MainWindow] LoadedSnapshotsChanged事件触发");

        var hasBase = SnapshotManagementViewModel.BaseSnapshot != null;
        var hasCompared = SnapshotManagementViewModel.ComparedSnapshot != null;

        Console.WriteLine($"[MainWindow] 当前状态: Base={hasBase}, Compared={hasCompared}");

        if (!hasBase && !hasCompared)
        {
            // 无快照：清空所有视图
            Console.WriteLine($"[MainWindow] 无快照，清空所有视图");
            ClearAllViews();
        }
        else
        {
            // 有快照：保持当前视图（数据已加载）
            Console.WriteLine($"[MainWindow] 有快照，保持当前视图");
        }
    }

    /// <summary>
    /// 清空所有视图
    /// </summary>
    private void ClearAllViews()
    {
        Console.WriteLine("[MainWindow] 开始清空所有视图...");

        SummaryViewModel.Clear();
        UnityObjectsViewModel.Clear();
        AllTrackedMemoryViewModel.Clear();
        ManagedObjectsViewModel.Clear();

        SnapshotPath = string.Empty;

        Console.WriteLine("[MainWindow] 所有视图已清空");
    }

    /// <summary>
    /// 释放所有快照资源（统一释放方法）
    /// 参考 Unity 的 SnapshotDataService.UnloadSnapshot 模式
    /// </summary>
    private void DisposeAllSnapshots()
    {
        // 释放单快照模式的快照
        if (_currentSnapshot != null)
        {
            Console.WriteLine($"[MainWindow] 释放单快照: {_currentSnapshot.FullPath}");
            _currentSnapshot.Dispose();
            _currentSnapshot = null;
        }

        // 释放对比模式的快照 A
        if (_comparedSnapshotA != null)
        {
            Console.WriteLine($"[MainWindow] 释放对比快照 A: {_comparedSnapshotA.FullPath}");
            _comparedSnapshotA.Dispose();
            _comparedSnapshotA = null;
        }

        // 释放对比模式的快照 B
        if (_comparedSnapshotB != null)
        {
            Console.WriteLine($"[MainWindow] 释放对比快照 B: {_comparedSnapshotB.FullPath}");
            _comparedSnapshotB.Dispose();
            _comparedSnapshotB = null;
        }
    }

    /// <summary>
    /// 关闭当前快照（从工具栏Close按钮触发）
    /// </summary>
    private void CloseSnapshot()
    {
        Console.WriteLine("[MainWindow] 从工具栏关闭快照");

        // 释放所有 CachedSnapshot
        DisposeAllSnapshots();

        // 清空所有视图
        ClearAllViews();

        // 通知快照管理面板更新状态
        SnapshotManagementViewModel.NotifySnapshotLoaded(null, false);

        Console.WriteLine("[MainWindow] 快照已关闭");
    }

    /// <summary>
    /// ViewModel属性变化事件处理（更新SelectionDetails）
    /// 参考: Unity的BreakdownDetailsViewControllerFactory.Create逻辑
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is SummaryViewModel && e.PropertyName == nameof(SummaryViewModel.SelectedNode))
        {
            var node = SummaryViewModel.SelectedNode;
            var snapshot = SummaryViewModel.CurrentSnapshot;
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.Summary))
                {
                    SelectionDetailsPanel.ClearSelection();
                }
            }
            else
            {
                SelectionDetailsPanel.ClearSelection();
            }
        }
        else if (sender is UnityObjectsViewModel && e.PropertyName == nameof(UnityObjectsViewModel.SelectedNode))
        {
            var node = UnityObjectsViewModel.SelectedNode;
            var snapshot = UnityObjectsViewModel.CurrentSnapshot;
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.UnityObjects))
                {
                    SelectionDetailsPanel.ClearSelection();
                }
            }
            else
            {
                SelectionDetailsPanel.ClearSelection();
            }
        }
        else if (sender is AllTrackedMemoryViewModel && e.PropertyName == nameof(AllTrackedMemoryViewModel.SelectedNode))
        {
            var node = AllTrackedMemoryViewModel.SelectedNode;
            var snapshot = AllTrackedMemoryViewModel.CurrentSnapshot;
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.AllTrackedMemory))
                {
                    SelectionDetailsPanel.ClearSelection();
                }
            }
            else
            {
                SelectionDetailsPanel.ClearSelection();
            }
        }
        else if (sender is AllTrackedMemoryViewModel && e.PropertyName == nameof(AllTrackedMemoryViewModel.BaseSelectedNode))
        {
            // Base 子表选择（对比模式）
            var node = AllTrackedMemoryViewModel.BaseSelectedNode;
            var snapshot = AllTrackedMemoryViewModel.CurrentSnapshot; // Base snapshot
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.AllTrackedMemory))
                {
                    SelectionDetailsPanel.ClearSelection();
                }
            }
            else if (node == null && AllTrackedMemoryViewModel.ComparedSelectedNode == null)
            {
                // 只有当两个表都没有选择时才清空 SelectionDetails
                SelectionDetailsPanel.ClearSelection();
            }
            // 如果 node == null 但 ComparedSelectedNode != null，说明是互斥清空，不处理
        }
        else if (sender is AllTrackedMemoryViewModel && e.PropertyName == nameof(AllTrackedMemoryViewModel.ComparedSelectedNode))
        {
            // Compared 子表选择（对比模式）
            var node = AllTrackedMemoryViewModel.ComparedSelectedNode;
            var snapshot = AllTrackedMemoryViewModel.ComparedSnapshot; // Compared snapshot
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.AllTrackedMemory))
                {
                    SelectionDetailsPanel.ClearSelection();
                }
            }
            else if (node == null && AllTrackedMemoryViewModel.BaseSelectedNode == null)
            {
                // 只有当两个表都没有选择时才清空 SelectionDetails
                SelectionDetailsPanel.ClearSelection();
            }
            // 如果 node == null 但 BaseSelectedNode != null，说明是互斥清空，不处理
        }
        // Unity Objects - Base 子表选择（对比模式）
        else if (sender is UnityObjectsViewModel && e.PropertyName == nameof(UnityObjectsViewModel.SelectedBaseNode))
        {
            var node = UnityObjectsViewModel.SelectedBaseNode;
            var snapshot = UnityObjectsViewModel.CurrentSnapshot; // Base snapshot
            
            System.Diagnostics.Debug.WriteLine($"[UnityObjects] SelectedBaseNode changed: node={node?.Name}, snapshot={snapshot != null}");
            
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.UnityObjects))
                {
                    System.Diagnostics.Debug.WriteLine($"[UnityObjects] TryPresent failed for Base node: {node.Name}");
                    SelectionDetailsPanel.ClearSelection();
            }
            else
            {
                    System.Diagnostics.Debug.WriteLine($"[UnityObjects] TryPresent succeeded for Base node: {node.Name}");
                }
            }
            else if (node == null && UnityObjectsViewModel.SelectedComparedNode == null)
            {
                // 只有当两个表都没有选择时才清空 SelectionDetails
                System.Diagnostics.Debug.WriteLine($"[UnityObjects] Both tables have no selection, clearing SelectionDetails");
                SelectionDetailsPanel.ClearSelection();
            }
            // 如果 node == null 但 SelectedComparedNode != null，说明是互斥清空，不处理
        }
        // Unity Objects - Compared 子表选择（对比模式）
        else if (sender is UnityObjectsViewModel && e.PropertyName == nameof(UnityObjectsViewModel.SelectedComparedNode))
        {
            var node = UnityObjectsViewModel.SelectedComparedNode;
            var snapshot = UnityObjectsViewModel.ComparedSnapshot; // Compared snapshot
            
            System.Diagnostics.Debug.WriteLine($"[UnityObjects] SelectedComparedNode changed: node={node?.Name}, snapshot={snapshot != null}");
            
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.UnityObjects))
                {
                    System.Diagnostics.Debug.WriteLine($"[UnityObjects] TryPresent failed for Compared node: {node.Name}");
                    SelectionDetailsPanel.ClearSelection();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[UnityObjects] TryPresent succeeded for Compared node: {node.Name}");
                }
            }
            else if (node == null && UnityObjectsViewModel.SelectedBaseNode == null)
            {
                // 只有当两个表都没有选择时才清空 SelectionDetails
                System.Diagnostics.Debug.WriteLine($"[UnityObjects] Both tables have no selection, clearing SelectionDetails");
                SelectionDetailsPanel.ClearSelection();
            }
            // 如果 node == null 但 SelectedBaseNode != null，说明是互斥清空，不处理
        }
        // Managed Objects - Detail Node 选择
        else if (sender is ManagedObjectsViewModel && e.PropertyName == nameof(ManagedObjectsViewModel.SelectedDetailNode))
        {
            var node = ManagedObjectsViewModel.SelectedDetailNode;
            var snapshot = ManagedObjectsViewModel.CurrentSnapshot;
            
            System.Diagnostics.Debug.WriteLine($"[ManagedObjects] SelectedDetailNode changed: node={node?.Name}, snapshot={snapshot != null}");
            
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.ManagedObjects))
                {
                    System.Diagnostics.Debug.WriteLine($"[ManagedObjects] TryPresent failed for Detail node: {node.Name}");
                    SelectionDetailsPanel.ClearSelection();
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ManagedObjects] TryPresent succeeded for Detail node: {node.Name}");
                }
            }
            else
            {
                SelectionDetailsPanel.ClearSelection();
            }
        }
        // Managed Objects - Base Node 选择（对比模式）
        else if (sender is ManagedObjectsViewModel && e.PropertyName == nameof(ManagedObjectsViewModel.SelectedBaseNode))
        {
            var node = ManagedObjectsViewModel.SelectedBaseNode;
            var snapshot = ManagedObjectsViewModel.CurrentSnapshot; // Base snapshot
            
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.ManagedObjects))
                {
                    SelectionDetailsPanel.ClearSelection();
                }
            }
            else if (node == null && ManagedObjectsViewModel.SelectedComparedNode == null)
            {
                SelectionDetailsPanel.ClearSelection();
            }
        }
        // Managed Objects - Compared Node 选择（对比模式）
        else if (sender is ManagedObjectsViewModel && e.PropertyName == nameof(ManagedObjectsViewModel.SelectedComparedNode))
        {
            var node = ManagedObjectsViewModel.SelectedComparedNode;
            var snapshot = ManagedObjectsViewModel.ComparedSnapshot; // Compared snapshot
            
            if (node != null && snapshot != null)
            {
                if (!_selectionDetailsService.TryPresent(SelectionDetailsPanel, node, snapshot, SelectionDetailsSource.ManagedObjects))
                {
                    SelectionDetailsPanel.ClearSelection();
                }
            }
            else if (node == null && ManagedObjectsViewModel.SelectedBaseNode == null)
            {
                SelectionDetailsPanel.ClearSelection();
            }
        }
    }

    /// <summary>
    /// 处理SummaryViewModel的InspectRequested事件
    /// </summary>
    private void OnSummaryInspectRequested(object? sender, SummaryViewModel.SummaryInspectRequestEventArgs e)
    {
        switch (e.Target)
        {
            case SummaryViewModel.SummaryInspectTarget.AllOfMemory:
                MainTabControl.SelectedIndex = 2; // All Of Memory tab
                break;
            case SummaryViewModel.SummaryInspectTarget.UnityObjects:
                MainTabControl.SelectedIndex = 1; // Unity Objects tab
                break;
        }
    }
}