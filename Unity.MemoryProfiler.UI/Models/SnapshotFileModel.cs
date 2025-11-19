using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 快照文件元数据模型
    /// 基于Unity官方SnapshotFileModel
    /// </summary>
    public class SnapshotFileModel : INotifyPropertyChanged
    {
        private bool _isBase;
        private bool _isCompared;

        public string FullPath { get; set; } = "";
        public string Name { get; set; } = "";
        public DateTime Date { get; set; }
        public long Size { get; set; }
        public uint SessionGUID { get; set; }
        public string ProductName { get; set; } = "";
        public string Platform { get; set; } = "";
        public string UnityVersion { get; set; } = "";
        
        /// <summary>
        /// 是否是Base快照（对比模式中的A）
        /// </summary>
        public bool IsBase
        {
            get => _isBase;
            set
            {
                if (_isBase != value)
                {
                    _isBase = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 是否是Compared快照（对比模式中的B）
        /// </summary>
        public bool IsCompared
        {
            get => _isCompared;
            set
            {
                if (_isCompared != value)
                {
                    _isCompared = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 格式化的文件大小
        /// </summary>
        public string SizeFormatted
        {
            get
            {
                if (Size < 1024)
                    return $"{Size} B";
                else if (Size < 1024 * 1024)
                    return $"{Size / 1024.0:F2} KB";
                else if (Size < 1024 * 1024 * 1024)
                    return $"{Size / (1024.0 * 1024.0):F2} MB";
                else
                    return $"{Size / (1024.0 * 1024.0 * 1024.0):F2} GB";
            }
        }

        /// <summary>
        /// 格式化的日期时间
        /// </summary>
        public string DateFormatted => Date.ToString("yyyy-MM-dd HH:mm:ss");

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

