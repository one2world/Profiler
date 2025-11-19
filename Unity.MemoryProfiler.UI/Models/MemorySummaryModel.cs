using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.UI.Models;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Memory summary model for displaying memory breakdown in charts and tables.
    /// Equivalent to Unity's MemorySummaryModel (Editor/UI/Analysis/Breakdowns/Summary/Data/MemorySummaryModel.cs)
    /// </summary>
    internal class MemorySummaryModel : INotifyPropertyChanged
    {
        private string _title;
        private string _description;
        private bool _isCompareMode;
        private long _totalA;
        private long _totalB;
        private string _warningMessage;

        public MemorySummaryModel(
            string title,
            string description,
            bool isCompareMode,
            long totalA,
            long totalB,
            List<Row> rows,
            string warningMessage)
        {
            _title = title;
            _description = description;
            _isCompareMode = isCompareMode;
            _totalA = totalA;
            _totalB = totalB;
            Rows = rows ?? new List<Row>();
            _warningMessage = warningMessage;
        }

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => _description;
            set { _description = value; OnPropertyChanged(); }
        }

        public bool IsCompareMode
        {
            get => _isCompareMode;
            set { _isCompareMode = value; OnPropertyChanged(); }
        }

        public long TotalA
        {
            get => _totalA;
            set { _totalA = value; OnPropertyChanged(); }
        }

        public long TotalB
        {
            get => _totalB;
            set { _totalB = value; OnPropertyChanged(); }
        }

        public List<Row> Rows { get; }

        public string WarningMessage
        {
            get => _warningMessage;
            set { _warningMessage = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Memory category row data
        /// Equivalent to Unity's MemorySummaryModel.Row
        /// </summary>
        public class Row : INotifyPropertyChanged
        {
            public Row(
                string name,
                MemorySize valueA,
                MemorySize valueB,
                string categoryId,
                string description,
                string documentationUrl)
            {
                Name = name;
                ValueA = valueA;
                ValueB = valueB;
                CategoryId = categoryId;
                Description = description;
                DocumentationUrl = documentationUrl;
                SortPriority = RowSortPriority.Normal;
                ResidentSizeUnavailable = false;
            }

            public string Name { get; set; }
            public MemorySize ValueA { get; set; }
            public MemorySize ValueB { get; set; }
            public string CategoryId { get; set; }
            public string Description { get; set; }
            public string DocumentationUrl { get; set; }

            /// <summary>
            /// For selection integration (Native, Managed, Graphics, etc.)
            /// </summary>
            public IAnalysisViewSelectable.Category? CategoryIdEnum { get; set; }

            /// <summary>
            /// Sort priority for displaying rows
            /// </summary>
            public RowSortPriority SortPriority { get; set; }

            /// <summary>
            /// Whether resident size is available for this category
            /// </summary>
            public bool ResidentSizeUnavailable { get; set; }

            // Formatted properties for UI binding
            public string FormattedCommittedA => FormatMemorySize(ValueA.Committed);
            public string FormattedResidentA => FormatMemorySize(ValueA.Resident);
            public string FormattedCommittedB => FormatMemorySize(ValueB.Committed);
            public string FormattedResidentB => FormatMemorySize(ValueB.Resident);
            public string FormattedDelta => FormatMemorySize(ValueB.Committed - ValueA.Committed);

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private string FormatMemorySize(ulong bytes)
            {
                return MemoryItemData.FormatBytes((long)bytes);
            }
        }

        /// <summary>
        /// Priority for sorting rows in the display
        /// </summary>
        public enum RowSortPriority
        {
            High,
            Normal,
            Low,
            ShowLast
        }
    }

    /// <summary>
    /// Interface for building memory summary models
    /// Equivalent to Unity's IMemorySummaryModelBuilder
    /// </summary>
    internal interface IMemorySummaryModelBuilder<out T> where T : MemorySummaryModel
    {
        T Build();
    }

    /// <summary>
    /// Selectable category for navigation/filtering
    /// Equivalent to Unity's IAnalysisViewSelectable.Category
    /// </summary>
    internal interface IAnalysisViewSelectable
    {
        enum Category
        {
            Native,
            Managed,
            Graphics,
            ExecutablesAndMapped,
            Unknown
        }
    }
}

