using System.Windows.Controls;
using Unity.MemoryProfiler.UI.ViewModels;

namespace Unity.MemoryProfiler.UI.Views
{
    /// <summary>
    /// ComparisonView.xaml 的交互逻辑
    /// </summary>
    public partial class ComparisonView : UserControl
    {
        public ComparisonView()
        {
            InitializeComponent();
            DataContext = new ComparisonViewModel();
        }

        public ComparisonViewModel ViewModel => (ComparisonViewModel)DataContext;
    }
}

