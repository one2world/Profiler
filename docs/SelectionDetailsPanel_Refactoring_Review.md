# SelectionDetailsPanel 重构方案 - 深度 Review

## 📊 现状分析

### 当前架构问题

#### 1. 混合模式导致的冲突
```csharp
// 问题代码
private void InitializeGroups()
{
    // 只注册了 Basic 和 Advanced
    RegisterGroup(GroupNameBasic, BasicInfoExpander, BasicInfoContent);
    RegisterGroup(GroupNameAdvanced, AdvancedInfoExpander, AdvancedInfoContent);
}

public DetailsGroup GetOrCreateGroup(string groupName)
{
    if (_groups.TryGetValue(groupName, out var existingGroup))
        return existingGroup;
    
    // 动态创建新分组
    var expander = new Expander { ... };
    // ❌ 尝试将 expander 插入到 DetailsContent
    DetailsContent.Children.Insert(insertIndex, expander);
}
```

**问题**：
- `BasicInfoExpander` 和 `AdvancedInfoExpander` 已在 XAML 中定义并添加到 `DetailsContent`
- 当调用 `GetOrCreateGroup(GroupNameCallStacks)` 时，如果 stash 代码中添加了 `CallStacksExpander` 到 XAML，但没有在 `InitializeGroups` 中注册
- 导致 `GetOrCreateGroup` 尝试将已存在的 `CallStacksExpander` 再次插入到 `DetailsContent`
- 触发异常：**指定的 Visual 已经是另一个 Visual 的子级**

#### 2. 不完整的分组注册
当前只注册了 2 个分组，但 XAML 中有 6 个 Expander：
- ✅ `BasicInfoExpander` (已注册)
- ❌ `MemoryInfoExpander` (未注册)
- ❌ `DescriptionExpander` (未注册)
- ✅ `AdvancedInfoExpander` (已注册)
- ❌ `ManagedFieldsExpander` (未注册)
- ❌ `ReferencesExpander` (未注册)

#### 3. 动态元素类型复杂
`AddDynamicElement` 支持多种类型：
- 普通标签+值
- Button
- Toggle
- SubFoldout (可展开的子区域)
- RichText (富文本)
- SelectableLabel (可选择的文本)
- InfoBox (信息提示框)

### 使用场景分析

#### 场景 1: Summary
```csharp
panel.AddDynamicElement(GroupNameBasic, "Label", "Value", tooltip, 
    DynamicElementOptions.ShowTitle | DynamicElementOptions.SelectableLabel);
```

#### 场景 2: Native Allocations Callstack
```csharp
// Button
m_UI.AddDynamicElement(GroupNameCallStacks, "Copy Call Stack", "Copy", tooltip,
    DynamicElementOptions.Button, onClick);

// Toggle
m_UI.AddDynamicElement(GroupNameCallStacks, "Show Address", "Show Address", tooltip,
    DynamicElementOptions.Toggle | DynamicElementOptions.ToggleOn, onToggle);

// SubFoldout
m_UI.AddDynamicElement(GroupNameCallStacks, "CallStack #1", richText,
    DynamicElementOptions.SubFoldout | DynamicElementOptions.EnableRichText);
```

#### 场景 3: Managed Objects
```csharp
// 使用 SelectedItemDetailsBuilder
builder.SetSelection(source, name, description);
// 内部会调用多次 AddDynamicElement
```

## 🔍 重构方案 Review

### ✅ 优点

1. **清晰的静态布局**：所有 UI 区域在 XAML 中预定义，一目了然
2. **避免 Visual Tree 冲突**：不再动态创建和插入控件
3. **性能提升**：减少控件创建/销毁开销
4. **易于维护**：UI 结构集中在 XAML，逻辑集中在代码

### ⚠️ 潜在问题

#### 问题 1: 动态分组名称
**现状**：Unity 官方支持动态创建任意名称的分组
```csharp
m_UI.AddDynamicElement("Custom Group Name", "Label", "Value");
```

**重构方案**：只支持预定义的分组名称
```csharp
_sections = new Dictionary<string, (Expander, Panel)>
{
    { GroupNameBasic, (BasicInfoExpander, BasicInfoContent) },
    { GroupNameCallStacks, (CallStacksExpander, CallStacksContent) },
    // ... 固定的分组
};
```

**影响评估**：
- 需要检查所有 `AddDynamicElement` 调用，确认是否有使用自定义分组名称
- 如果有，需要将其映射到预定义分组，或添加到 XAML

**缓解措施**：
```csharp
public void AddToSection(string sectionName, UIElement element)
{
    if (_sections.TryGetValue(sectionName, out var section))
    {
        section.content.Children.Add(element);
        ShowSection(sectionName);
    }
    else
    {
        // 回退：添加到 Basic 分组并记录警告
        System.Diagnostics.Debug.WriteLine($"[Warning] Unknown section: {sectionName}, fallback to Basic");
        AddToSection(GroupNameBasic, element);
    }
}
```

#### 问题 2: SubFoldout 的实现
**现状**：SubFoldout 是动态创建的 Expander
```csharp
private UIElement CreateSubFoldoutElement(string title, string? tooltip)
{
    var expander = new Expander
    {
        Header = title,
        IsExpanded = false,
        Content = new StackPanel()
    };
    return expander;
}
```

**重构方案**：仍需动态创建 SubFoldout（它是内容的一部分，不是顶级分组）
```csharp
// 这是合理的，SubFoldout 是添加到预定义分组的内容
m_UI.AddToSection(GroupNameCallStacks, CreateSubFoldout("CallStack #1", content));
```

**结论**：SubFoldout 不受影响，可以继续动态创建

#### 问题 3: 复杂元素的创建
**现状**：`AddDynamicElement` 一个方法处理所有类型
```csharp
public UIElement AddDynamicElement(string groupName, string elementName, string value,
    string? tooltip, DynamicElementOptions options, Action? onInteraction)
{
    if (options.HasFlag(DynamicElementOptions.Button))
        element = CreateButtonElement(...);
    else if (options.HasFlag(DynamicElementOptions.Toggle))
        element = CreateToggleElement(...);
    else if (options.HasFlag(DynamicElementOptions.SubFoldout))
        element = CreateSubFoldoutElement(...);
    // ...
}
```

**重构方案**：需要手动创建元素
```csharp
// 旧方式（一行）
m_UI.AddDynamicElement(GroupNameBasic, "Size", "1024 B", tooltip);

// 新方式（多行）
var grid = CreateLabelValueGrid("Size", "1024 B", tooltip);
m_UI.AddToSection(GroupNameBasic, grid);
```

**影响评估**：
- 代码量增加
- 但更清晰、更灵活

**缓解措施**：提供辅助方法
```csharp
// 简化的辅助方法
public void AddLabelValue(string sectionName, string label, string value, string? tooltip = null)
{
    var grid = CreateLabelValueGrid(label, value, tooltip);
    AddToSection(sectionName, grid);
}

public void AddButton(string sectionName, string label, string text, Action onClick)
{
    var button = CreateButton(label, text, onClick);
    AddToSection(sectionName, button);
}

public void AddToggle(string sectionName, string label, bool isChecked, Action<bool> onToggle)
{
    var toggle = CreateToggle(label, isChecked, onToggle);
    AddToSection(sectionName, toggle);
}
```

#### 问题 4: InfoBox 的处理
**现状**：InfoBox 是一个自定义控件，通过 `AddInfoBox` 方法添加
```csharp
m_UI.AddInfoBox(SelectionDetailsPanel.GroupNameBasic, new InfoBox
{
    Level = InfoBox.IssueLevel.Error,
    Message = "Error message"
});
```

**重构方案**：保持不变，InfoBox 可以直接添加到分组
```csharp
var infoBox = new InfoBox { ... };
m_UI.AddToSection(GroupNameBasic, infoBox);
```

**结论**：不受影响

### 📋 完整的分组列表

根据代码分析，需要预定义以下分组：

| 分组名称 | 常量 | XAML Expander | 当前状态 | 用途 |
|---------|------|--------------|---------|------|
| Basic Information | `GroupNameBasic` | `BasicInfoExpander` | ✅ 已有 | 基本信息 |
| Memory Information | `GroupNameMemory` | `MemoryInfoExpander` | ✅ 已有 | 内存信息 |
| Description | - | `DescriptionExpander` | ✅ 已有 | 描述文本 |
| Advanced | `GroupNameAdvanced` | `AdvancedInfoExpander` | ✅ 已有 | 高级信息 |
| Call Stack Info | `GroupNameCallStacks` | ❌ 需添加 | 新增 | 调用栈 |
| Managed Fields | `GroupNameManagedFields` | `ManagedFieldsExpander` | ✅ 已有 | Managed 字段 |
| References | - | `ReferencesExpander` | ✅ 已有 | 引用关系 |
| MetaData | `GroupNameMetaData` | ❌ 需添加 | 动态 | 元数据 |
| Help | `GroupNameHelp` | ❌ 需添加 | 动态 | 帮助信息 |
| Debug | `GroupNameDebug` | ❌ 需添加 | 动态 | 调试信息 |

**注意**：`GroupNameMemory` 常量缺失，需要添加

### 🔧 改进的重构方案

#### 1. 完整的 XAML 结构
```xml
<ScrollViewer>
    <StackPanel x:Name="DetailsContent" Visibility="Collapsed">
        <!-- 标题 -->
        <TextBlock x:Name="TitleTextBlock" Style="{StaticResource TitleStyle}"/>
        
        <!-- Basic Information -->
        <Expander x:Name="BasicInfoExpander" Header="Basic Information" 
                  IsExpanded="True" Visibility="Collapsed">
            <StackPanel x:Name="BasicInfoContent"/>
        </Expander>
        
        <!-- Memory Information -->
        <Expander x:Name="MemoryInfoExpander" Header="Memory Information" 
                  IsExpanded="True" Visibility="Collapsed">
            <StackPanel x:Name="MemoryInfoContent"/>
        </Expander>
        
        <!-- Description -->
        <Expander x:Name="DescriptionExpander" Header="Description" 
                  IsExpanded="False" Visibility="Collapsed">
            <TextBlock x:Name="DescriptionText" TextWrapping="Wrap"/>
        </Expander>
        
        <!-- Advanced -->
        <Expander x:Name="AdvancedInfoExpander" Header="Advanced" 
                  IsExpanded="False" Visibility="Collapsed">
            <StackPanel x:Name="AdvancedInfoContent"/>
        </Expander>
        
        <!-- Call Stack Info (新增) -->
        <Expander x:Name="CallStacksExpander" Header="Call Stack Info" 
                  IsExpanded="True" Visibility="Collapsed">
            <StackPanel x:Name="CallStacksContent"/>
        </Expander>
        
        <!-- MetaData (新增) -->
        <Expander x:Name="MetaDataExpander" Header="MetaData" 
                  IsExpanded="True" Visibility="Collapsed">
            <StackPanel x:Name="MetaDataContent"/>
        </Expander>
        
        <!-- Help (新增) -->
        <Expander x:Name="HelpExpander" Header="Help" 
                  IsExpanded="False" Visibility="Collapsed">
            <StackPanel x:Name="HelpContent"/>
        </Expander>
        
        <!-- Debug (新增) -->
        <Expander x:Name="DebugExpander" Header="Debug" 
                  IsExpanded="False" Visibility="Collapsed">
            <StackPanel x:Name="DebugContent"/>
        </Expander>
        
        <!-- Managed Fields -->
        <Expander x:Name="ManagedFieldsExpander" Header="Managed Fields" 
                  IsExpanded="True" Visibility="Collapsed">
            <local:ManagedObjectInspector x:Name="ManagedObjectInspectorControl"/>
        </Expander>
        
        <!-- References -->
        <Expander x:Name="ReferencesExpander" Header="References" 
                  IsExpanded="True" Visibility="Collapsed">
            <local:PathsToRootView x:Name="PathsToRootViewControl"/>
        </Expander>
        
        <!-- No Selection Message -->
        <TextBlock x:Name="NoSelectionMessage" Text="No Selection" 
                   Visibility="Visible" Style="{StaticResource HintStyle}"/>
    </StackPanel>
</ScrollViewer>
```

#### 2. 改进的代码结构
```csharp
public partial class SelectionDetailsPanel : UserControl
{
    // 分组名称常量
    public const string GroupNameBasic = "Basic";
    public const string GroupNameMemory = "Memory"; // 新增
    public const string GroupNameDescription = "Description"; // 新增
    public const string GroupNameMetaData = "MetaData";
    public const string GroupNameHelp = "Help";
    public const string GroupNameAdvanced = "Advanced";
    public const string GroupNameCallStacks = "Call Stack Info";
    public const string GroupNameDebug = "Debug";
    public const string GroupNameManagedFields = "Managed Fields";
    public const string GroupNameReferences = "References"; // 新增
    
    // 预定义的所有分组
    private readonly Dictionary<string, (Expander expander, Panel content)> _sections;
    
    private void InitializeSections()
    {
        _sections = new Dictionary<string, (Expander, Panel)>
        {
            { GroupNameBasic, (BasicInfoExpander, BasicInfoContent) },
            { GroupNameMemory, (MemoryInfoExpander, MemoryInfoContent) },
            { GroupNameAdvanced, (AdvancedInfoExpander, AdvancedInfoContent) },
            { GroupNameCallStacks, (CallStacksExpander, CallStacksContent) },
            { GroupNameMetaData, (MetaDataExpander, MetaDataContent) },
            { GroupNameHelp, (HelpExpander, HelpContent) },
            { GroupNameDebug, (DebugExpander, DebugContent) },
        };
        
        // 特殊处理：DescriptionExpander 的 Content 是 TextBlock 而不是 Panel
        // 特殊处理：ManagedFieldsExpander 的 Content 是 ManagedObjectInspector
        // 特殊处理：ReferencesExpander 的 Content 是 PathsToRootView
        
        HideAllSections();
    }
    
    // 辅助方法：简化常见操作
    public void AddLabelValue(string sectionName, string label, string value, string? tooltip = null)
    {
        var grid = CreateLabelValueGrid(label, value, tooltip);
        AddToSection(sectionName, grid);
    }
    
    public void AddButton(string sectionName, string label, string text, string? tooltip, Action onClick)
    {
        var button = CreateButton(label, text, tooltip, onClick);
        AddToSection(sectionName, button);
    }
    
    public void AddToggle(string sectionName, string label, string text, bool isChecked, string? tooltip, Action<bool> onToggle)
    {
        var toggle = CreateToggle(label, text, isChecked, tooltip, onToggle);
        AddToSection(sectionName, toggle);
    }
    
    public void AddSubFoldout(string sectionName, string title, UIElement content)
    {
        var expander = CreateSubFoldout(title, content);
        AddToSection(sectionName, expander);
    }
    
    // 特殊处理方法
    public void SetDescription(string text)
    {
        DescriptionText.Text = text;
        DescriptionExpander.Visibility = string.IsNullOrEmpty(text) 
            ? Visibility.Collapsed 
            : Visibility.Visible;
    }
}
```

#### 3. SelectedItemDetailsBuilder 适配示例
```csharp
// 旧方式
m_UI.AddDynamicElement(GroupNameBasic, "Size", EditorUtility.FormatBytes(size), tooltip);
m_UI.AddDynamicElement(GroupNameCallStacks, "Copy Call Stack", "Copy", tooltip, 
    DynamicElementOptions.Button, onClick);

// 新方式
m_UI.AddLabelValue(GroupNameBasic, "Size", EditorUtility.FormatBytes(size), tooltip);
m_UI.AddButton(GroupNameCallStacks, "Copy Call Stack", "Copy", tooltip, onClick);
```

### 🎯 最终建议

#### 分阶段实施

**Phase 1: 准备和验证**
1. ✅ 扫描所有 `AddDynamicElement` 调用，统计分组名称
2. ✅ 确认所有分组都在 XAML 中预定义
3. ✅ 设计辅助方法 API

**Phase 2: 重构核心**
1. 更新 XAML，添加缺失的 Expander
2. 重构 `SelectionDetailsPanel.xaml.cs`
   - 实现 `InitializeSections`
   - 实现辅助方法
   - 保留 `AddDynamicElement` 作为过渡（标记为 Obsolete）
3. 编译测试

**Phase 3: 逐步迁移**
1. 迁移 `SelectedItemDetailsBuilder`
2. 迁移所有 Presenter
3. 每迁移一个模块，测试一次
4. 确保功能一致性

**Phase 4: 清理和优化**
1. 移除 `AddDynamicElement`
2. 移除 `DetailsGroup` 类
3. 移除 `GetOrCreateGroup`
4. 代码审查和文档更新

**Phase 5: 实现 Callstack 功能**
1. 恢复 stash
2. 适配新 API
3. 测试验证

### ⚠️ 风险和注意事项

1. **高风险模块**：SelectionDetailsPanel 影响所有详情显示
2. **测试覆盖**：必须测试所有功能（Summary、Unity Objects、All Of Memory、Managed Objects、Diff）
3. **UI 一致性**：确保重构后 UI 外观和行为完全一致
4. **性能验证**：虽然理论上性能更好，但需实际验证
5. **Git 管理**：使用独立分支，每个 Phase 一个 commit

### ✅ 结论

重构方案**总体可行**，但需要：
1. **补充缺失的分组**（MetaData、Help、Debug、Memory 常量）
2. **提供辅助方法**以简化迁移工作
3. **分阶段实施**，每个阶段都可编译运行
4. **充分测试**，确保功能一致性

建议**先实施 Phase 1 和 Phase 2**，验证核心架构可行后再继续。

