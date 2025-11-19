using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// WPF树节点基类，支持INotifyPropertyChanged和双向绑定
    /// 适配WPF TreeView/TreeListView的MVVM模式
    /// </summary>
    /// <typeparam name="TData">节点数据类型</typeparam>
    public class TreeNode<TData> : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private bool _isVisible = true;
        private TData _data;
        private object? _source;

        public TreeNode()
        {
            Children = new ObservableCollection<TreeNode<TData>>();
        }

        public TreeNode(TData data) : this()
        {
            _data = data;
        }

        /// <summary>
        /// 节点数据
        /// </summary>
        public TData Data
        {
            get => _data;
            set => SetProperty(ref _data, value);
        }

        /// <summary>
        /// 父节点
        /// </summary>
        public TreeNode<TData> Parent { get; set; }

        /// <summary>
        /// 子节点集合（ObservableCollection支持WPF绑定）
        /// </summary>
        public ObservableCollection<TreeNode<TData>> Children { get; }

        /// <summary>
        /// 数据源（用于Selection Details等功能）
        /// 使用object类型以避免可访问性问题，实际类型通常为SourceIndex
        /// </summary>
        public object? Source
        {
            get => _source;
            set => SetProperty(ref _source, value);
        }
        
        /// <summary>
        /// 获取Source作为SourceIndex（辅助方法）
        /// </summary>
        internal SourceIndex GetSourceIndex()
        {
            return _source is SourceIndex src ? src : default;
        }

        /// <summary>
        /// 是否展开（支持WPF双向绑定）
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// 是否选中（支持WPF双向绑定）
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 是否可见（用于过滤）
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value);
        }

        /// <summary>
        /// 是否有子节点
        /// </summary>
        public bool HasChildren => Children != null && Children.Count > 0;

        /// <summary>
        /// 节点深度（从根节点开始为0）
        /// </summary>
        public int Depth
        {
            get
            {
                int depth = 0;
                var current = Parent;
                while (current != null)
                {
                    depth++;
                    current = current.Parent;
                }
                return depth;
            }
        }

        /// <summary>
        /// 从根节点到当前节点的路径
        /// </summary>
        public List<TreeNode<TData>> Path
        {
            get
            {
                var path = new List<TreeNode<TData>>();
                var current = this;
                while (current != null)
                {
                    path.Insert(0, current);
                    current = current.Parent;
                }
                return path;
            }
        }

        /// <summary>
        /// 添加子节点
        /// </summary>
        public void AddChild(TreeNode<TData> child)
        {
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            child.Parent = this;
            Children.Add(child);
            OnPropertyChanged(nameof(HasChildren));
        }

        /// <summary>
        /// 移除子节点
        /// </summary>
        public void RemoveChild(TreeNode<TData> child)
        {
            if (child == null)
                return;

            if (Children.Remove(child))
            {
                child.Parent = null;
                OnPropertyChanged(nameof(HasChildren));
            }
        }

        /// <summary>
        /// 清空所有子节点
        /// </summary>
        public void ClearChildren()
        {
            foreach (var child in Children)
            {
                child.Parent = null;
            }
            Children.Clear();
            OnPropertyChanged(nameof(HasChildren));
        }

        /// <summary>
        /// 递归查找所有后代节点
        /// </summary>
        public IEnumerable<TreeNode<TData>> GetDescendants()
        {
            foreach (var child in Children)
            {
                yield return child;
                foreach (var descendant in child.GetDescendants())
                {
                    yield return descendant;
                }
            }
        }

        /// <summary>
        /// 查找满足条件的第一个后代节点
        /// </summary>
        public TreeNode<TData> FindDescendant(Predicate<TreeNode<TData>> predicate)
        {
            foreach (var child in Children)
            {
                if (predicate(child))
                    return child;

                var found = child.FindDescendant(predicate);
                if (found != null)
                    return found;
            }
            return null;
        }

        /// <summary>
        /// 查找所有满足条件的后代节点
        /// </summary>
        public List<TreeNode<TData>> FindDescendants(Predicate<TreeNode<TData>> predicate)
        {
            var results = new List<TreeNode<TData>>();
            foreach (var child in Children)
            {
                if (predicate(child))
                    results.Add(child);

                results.AddRange(child.FindDescendants(predicate));
            }
            return results;
        }

        /// <summary>
        /// 展开到指定深度
        /// </summary>
        public void ExpandToDepth(int targetDepth)
        {
            if (Depth < targetDepth)
            {
                IsExpanded = true;
                foreach (var child in Children)
                {
                    child.ExpandToDepth(targetDepth);
                }
            }
        }

        /// <summary>
        /// 全部展开
        /// </summary>
        public void ExpandAll()
        {
            IsExpanded = true;
            foreach (var child in Children)
            {
                child.ExpandAll();
            }
        }

        /// <summary>
        /// 全部折叠
        /// </summary>
        public void CollapseAll()
        {
            IsExpanded = false;
            foreach (var child in Children)
            {
                child.CollapseAll();
            }
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion

        public override string ToString()
        {
            return $"TreeNode(Depth={Depth}, HasChildren={HasChildren}, IsExpanded={IsExpanded})";
        }
    }
}

