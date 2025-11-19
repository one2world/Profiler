using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Model for snapshot issues (warnings, errors, infos).
    /// 对应Unity的SnapshotIssuesModel (Line 7-30)
    /// 100%等价实现
    /// </summary>
    internal class SnapshotIssuesModel : INotifyPropertyChanged
    {
        public SnapshotIssuesModel(List<Issue> issues)
        {
            Issues = new ObservableCollection<Issue>(issues ?? new List<Issue>());
        }

        public ObservableCollection<Issue> Issues { get; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Issue severity level
        /// </summary>
        public enum IssueLevel
        {
            Info,
            Warning,
            Error,
        }

        /// <summary>
        /// Individual issue data
        /// </summary>
        public struct Issue
        {
            public IssueLevel IssueLevel;
            public string Summary;
            public string Details;
        }
    }
}

