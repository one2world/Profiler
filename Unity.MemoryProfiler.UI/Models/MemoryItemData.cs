using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Unity.MemoryProfiler.Editor.UI.Models
{
    /// <summary>
    /// 内存项数据类，用于AllTrackedMemory等视图的TreeNode数据
    /// 对应Unity的AllTrackedMemoryModel中的数据项
    /// </summary>
    public class MemoryItemData : INotifyPropertyChanged
    {
        private string _name;
        private long _size;
        private double _percentage;
        private IconType _iconType;
        private string _type;
        private long _nativeSize;
        private long _managedSize;
        private long _graphicsSize;
        private object _userData;

        public MemoryItemData()
        {
        }

        public MemoryItemData(string name, long size)
        {
            _name = name;
            _size = size;
        }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>
        /// 大小（字节）
        /// </summary>
        public long Size
        {
            get => _size;
            set
            {
                if (SetProperty(ref _size, value))
                {
                    OnPropertyChanged(nameof(SizeFormatted));
                }
            }
        }

        /// <summary>
        /// 百分比（0-100）
        /// </summary>
        public double Percentage
        {
            get => _percentage;
            set
            {
                if (SetProperty(ref _percentage, value))
                {
                    OnPropertyChanged(nameof(PercentageFormatted));
                }
            }
        }

        /// <summary>
        /// 图标类型（用于UI显示）
        /// </summary>
        public IconType IconType
        {
            get => _iconType;
            set => SetProperty(ref _iconType, value);
        }

        /// <summary>
        /// 类型名称
        /// </summary>
        public string Type
        {
            get => _type;
            set => SetProperty(ref _type, value);
        }

        /// <summary>
        /// Native内存大小
        /// </summary>
        public long NativeSize
        {
            get => _nativeSize;
            set
            {
                if (SetProperty(ref _nativeSize, value))
                {
                    OnPropertyChanged(nameof(NativeSizeFormatted));
                }
            }
        }

        /// <summary>
        /// Managed内存大小
        /// </summary>
        public long ManagedSize
        {
            get => _managedSize;
            set
            {
                if (SetProperty(ref _managedSize, value))
                {
                    OnPropertyChanged(nameof(ManagedSizeFormatted));
                }
            }
        }

        /// <summary>
        /// Graphics内存大小
        /// </summary>
        public long GraphicsSize
        {
            get => _graphicsSize;
            set
            {
                if (SetProperty(ref _graphicsSize, value))
                {
                    OnPropertyChanged(nameof(GraphicsSizeFormatted));
                }
            }
        }

        /// <summary>
        /// 用户自定义数据（如SourceIndex等）
        /// </summary>
        public object UserData
        {
            get => _userData;
            set => SetProperty(ref _userData, value);
        }

        /// <summary>
        /// 格式化的大小字符串（使用EditorUtility.FormatBytes逻辑）
        /// </summary>
        public string SizeFormatted => FormatBytes(Size);

        /// <summary>
        /// 格式化的百分比字符串
        /// </summary>
        public string PercentageFormatted => $"{Percentage:F2}%";

        /// <summary>
        /// 格式化的Native大小
        /// </summary>
        public string NativeSizeFormatted => FormatBytes(NativeSize);

        /// <summary>
        /// 格式化的Managed大小
        /// </summary>
        public string ManagedSizeFormatted => FormatBytes(ManagedSize);

        /// <summary>
        /// 格式化的Graphics大小
        /// </summary>
        public string GraphicsSizeFormatted => FormatBytes(GraphicsSize);

        /// <summary>
        /// 格式化字节大小（Unity EditorUtility.FormatBytes等价实现）
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            if (bytes < 0)
                return "0 B";

            const long KB = 1024;
            const long MB = KB * 1024;
            const long GB = MB * 1024;
            const long TB = GB * 1024;

            if (bytes >= TB)
                return $"{bytes / (double)TB:F2} TB";
            if (bytes >= GB)
                return $"{bytes / (double)GB:F2} GB";
            if (bytes >= MB)
                return $"{bytes / (double)MB:F2} MB";
            if (bytes >= KB)
                return $"{bytes / (double)KB:F2} KB";
            
            return $"{bytes} B";
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
            return $"{Name} ({SizeFormatted}, {PercentageFormatted})";
        }
    }

    /// <summary>
    /// 图标类型枚举（用于UI显示）
    /// </summary>
    public enum IconType
    {
        None,
        Folder,
        NativeObject,
        ManagedObject,
        Texture,
        Mesh,
        Material,
        Shader,
        AudioClip,
        GameObject,
        Component,
        Script,
        Asset,
        System,
        Reserved,
        Untracked,
        Graphics,
        Executable
    }
}

