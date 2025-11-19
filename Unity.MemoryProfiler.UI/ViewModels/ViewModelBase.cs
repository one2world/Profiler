using CommunityToolkit.Mvvm.ComponentModel;

namespace Unity.MemoryProfiler.UI.ViewModels
{
    /// <summary>
    /// ViewModel基类，使用CommunityToolkit.Mvvm提供的ObservableObject
    /// 自动实现INotifyPropertyChanged，简化属性通知代码
    /// </summary>
    /// <remarks>
    /// 等价于Unity的ViewController基类，但采用WPF MVVM模式
    /// 参考：UnityMemoryProfilerWPF\References\com.unity.memoryprofiler@1.1.6\Editor\UI\ViewController.cs
    /// </remarks>
    public abstract class ViewModelBase : ObservableObject
    {
        // ObservableObject已提供：
        // - INotifyPropertyChanged实现
        // - OnPropertyChanged()方法
        // - SetProperty<T>()方法
        // - [ObservableProperty] attribute支持

        // 派生类可以直接使用：
        // 1. [ObservableProperty] private string _fieldName; // 自动生成FieldName属性
        // 2. 手动使用SetProperty(ref _field, value)
        // 3. 手动调用OnPropertyChanged()
    }
}

