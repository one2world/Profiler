using System.Windows.Controls;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.ViewModels.SelectionDetails;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Controls
{
    /// <summary>
    /// 选择详情面板 - 使用 MVVM 模式显示所选项目的详细信息
    /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsPanel
    /// </summary>
    public partial class SelectionDetailsPanel : UserControl
    {
        private CachedSnapshot? m_Snapshot;
        private Services.SelectedItemDetailsBuilder? m_DetailsBuilder;

        /// <summary>
        /// ViewModel
        /// </summary>
        public SelectionDetailsViewModel ViewModel { get; }

        /// <summary>
        /// 适配器（用于向后兼容旧 API）
        /// </summary>
        public SelectionDetailsPanelAdapter Adapter { get; }

        /// <summary>
        /// Details Builder (用于向后兼容)
        /// </summary>
        internal Services.SelectedItemDetailsBuilder? DetailsBuilder => m_DetailsBuilder;

        public SelectionDetailsPanel()
        {
            InitializeComponent();
            
            // 创建并设置 ViewModel
            ViewModel = new SelectionDetailsViewModel();
            DataContext = ViewModel;
            
            // 创建适配器
            Adapter = new SelectionDetailsPanelAdapter(this);
            
            // 订阅PathsToRootView的选择变化事件
            PathsToRootViewControl.SelectionChanged += OnReferencesSelectionChanged;
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
            ViewModel.Clear();
            
            // 清空并隐藏特殊控件
            HideManagedObjectInspector();
            HideReferences();
        }

        /// <summary>
        /// 设置引用浏览器的根对象
        /// </summary>
        internal void SetupReferences(SourceIndex source)
        {
            if (m_Snapshot == null)
            {
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
            ReferencesExpander.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// 隐藏引用浏览器
        /// </summary>
        public void HideReferences()
        {
            PathsToRootViewControl.ClearSelection();
            ReferencesExpander.Visibility = System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// 设置Managed对象检查器的内容
        /// </summary>
        public void SetupManagedObjectInspector(System.Collections.Generic.List<ManagedFieldInfo> fields)
        {
            if (fields == null || fields.Count == 0)
            {
                HideManagedObjectInspector();
                return;
            }

            ManagedObjectInspectorControl.SetupManagedObject(fields);
            ManagedFieldsExpander.Visibility = System.Windows.Visibility.Visible;
        }

        /// <summary>
        /// 隐藏Managed对象检查器
        /// </summary>
        public void HideManagedObjectInspector()
        {
            ManagedObjectInspectorControl.Clear();
            ManagedFieldsExpander.Visibility = System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// 检查对象是否有引用数据
        /// </summary>
        private static bool HasReferencesData(CachedSnapshot snapshot, SourceIndex sourceIndex)
        {
            if (!sourceIndex.Valid)
                return false;

            var references = new System.Collections.Generic.List<ObjectData>();
            ObjectConnection.GetAllReferencingObjects(snapshot, sourceIndex, ref references);
            var refCount = references.Count;
            ObjectConnection.GetAllReferencedObjects(snapshot, sourceIndex, ref references);
            return refCount + references.Count > 0;
        }

        /// <summary>
        /// 处理引用浏览器中的选择变化事件
        /// </summary>
        private void OnReferencesSelectionChanged(SourceIndex selectedSource)
        {
            if (m_Snapshot == null || !selectedSource.Valid)
                return;

            // 使用SelectedItemDetailsBuilder系统处理选择
            if (m_DetailsBuilder != null)
            {
                m_DetailsBuilder.SetSelection(selectedSource);
                return;
            }
        }
    }
}
