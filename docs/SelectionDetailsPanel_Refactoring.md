# SelectionDetailsPanel 重构方案

## 📋 背景

### 当前问题
1. **代码组织混乱**：混合使用 XAML 预定义控件和动态创建控件
2. **WPF Visual Tree 冲突**：`GetOrCreateGroup` 尝试将已存在的 XAML Expander 重新添加到容器
3. **维护困难**：动态 UI 创建逻辑分散，难以追踪和调试
4. **性能问题**：频繁创建和销毁 WPF 控件

### 触发原因
在实现 "All Of Memory 中 Managed Objects 的 Callstack 显示" 功能时，遇到以下错误：
```
System.ArgumentException: 指定的 Visual 已经是另一个 Visual 的子级或者已经是 CompositionTarget 的根。
```

## 🎯 重构目标

### 核心原则
1. **静态布局**：在 XAML 中预定义所有可能的 UI 区域
2. **上对齐布局**：使用 StackPanel 或 Grid，隐藏的内容会自动让下方内容上移
3. **Visibility 控制**：通过 `Visibility` 属性控制显示/隐藏，不动态创建控件
4. **逻辑一致性**：确保重构后数据和显示逻辑与原有功能完全一致

### 设计方案

#### 1. XAML 结构设计

```xml
<ScrollViewer>
    <StackPanel x:Name="DetailsContent">
        <!-- 标题区域 -->
        <TextBlock x:Name="TitleTextBlock" Style="{StaticResource TitleStyle}"/>
        
        <!-- 基本信息组 -->
        <Expander x:Name="BasicInfoExpander" Header="Basic" IsExpanded="True">
            <StackPanel x:Name="BasicInfoContent"/>
        </Expander>
        
        <!-- 内存信息组 -->
        <Expander x:Name="MemoryInfoExpander" Header="Memory" IsExpanded="True">
            <StackPanel x:Name="MemoryInfoContent"/>
        </Expander>
        
        <!-- 描述组 -->
        <Expander x:Name="DescriptionExpander" Header="Description" IsExpanded="False">
            <TextBlock x:Name="DescriptionText" TextWrapping="Wrap"/>
        </Expander>
        
        <!-- 高级信息组 -->
        <Expander x:Name="AdvancedInfoExpander" Header="Advanced" IsExpanded="False">
            <StackPanel x:Name="AdvancedInfoContent"/>
        </Expander>
        
        <!-- Call Stacks 组 (新增) -->
        <Expander x:Name="CallStacksExpander" Header="Call Stack Info" IsExpanded="True">
            <StackPanel x:Name="CallStacksContent"/>
        </Expander>
        
        <!-- Managed Fields 组 -->
        <Expander x:Name="ManagedFieldsExpander" Header="Managed Fields" IsExpanded="True">
            <local:ManagedObjectInspector x:Name="ManagedObjectInspectorControl"/>
        </Expander>
        
        <!-- References 组 -->
        <Expander x:Name="ReferencesExpander" Header="References" IsExpanded="True">
            <local:PathsToRootView x:Name="PathsToRootViewControl"/>
        </Expander>
        
        <!-- 无选择提示 -->
        <TextBlock x:Name="NoSelectionMessage" Text="No Selection" 
                   Visibility="Visible" Style="{StaticResource HintStyle}"/>
    </StackPanel>
</ScrollViewer>
```

#### 2. 代码结构重构

```csharp
public partial class SelectionDetailsPanel : UserControl
{
    // 预定义的所有 Expander 和 Content
    private readonly Dictionary<string, (Expander expander, Panel content)> _sections;
    
    public SelectionDetailsPanel()
    {
        InitializeComponent();
        InitializeSections();
    }
    
    private void InitializeSections()
    {
        _sections = new Dictionary<string, (Expander, Panel)>
        {
            { GroupNameBasic, (BasicInfoExpander, BasicInfoContent) },
            { GroupNameMemory, (MemoryInfoExpander, MemoryInfoContent) },
            { GroupNameAdvanced, (AdvancedInfoExpander, AdvancedInfoContent) },
            { GroupNameCallStacks, (CallStacksExpander, CallStacksContent) },
            // ... 其他分组
        };
        
        // 初始化时全部隐藏
        HideAllSections();
    }
    
    // 显示指定分组
    public void ShowSection(string sectionName)
    {
        if (_sections.TryGetValue(sectionName, out var section))
        {
            section.expander.Visibility = Visibility.Visible;
        }
    }
    
    // 隐藏指定分组
    public void HideSection(string sectionName)
    {
        if (_sections.TryGetValue(sectionName, out var section))
        {
            section.expander.Visibility = Visibility.Collapsed;
        }
    }
    
    // 清空指定分组内容
    public void ClearSection(string sectionName)
    {
        if (_sections.TryGetValue(sectionName, out var section))
        {
            section.content.Children.Clear();
        }
    }
    
    // 添加内容到指定分组
    public void AddToSection(string sectionName, UIElement element)
    {
        if (_sections.TryGetValue(sectionName, out var section))
        {
            section.content.Children.Add(element);
            ShowSection(sectionName); // 自动显示
        }
    }
    
    // 隐藏所有分组
    private void HideAllSections()
    {
        foreach (var section in _sections.Values)
        {
            section.expander.Visibility = Visibility.Collapsed;
        }
    }
    
    // 清空所有分组
    public void ClearAllSections()
    {
        foreach (var (sectionName, section) in _sections)
        {
            section.content.Children.Clear();
            HideSection(sectionName);
        }
    }
}
```

#### 3. SelectedItemDetailsBuilder 适配

```csharp
// 旧方式（动态创建）
m_UI.AddDynamicElement(SelectionDetailsPanel.GroupNameCallStacks, 
    "Copy Call Stack", "Copy Call Stack", tooltip, 
    DynamicElementOptions.Button, onClick);

// 新方式（静态布局 + Visibility）
var button = new Button { Content = "Copy Call Stack", ... };
button.Click += (s, e) => onClick();
m_UI.AddToSection(SelectionDetailsPanel.GroupNameCallStacks, button);
```

### 重构步骤

#### Phase 1: 准备工作
1. ✅ 分析现有 SelectionDetailsPanel 的所有使用场景
2. ✅ 列出所有可能显示的 UI 区域和内容类型
3. ✅ 设计新的 XAML 布局结构

#### Phase 2: 重构实施
1. 🔲 更新 `SelectionDetailsPanel.xaml`
   - 预定义所有 Expander 和 Content 区域
   - 使用 StackPanel 实现上对齐布局
   - 初始状态全部设为 `Visibility="Collapsed"`

2. 🔲 重构 `SelectionDetailsPanel.xaml.cs`
   - 移除 `GetOrCreateGroup` 动态创建逻辑
   - 实现 `InitializeSections` 注册所有预定义区域
   - 实现 `ShowSection`、`HideSection`、`ClearSection`、`AddToSection` 方法
   - 移除 `DetailsGroup` 类的使用

3. 🔲 适配 `SelectedItemDetailsBuilder`
   - 将所有 `AddDynamicElement` 调用改为 `AddToSection`
   - 手动创建 UI 元素（Button、TextBlock、Toggle 等）
   - 确保逻辑一致性

4. 🔲 适配所有 Presenter
   - `AllTrackedMemorySelectionDetailsPresenter`
   - `UnityObjectsSelectionDetailsPresenter`
   - `ManagedObjectsSelectionDetailsPresenter`
   - `SummarySelectionDetailsPresenter`

#### Phase 3: 测试验证
1. 🔲 验证 Summary 功能
2. 🔲 验证 Unity Objects 功能
3. 🔲 验证 All Of Memory 功能
4. 🔲 验证 Managed Objects 功能
5. 🔲 验证 Diff 模式

#### Phase 4: 实现 Managed Objects Callstack
1. 🔲 恢复 stash 的代码
2. 🔲 适配新的 SelectionDetailsPanel API
3. 🔲 测试 All Of Memory 中 Managed Objects 的 Callstack 显示

## 📊 影响范围

### 修改文件
- `Unity.MemoryProfiler.UI/Controls/SelectionDetailsPanel.xaml`
- `Unity.MemoryProfiler.UI/Controls/SelectionDetailsPanel.xaml.cs`
- `Unity.MemoryProfiler.UI/Services/SelectedItemDetailsBuilder.cs`
- `Unity.MemoryProfiler.UI/Services/SelectionDetails/*.cs` (所有 Presenter)
- `Unity.MemoryProfiler.UI/Models/DetailsGroup.cs` (可能删除)

### 风险评估
- **高风险**：SelectionDetailsPanel 是核心 UI 组件，影响所有详情显示功能
- **缓解措施**：
  1. 充分测试所有功能场景
  2. 保持逻辑一致性
  3. 分阶段重构，每个阶段都可编译运行
  4. 使用 Git 分支进行开发

## 🎯 预期收益

1. **代码质量**：清晰的静态布局，易于理解和维护
2. **性能提升**：减少动态创建和销毁控件的开销
3. **可靠性**：避免 WPF Visual Tree 冲突
4. **扩展性**：添加新的 UI 区域只需在 XAML 中定义

## 📝 后续任务

1. 重构 SelectionDetailsPanel（本文档）
2. 实现 All Of Memory Managed Objects Callstack 功能
3. 考虑是否需要重构其他动态 UI 组件

