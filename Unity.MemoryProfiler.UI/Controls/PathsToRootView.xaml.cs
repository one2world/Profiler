using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using DevExpress.Xpf.Grid;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Models;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// PathsToRoot引用浏览视图
    /// 参考: Unity.MemoryProfiler.Editor.UI.PathsToRoot.PathsToRootDetailView
    /// </summary>
    public partial class PathsToRootView : UserControl
    {
        /// <summary>
        /// 视图模式
        /// </summary>
        public enum ViewMode
        {
            ReferencedBy,   // 显示哪些对象引用了当前选中对象
            ReferencesTo    // 显示当前对象引用了哪些对象
        }

        /// <summary>
        /// GUI状态
        /// </summary>
        private enum GUIState
        {
            NothingSelected,
            Searching,
            SearchComplete
        }

        /// <summary>
        /// 临时引用数据结构（仅用于后台线程，不包含UI对象）
        /// 用于在后台线程收集数据，然后在UI线程构建PathsToRootTreeNode
        /// </summary>
        private class ReferenceData
        {
            public ObjectData Data { get; set; }
            public List<ReferenceData> Children { get; set; } = new List<ReferenceData>();
        }

        private CachedSnapshot? m_CachedSnapshot;
        private SourceIndex m_CurrentSelection;
        private ViewMode m_ActiveMode;
        private GUIState m_GUIState;

        // 两个视图的根节点
        private PathsToRootTreeNode? m_ReferencedByRoot;
        private PathsToRootTreeNode? m_ReferencesToRoot;

        // 后台搜索线程
        private Thread? m_BackgroundThread;
        private volatile bool m_CancelSearch;

        // 防止RefreshTreeView期间触发SelectionChanged事件的标志
        private bool m_IsRefreshingTreeView = false;

        // 临时数据列表（用于引用搜索）
        private List<ObjectData> m_CachedObjectDataList = new List<ObjectData>();
        private HashSet<SourceIndex> m_ReferenceSearchAccelerator = new HashSet<SourceIndex>();

        // 事件
        internal event Action<SourceIndex>? SelectionChanged;

        public PathsToRootView()
        {
            InitializeComponent();
            m_ActiveMode = ViewMode.ReferencedBy;
            m_GUIState = GUIState.NothingSelected;
            UpdateGUIState();
        }

        /// <summary>
        /// 设置根对象
        /// </summary>
        internal void SetRoot(CachedSnapshot snapshot, SourceIndex source)
        {
            m_CachedSnapshot = snapshot;

            if (m_CachedSnapshot == null || !source.Valid)
            {
                ClearSelection();
                return;
            }

            // 取消之前的搜索
            CancelCurrentSearch();

            m_CurrentSelection = source;

            // 根据对象类型决定是否可以查找引用
            switch (source.Id)
            {
                case SourceIndex.SourceId.NativeObject:
                case SourceIndex.SourceId.NativeAllocation:
                case SourceIndex.SourceId.ManagedObject:
                    StartBackgroundSearch(source);
                    break;
                case SourceIndex.SourceId.GfxResource:
                    var objectData = ObjectData.FromSourceLink(m_CachedSnapshot, source);
                    if (objectData.IsValid && objectData.nativeObjectIndex >= 0)
                        StartBackgroundSearch(new SourceIndex(SourceIndex.SourceId.NativeObject, objectData.nativeObjectIndex));
                    break;
                default:
                    ClearSelection();
                    break;
            }
        }

        /// <summary>
        /// 清空选择
        /// </summary>
        public void ClearSelection()
        {
            m_CurrentSelection = default;
            m_ReferencedByRoot = null;
            m_ReferencesToRoot = null;
            m_GUIState = GUIState.NothingSelected;
            UpdateGUIState();
        }

        /// <summary>
        /// 取消当前搜索
        /// </summary>
        private void CancelCurrentSearch()
        {
            if (m_BackgroundThread != null && m_BackgroundThread.IsAlive)
            {
                m_CancelSearch = true;
                m_BackgroundThread.Join(1000); // 等待最多1秒
                // .NET Core/5+ 不支持 Thread.Abort()
                // 如果线程在1秒内没有响应取消信号，我们只能放弃等待
                // 线程会在下次检查 m_CancelSearch 时自然退出
                m_BackgroundThread = null;
            }
        }

        /// <summary>
        /// 启动后台搜索
        /// </summary>
        private void StartBackgroundSearch(SourceIndex source)
        {
            m_GUIState = GUIState.Searching;
            UpdateGUIState();

            m_CancelSearch = false;
            m_BackgroundThread = new Thread(() => SearchThread(source));
            m_BackgroundThread.IsBackground = true;
            m_BackgroundThread.Start();
        }

        /// <summary>
        /// 后台搜索线程 - 使用新的 PathsToRootBuilder
        /// </summary>
        private void SearchThread(SourceIndex source)
        {
            try
            {
                if (m_CachedSnapshot == null) return;

                // 使用新的 PathsToRootBuilder 递归构建完整路径树
                var builder = new Services.PathsToRootBuilder(m_CachedSnapshot, maxDepth: 10);
                
                // 搜索Referenced By (哪些对象引用了当前对象)
                var referencedByPaths = builder.BuildReferencedBy(source);

                if (m_CancelSearch) return;

                // 搜索References To (当前对象引用了哪些对象) - 使用旧方法保持兼容
                var referencesToData = SearchForReferencesToData(m_CachedSnapshot, source);

                if (m_CancelSearch) return;

                // 更新UI（必须在UI线程）
                Dispatcher.Invoke(() =>
                {
                    // 转换新格式到 TreeNode
                    m_ReferencedByRoot = ConvertPathsToTreeNode(referencedByPaths, m_CachedSnapshot);
                    m_ReferencesToRoot = BuildTreeFromData(referencesToData, m_CachedSnapshot);
                    
                    m_GUIState = GUIState.SearchComplete;
                    UpdateGUIState();
                    RefreshTreeView();
                });
            }
            catch (ThreadAbortException)
            {
                // 搜索被取消
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PathsToRootView search error: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    m_GUIState = GUIState.SearchComplete;
                    UpdateGUIState();
                });
            }
        }

        /// <summary>
        /// 将新的 ReferencePathNode 转换为 PathsToRootTreeNode
        /// </summary>
        private PathsToRootTreeNode ConvertPathsToTreeNode(List<Services.ReferencePathNode> paths, CachedSnapshot snapshot)
        {
            var root = new PathsToRootTreeNode(
                ObjectData.Invalid,
                snapshot,
                null,
                truncateTypeNames: false
            );

            foreach (var path in paths)
            {
                var childNode = ConvertPathNodeRecursive(path, snapshot, root);
                if (childNode != null)
                    root.AddChild(childNode);  // 使用AddChild设置Parent引用
            }

            return root;
        }

        /// <summary>
        /// 递归转换路径节点
        /// </summary>
        private PathsToRootTreeNode? ConvertPathNodeRecursive(
            Services.ReferencePathNode pathNode,
            CachedSnapshot snapshot,
            PathsToRootTreeNode parent)
        {
            // **关键修复：使用 DisplayObjectData（被引用的目标对象）**
            // 参考Unity代码：PathsToRootDetailView.cs Line 499
            //   var child = new PathsToRootDetailTreeViewItem(connection.displayObject, ...)
            //
            // PathsToRoot树的逻辑：
            // 1. 节点的显示名称来自ConnectionData（如"field m_Mesh"、"Mesh[3]"）
            // 2. 但节点的Data存储的是DisplayObjectData（被引用的对象）
            // 3. 点击节点时，Selection Details显示的是被引用的对象
            var objectData = pathNode.DisplayObjectData;

            if (!objectData.IsValid)
                return null;

            // 创建 TreeNode，使用 truncateTypeNames: true 来截断类型名
            // Unity 的 Referenced By 显示的是截断后的类型名（如 "Mesh[3]" 而不是 "UnityEngine.Mesh[3]"）
            var treeNode = new PathsToRootTreeNode(
                objectData,
                snapshot,
                parent,
                truncateTypeNames: true  // Unity使用截断的类型名
            );

            // 标记 GC Root
            if (pathNode.IsGCRoot)
            {
                treeNode.IsGCRoot = true;
            }

            // 递归转换子节点
            if (pathNode.Children != null && pathNode.Children.Count > 0)
            {
                foreach (var childPath in pathNode.Children)
                {
                    var childNode = ConvertPathNodeRecursive(childPath, snapshot, treeNode);
                    if (childNode != null)
                        treeNode.AddChild(childNode);  // 使用AddChild设置Parent引用
                }
            }

            return treeNode;
        }

        /// <summary>
        /// 更新GUI状态
        /// </summary>
        private void UpdateGUIState()
        {
            switch (m_GUIState)
            {
                case GUIState.NothingSelected:
                    PathsTreeList.Visibility = Visibility.Collapsed;
                    NoSelectionMessage.Visibility = Visibility.Visible;
                    NoReferencesMessage.Visibility = Visibility.Collapsed;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    break;

                case GUIState.Searching:
                    PathsTreeList.Visibility = Visibility.Collapsed;
                    NoSelectionMessage.Visibility = Visibility.Collapsed;
                    NoReferencesMessage.Visibility = Visibility.Collapsed;
                    LoadingOverlay.Visibility = Visibility.Visible;
                    break;

                case GUIState.SearchComplete:
                    var currentRoot = m_ActiveMode == ViewMode.ReferencedBy ? m_ReferencedByRoot : m_ReferencesToRoot;
                    var hasChildren = currentRoot != null && currentRoot.Children.Count > 0;

                    PathsTreeList.Visibility = hasChildren ? Visibility.Visible : Visibility.Collapsed;
                    NoSelectionMessage.Visibility = Visibility.Collapsed;
                    NoReferencesMessage.Visibility = hasChildren ? Visibility.Collapsed : Visibility.Visible;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        /// <summary>
        /// 刷新TreeView
        /// </summary>
        private void RefreshTreeView()
        {
            // 设置刷新标志，防止设置ItemsSource和FocusedRowHandle时触发SelectionChanged
            m_IsRefreshingTreeView = true;

            try
            {
                var currentRoot = m_ActiveMode == ViewMode.ReferencedBy ? m_ReferencedByRoot : m_ReferencesToRoot;

                if (currentRoot != null)
                {
                    PathsTreeList.ItemsSource = currentRoot.Children;

                    // 更新Ribbon按钮文本
                    ReferencedByButton.Content = $"Referenced By ({m_ReferencedByRoot?.Children.Count ?? 0})";
                    ReferencesToButton.Content = $"References To ({m_ReferencesToRoot?.Children.Count ?? 0})";

                    // **关键修复：清除自动选中，避免触发SelectionChanged事件**
                    // DevExpress TreeListControl设置ItemsSource后可能自动选中第一行
                    // Unity的行为是：PathsToRoot树不自动选中任何节点
                    // 只有用户主动点击树中的节点时才更新SelectionDetails
                    PathsTreeList.View.FocusedRowHandle = DevExpress.Xpf.Grid.DataControlBase.InvalidRowHandle;
                }
                else
                {
                    PathsTreeList.ItemsSource = null;
                    ReferencedByButton.Content = "Referenced By (0)";
                    ReferencesToButton.Content = "References To (0)";
                }
            }
            finally
            {
                // 重要：确保标志被清除，即使发生异常
                m_IsRefreshingTreeView = false;
            }
        }

        /// <summary>
        /// Ribbon按钮点击事件
        /// </summary>
        private void RibbonButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton button && button.Tag is string tag)
            {
                m_ActiveMode = tag == "ReferencedBy" ? ViewMode.ReferencedBy : ViewMode.ReferencesTo;
                RefreshTreeView();
                UpdateGUIState();
            }
        }

        /// <summary>
        /// DevExpress TreeListView行焦点改变事件
        /// </summary>
        private void OnFocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
        {
            // **关键修复：防止RefreshTreeView期间触发SelectionChanged**
            // RefreshTreeView会设置ItemsSource和清除FocusedRowHandle，这两个操作都会触发FocusedRowChanged事件
            // 但Unity的行为是：RefreshTreeView不应该改变SelectionDetails的内容
            // 只有用户主动点击Referenced By树中的节点时才应该更新SelectionDetails
            if (m_IsRefreshingTreeView)
                return;

            var view = sender as TreeListView;
            if (view != null && e.NewRow is PathsToRootTreeNode node && m_CachedSnapshot != null)
            {
                var sourceLink = node.Data.GetSourceLink(m_CachedSnapshot);
                SelectionChanged?.Invoke(sourceLink);
            }
        }

        /// <summary>
        /// 循环引用图标点击事件
        /// 参考: Unity.MemoryProfiler.Editor.UI.PathsToRoot.PathsToRootDetailView.OnCircularReferenceClick
        /// </summary>
        private void CircularRefIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PathsToRootTreeNode node)
            {
                if (node.CircularRefId >= 0)
                {
                    // 查找并跳转到原始节点
                    JumpToNodeById(node.CircularRefId);
                }
            }
        }

        /// <summary>
        /// 跳转到指定ID的节点
        /// Unity实现：遍历TreeView找到节点，展开路径，选中并滚动到视图中
        /// DevExpress实现：直接设置FocusedRowHandle会自动展开并滚动
        /// </summary>
        private void JumpToNodeById(int targetId)
        {
            var currentRoot = m_ActiveMode == ViewMode.ReferencedBy ? m_ReferencedByRoot : m_ReferencesToRoot;

            if (currentRoot == null || currentRoot.Children.Count == 0)
                return;

            // 1. 查找目标节点（递归搜索）
            PathsToRootTreeNode? targetNode = null;

            foreach (var rootChild in currentRoot.Children)
            {
                targetNode = FindNodeByIdSimple(rootChild, targetId);
                if (targetNode != null)
                    break;
            }

            if (targetNode == null)
            {
                MessageBox.Show($"Could not find the original reference node (ID: {targetId})",
                    "Circular Reference",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            // 2. 选中目标节点（DevExpress会自动展开路径并滚动）
            SelectAndScrollToNode(targetNode);
        }

        /// <summary>
        /// 简化的递归查找节点方法（不记录路径）
        /// </summary>
        private PathsToRootTreeNode? FindNodeByIdSimple(PathsToRootTreeNode node, int targetId)
        {
            if (node.Id == targetId)
                return node;

            if (node.Children != null && node.Children.Count > 0)
            {
                foreach (var child in node.Children)
                {
                    var result = FindNodeByIdSimple(child, targetId);
                    if (result != null)
                        return result;
                }
            }

            return null;
        }

        /// <summary>
        /// 选中并滚动到目标节点
        /// DevExpress TreeListControl: 需要先展开父节点路径，然后选中
        /// </summary>
        private void SelectAndScrollToNode(PathsToRootTreeNode targetNode)
        {
            var view = PathsTreeList.View as DevExpress.Xpf.Grid.TreeListView;
            if (view == null)
                return;

            // 构建从根到目标节点的路径
            var path = new List<PathsToRootTreeNode>();
            BuildPathToNode(targetNode, path);

            if (path.Count == 0)
                return;

            // 从根开始逐层展开并查找
            ExpandAndSelectPath(path);
        }

        /// <summary>
        /// 构建从根到目标节点的路径（通过Parent引用向上遍历）
        /// </summary>
        private void BuildPathToNode(PathsToRootTreeNode targetNode, List<PathsToRootTreeNode> path)
        {
            var current = targetNode;
            while (current != null)
            {
                path.Insert(0, current); // 插入到列表开头，保持从根到目标的顺序
                current = current.Parent;
            }
        }

        /// <summary>
        /// 逐层展开路径并最终选中目标节点
        /// </summary>
        private void ExpandAndSelectPath(List<PathsToRootTreeNode> path)
        {
            if (path.Count == 0)
                return;

            var view = PathsTreeList.View as DevExpress.Xpf.Grid.TreeListView;
            if (view == null)
                return;

            // 获取TreeView的根节点集合
            var currentRoot = m_ActiveMode == ViewMode.ReferencedBy ? m_ReferencedByRoot : m_ReferencesToRoot;
            if (currentRoot?.Children == null)
                return;

            // 逐层展开：从第一层开始，每次展开一层父节点
            for (int i = 0; i < path.Count; i++)
            {
                var node = path[i];

                // 在当前可见行中查找节点
                int rowHandle = FindVisibleRowByNode(node);

                if (rowHandle != DevExpress.Xpf.Grid.DataControlBase.InvalidRowHandle)
                {
                    // 如果这不是最后一个节点（目标节点），展开它
                    if (i < path.Count - 1)
                    {
                        // 展开节点（ExpandNode在已展开时调用不会有问题）
                        view.ExpandNode(rowHandle);

                        // 等待UI更新
                        System.Windows.Application.Current.Dispatcher.Invoke(
                            System.Windows.Threading.DispatcherPriority.Background,
                            new System.Action(() => { }));
                    }
                    else
                    {
                        // 这是目标节点，选中它
                        view.FocusedRowHandle = rowHandle;
                        HighlightNodeTemporarily(rowHandle);
                    }
                }
            }
        }

        /// <summary>
        /// 在当前可见行中查找节点
        /// </summary>
        private int FindVisibleRowByNode(PathsToRootTreeNode targetNode)
        {
            // 遍历所有行（包括展开和折叠的）
            var dataControl = PathsTreeList;
            int rowCount = dataControl.VisibleRowCount;

            for (int rowHandle = 0; rowHandle < rowCount; rowHandle++)
            {
                var rowData = dataControl.GetRow(rowHandle);
                if (rowData is PathsToRootTreeNode node && node.Id == targetNode.Id)
                {
                    return rowHandle;
                }
            }

            return DevExpress.Xpf.Grid.DataControlBase.InvalidRowHandle;
        }

        /// <summary>
        /// 临时高亮节点（视觉反馈）
        /// 使用动画效果短暂改变背景色
        /// </summary>
        private void HighlightNodeTemporarily(int rowHandle)
        {
            // DevExpress TreeListControl 通过选中已经提供了高亮
            // 这里可以添加额外的视觉效果，如闪烁动画
            // 暂时使用简单的延迟来确保用户注意到选中
            System.Threading.Tasks.Task.Delay(100).ContinueWith(_ =>
            {
                Dispatcher.Invoke(() =>
                {
                    // 可以在这里添加额外的视觉效果
                    // 例如：改变选中行的背景色并渐变回原色
                });
            });
        }

        #region 后台线程数据收集方法（不创建UI对象）

        /// <summary>
        /// 在后台线程收集Referenced By数据
        /// 注意：不创建PathsToRootTreeNode，只收集ObjectData
        /// </summary>
        private ReferenceData SearchForReferencedByData(CachedSnapshot snapshot, SourceIndex source)
        {
            var root = new ReferenceData { Data = default };

            if (!source.Valid) return root;

            // 获取所有引用当前对象的对象
            m_CachedObjectDataList.Clear();
            m_ReferenceSearchAccelerator.Clear();
            ObjectConnection.GetAllReferencingObjects(snapshot, source, ref m_CachedObjectDataList, m_ReferenceSearchAccelerator);

            if (m_CachedObjectDataList.Count == 0)
            {
                return root;
            }

            // 构建引用数据树
            foreach (var objectData in m_CachedObjectDataList)
            {
                root.Children.Add(new ReferenceData { Data = objectData });
            }

            // 递归展开每个子节点（限制展开数量）
            var queue = new Queue<ReferenceData>();
            foreach (var child in root.Children)
            {
                queue.Enqueue(child);
            }

            int processedCount = 0;
            const int MaxProcessedObjects = 1000;

            while (queue.Count > 0 && processedCount < MaxProcessedObjects && !m_CancelSearch)
            {
                var current = queue.Dequeue();
                processedCount++;

                // 获取当前节点的引用
                var tempList = new List<ObjectData>();
                var tempAccelerator = new HashSet<SourceIndex>();
                current.Data.GetAllReferencingObjects(snapshot, ref tempList, tempAccelerator);

                foreach (var connection in tempList)
                {
                    if (connection.IsUnknownDataType()) continue;

                    var child = new ReferenceData { Data = connection.displayObject };
                    current.Children.Add(child);
                    queue.Enqueue(child);
                }
            }

            return root;
        }

        /// <summary>
        /// 在后台线程收集References To数据
        /// </summary>
        private ReferenceData SearchForReferencesToData(CachedSnapshot snapshot, SourceIndex source)
        {
            var root = new ReferenceData { Data = default };

            if (!source.Valid) return root;

            // 获取当前对象引用的所有对象
            m_CachedObjectDataList.Clear();
            m_ReferenceSearchAccelerator.Clear();
            ObjectConnection.GetAllReferencedObjects(snapshot, source, ref m_CachedObjectDataList, foundSourceIndices: m_ReferenceSearchAccelerator);

            if (m_CachedObjectDataList.Count == 0)
            {
                return root;
            }

            // 构建引用数据树
            foreach (var objectData in m_CachedObjectDataList)
            {
                root.Children.Add(new ReferenceData { Data = objectData });
            }

            // References To通常只显示一层，不递归展开
            return root;
        }

        /// <summary>
        /// 在UI线程上从数据构建PathsToRootTreeNode树
        /// 这是关键：所有PathsToRootTreeNode对象必须在UI线程上创建
        /// </summary>
        private PathsToRootTreeNode BuildTreeFromData(ReferenceData data, CachedSnapshot snapshot)
        {
            // 创建根节点（在UI线程）
            var root = new PathsToRootTreeNode();

            // 递归构建子节点
            foreach (var childData in data.Children)
            {
                var childNode = BuildNodeFromData(childData, snapshot, null);
                root.AddChild(childNode);
            }

            return root;
        }

        /// <summary>
        /// 递归构建单个节点
        /// </summary>
        private PathsToRootTreeNode BuildNodeFromData(ReferenceData data, CachedSnapshot snapshot, PathsToRootTreeNode? parent)
        {
            // 在UI线程上创建PathsToRootTreeNode（构造函数会自动检测循环引用）
            // 使用 truncateTypeNames: true 来保持与 Referenced By 一致的显示风格
            var node = new PathsToRootTreeNode(data.Data, snapshot, parent, truncateTypeNames: true);

            // 递归构建子节点
            foreach (var childData in data.Children)
            {
                var childNode = BuildNodeFromData(childData, snapshot, node);
                node.AddChild(childNode);
            }

            return node;
        }

        #endregion
    }
}

