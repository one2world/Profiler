using System;
using System.Windows.Input;

namespace Unity.MemoryProfiler.UI.ViewModels.SelectionDetails
{
    /// <summary>
    /// Call Stacks Section ViewModel
    /// 显示调用堆栈信息
    /// </summary>
    public class CallStacksViewModel : SectionViewModel
    {
        private string _callStackText = string.Empty;
        private string _callStackPlainText = string.Empty;
        private bool _showAddress = true;
        private bool _clickableCallStacks = false;
        private int _callStackCount = 0;
        private ulong _objectAddress;

        /// <summary>
        /// 调用堆栈文本（富文本格式）
        /// </summary>
        public string CallStackText
        {
            get => _callStackText;
            set => SetProperty(ref _callStackText, value);
        }

        /// <summary>
        /// 调用堆栈纯文本（用于复制）
        /// </summary>
        public string CallStackPlainText
        {
            get => _callStackPlainText;
            set => SetProperty(ref _callStackPlainText, value);
        }

        /// <summary>
        /// 是否显示地址
        /// </summary>
        public bool ShowAddress
        {
            get => _showAddress;
            set
            {
                if (SetProperty(ref _showAddress, value))
                {
                    // 触发重新格式化
                    OnShowAddressChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// 是否可点击（WPF 中不可用，但保留 UI）
        /// </summary>
        public bool ClickableCallStacks
        {
            get => _clickableCallStacks;
            set => SetProperty(ref _clickableCallStacks, value);
        }

        /// <summary>
        /// 调用堆栈数量
        /// </summary>
        public int CallStackCount
        {
            get => _callStackCount;
            set => SetProperty(ref _callStackCount, value);
        }

        /// <summary>
        /// 对象地址（用于重新格式化）
        /// </summary>
        public ulong ObjectAddress
        {
            get => _objectAddress;
            set => SetProperty(ref _objectAddress, value);
        }

        /// <summary>
        /// 复制命令
        /// </summary>
        public ICommand CopyCommand { get; }

        /// <summary>
        /// ShowAddress 改变时的回调
        /// </summary>
        public Action? OnShowAddressChanged { get; set; }

        public CallStacksViewModel()
        {
            CopyCommand = new RelayCommand(CopyCallStack);
        }

        public override void Clear()
        {
            CallStackText = string.Empty;
            CallStackPlainText = string.Empty;
            CallStackCount = 0;
            ObjectAddress = 0;
            Hide();
        }

        private void CopyCallStack()
        {
            if (!string.IsNullOrEmpty(CallStackPlainText))
            {
                System.Windows.Clipboard.SetText(CallStackPlainText);
            }
        }
    }

    /// <summary>
    /// 简单的 RelayCommand 实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }
}

