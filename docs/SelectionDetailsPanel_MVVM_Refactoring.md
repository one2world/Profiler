# SelectionDetailsPanel MVVM 重构设计

## 设计目标

完全移除动态 UI 创建，使用 WPF MVVM 模式：
1. **静态 UI** - XAML 中预先设计所有可能的 UI 元素
2. **数据绑定** - 使用 `Binding` 连接 ViewModel 和 View
3. **Visibility 绑定** - 通过 `Visibility` 属性控制显示/隐藏
4. **INotifyPropertyChanged** - 自动通知 UI 更新

## 架构设计

### 1. ViewModel 层次结构

```
SelectionDetailsViewModel (根 ViewModel)
├── BasicInfoViewModel          // 基本信息
├── MemoryInfoViewModel         // 内存信息
├── DescriptionViewModel        // 描述
├── MetaDataViewModel           // 元数据
├── HelpViewModel              // 帮助
├── AdvancedInfoViewModel      // 高级信息
├── CallStacksViewModel        // 调用堆栈
├── ManagedFieldsViewModel     // Managed 字段
└── ReferencesViewModel        // 引用关系
```

### 2. ViewModel 基类

```csharp
public abstract class SectionViewModel : INotifyPropertyChanged
{
    private Visibility _visibility = Visibility.Collapsed;
    
    public Visibility Visibility
    {
        get => _visibility;
        set { _visibility = value; OnPropertyChanged(); }
    }
    
    public void Show() => Visibility = Visibility.Visible;
    public void Hide() => Visibility = Visibility.Collapsed;
    
    public abstract void Clear();
}
```

### 3. 具体 Section ViewModel 示例

#### BasicInfoViewModel
```csharp
public class BasicInfoViewModel : SectionViewModel
{
    // 属性列表
    public ObservableCollection<PropertyItem> Properties { get; }
    
    public override void Clear()
    {
        Properties.Clear();
        Hide();
    }
    
    public void AddProperty(string label, string value, string tooltip = null)
    {
        Properties.Add(new PropertyItem 
        { 
            Label = label, 
            Value = value, 
            Tooltip = tooltip 
        });
        Show();
    }
}

public class PropertyItem
{
    public string Label { get; set; }
    public string Value { get; set; }
    public string Tooltip { get; set; }
}
```

#### MemoryInfoViewModel
```csharp
public class MemoryInfoViewModel : SectionViewModel
{
    // 通用属性
    public ObservableCollection<PropertyItem> Properties { get; }
    
    // 特定属性（可选，用于特殊格式化）
    private string _allocatedSize;
    public string AllocatedSize
    {
        get => _allocatedSize;
        set { _allocatedSize = value; OnPropertyChanged(); }
    }
    
    private string _residentSize;
    public string ResidentSize
    {
        get => _residentSize;
        set { _residentSize = value; OnPropertyChanged(); }
    }
}
```

#### CallStacksViewModel
```csharp
public class CallStacksViewModel : SectionViewModel
{
    private string _callStackText;
    public string CallStackText
    {
        get => _callStackText;
        set { _callStackText = value; OnPropertyChanged(); }
    }
    
    private bool _showAddress = true;
    public bool ShowAddress
    {
        get => _showAddress;
        set { _showAddress = value; OnPropertyChanged(); OnShowAddressChanged(); }
    }
    
    private bool _clickableCallStacks = false;
    public bool ClickableCallStacks
    {
        get => _clickableCallStacks;
        set { _clickableCallStacks = value; OnPropertyChanged(); }
    }
    
    public ICommand CopyCommand { get; }
    public ICommand ToggleAddressCommand { get; }
    public ICommand ToggleClickableCommand { get; }
}
```

### 4. XAML 设计

#### 基本信息 Section
```xml
<Expander Header="Basic Information" 
          IsExpanded="True"
          Visibility="{Binding BasicInfo.Visibility}">
    <ItemsControl ItemsSource="{Binding BasicInfo.Properties}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Grid Margin="0,3">
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="120"/>
                        <ColumnDefinition Width="*"/>
                    </Grid.ColumnDefinitions>
                    <TextBlock Text="{Binding Label}" 
                               FontWeight="SemiBold"
                               ToolTip="{Binding Tooltip}"/>
                    <TextBlock Grid.Column="1" 
                               Text="{Binding Value}"
                               TextWrapping="Wrap"
                               ToolTip="{Binding Tooltip}"/>
                </Grid>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</Expander>
```

#### 调用堆栈 Section
```xml
<Expander Header="Call Stack Info"
          IsExpanded="True"
          Visibility="{Binding CallStacks.Visibility}">
    <StackPanel>
        <!-- Copy 按钮 -->
        <Button Content="Copy Call Stack"
                Command="{Binding CallStacks.CopyCommand}"
                Margin="0,5"/>
        
        <!-- 计数 -->
        <TextBlock Text="Call Stacks: 1" Margin="0,5"/>
        
        <!-- Toggle 开关 -->
        <CheckBox Content="Clickable Call Stacks"
                  IsChecked="{Binding CallStacks.ClickableCallStacks}"
                  Margin="0,5"/>
        
        <CheckBox Content="Show Address in Call Stacks"
                  IsChecked="{Binding CallStacks.ShowAddress}"
                  Margin="0,5"/>
        
        <!-- 堆栈内容 -->
        <Expander Header="Allocation Call Stack" IsExpanded="False">
            <TextBlock Text="{Binding CallStacks.CallStackText}"
                       TextWrapping="Wrap"
                       FontFamily="Consolas"/>
        </Expander>
    </StackPanel>
</Expander>
```

### 5. 数据流

```
Presenter/Builder 
    ↓
SelectionDetailsViewModel.Update(data)
    ↓
各个 SectionViewModel 更新属性
    ↓
INotifyPropertyChanged 触发
    ↓
WPF Binding 自动更新 UI
```

### 6. API 设计

#### SelectionDetailsViewModel
```csharp
public class SelectionDetailsViewModel : INotifyPropertyChanged
{
    public string Title { get; set; }
    
    public BasicInfoViewModel BasicInfo { get; }
    public MemoryInfoViewModel MemoryInfo { get; }
    public DescriptionViewModel Description { get; }
    public CallStacksViewModel CallStacks { get; }
    // ... 其他 Section ViewModels
    
    public void Clear()
    {
        Title = "No Selection";
        BasicInfo.Clear();
        MemoryInfo.Clear();
        Description.Clear();
        CallStacks.Clear();
        // ... 清空所有 Sections
    }
    
    // 便捷方法
    public void ShowManagedObjectDetails(ManagedObjectInfo info)
    {
        Clear();
        Title = $"Managed Object: 0x{info.Address:X}";
        
        BasicInfo.AddProperty("Managed Size", FormatBytes(info.Size));
        BasicInfo.AddProperty("Referenced By", info.RefCount.ToString());
        BasicInfo.Show();
        
        MemoryInfo.AllocatedSize = FormatBytes(info.Size);
        MemoryInfo.Show();
        
        // ...
    }
}
```

## 实施步骤

### Phase 1: 创建 ViewModel 层
1. 创建 `SectionViewModel` 基类
2. 创建各个具体的 SectionViewModel
3. 创建 `SelectionDetailsViewModel` 根 ViewModel
4. 实现 `INotifyPropertyChanged`

### Phase 2: 重新设计 XAML
1. 为每个 Section 设计完整的 UI 结构
2. 使用 `ItemsControl` 显示动态列表
3. 绑定 `Visibility` 属性
4. 绑定数据属性

### Phase 3: 更新 SelectionDetailsPanel
1. 添加 `DataContext` 为 `SelectionDetailsViewModel`
2. 移除所有动态 UI 创建代码
3. 提供简单的 API 供外部调用

### Phase 4: 更新 Presenter 和 Builder
1. 修改 `SelectedItemDetailsBuilder` 使用 ViewModel
2. 修改各个 Presenter 使用 ViewModel
3. 移除所有 `AddDynamicElement` 调用

### Phase 5: 测试和验证
1. 编译测试
2. 功能测试
3. 性能测试

## 优势

1. **完全静态** - 无动态 UI 创建，避免 Visual Tree 冲突
2. **声明式** - XAML 清晰表达 UI 结构
3. **可测试** - ViewModel 可独立测试
4. **可维护** - 数据和 UI 分离
5. **性能好** - WPF 绑定引擎优化
6. **符合 WPF 最佳实践**

## 挑战

1. **工作量大** - 需要重写大量代码
2. **复杂性** - 需要设计合理的 ViewModel 结构
3. **灵活性** - 某些动态场景可能需要特殊处理
4. **学习曲线** - 团队需要熟悉 MVVM 模式

## 下一步

开始实施 Phase 1：创建 ViewModel 层。

