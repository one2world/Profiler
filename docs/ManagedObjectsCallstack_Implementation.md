# Managed Objects Callstack 实现文档

## 📋 功能概述

为 **All Of Memory** 标签页中的 **Managed Objects** 添加分配堆栈（Callstack）显示功能，使用 DevExpress TreeListView 实现，与 Native Objects 的功能保持一致。

## ✅ 已完成功能

### 1. CallStackTreeView 控件

**文件**: `Unity.MemoryProfiler.UI/Controls/CallStackTreeView.xaml` 和 `.xaml.cs`

#### 功能特性:
- 使用 DevExpress TreeListControl 显示调用栈
- 两列显示: `Call Stack` (函数名) 和 `File:Line` (文件路径:行号)
- 支持双击行跳转到 VS Code 源码位置
- 自动从 `appsettings.json` 读取源码目录配置

#### 核心方法:
```csharp
// 设置数据源
public void SetData(List<CallStackNode> nodes, List<string>? sourceDirectories = null)

// 清空数据
public void ClearData()

// 双击跳转到源码
private void OnRowDoubleClick(object sender, RowDoubleClickEventArgs e)
```

#### CallStackNode 数据模型:
```csharp
public class CallStackNode
{
    public string Description { get; set; }      // 函数名 (Module!Function)
    public string FileLine { get; set; }         // 文件路径:行号
    public string FilePath { get; set; }         // 文件路径 (用于跳转)
    public int LineNumber { get; set; }          // 行号 (用于跳转)
    public List<CallStackNode>? Children { get; set; }  // 子节点
}
```

### 2. SelectionDetailsPanel 集成

**文件**: `Unity.MemoryProfiler.UI/Controls/SelectionDetailsPanel.xaml` 和 `.xaml.cs`

#### XAML 更新:
```xml
<!-- 堆栈内容 - TreeListView -->
<Expander Header="Allocation Call Stack" IsExpanded="True" Margin="0,5">
    <local:CallStackTreeView x:Name="CallStackTreeViewControl"
                             Height="300"
                             Margin="10,5"/>
</Expander>
```

#### 新增方法:
```csharp
// 设置 CallStack TreeView 的数据
public void SetupCallStackTreeView(List<CallStackNode> nodes, List<string>? sourceDirectories = null)

// 隐藏 CallStack TreeView
public void HideCallStackTreeView()
```

### 3. SelectedItemDetailsBuilder 数据加载

**文件**: `Unity.MemoryProfiler.UI/Services/SelectedItemDetailsBuilder.cs`

#### 新增方法:

##### AddManagedCallStacksInfoToUI
```csharp
private void AddManagedCallStacksInfoToUI(ulong objectAddress)
{
    // 1. 检查数据可用性
    if (m_CachedSnapshot.ManagedAllocations == null)
        return;

    // 2. 获取 CallStack
    var callStack = m_CachedSnapshot.ManagedAllocations.GetCallStackForAddress(objectAddress);
    if (callStack == null || callStack.Frames.Count == 0)
        return;

    // 3. 构建 TreeListView 数据
    var nodes = BuildCallStackTreeNodes(callStack);
    if (nodes.Count == 0)
        return;

    // 4. 获取源码目录配置
    var sourceDirectories = ManagedObjectsConfigService.GetSourceDirectories();

    // 5. 设置到 UI
    m_UI.SetupCallStackTreeView(nodes, sourceDirectories);
}
```

##### BuildCallStackTreeNodes
```csharp
private List<CallStackNode> BuildCallStackTreeNodes(CallStack callStack)
{
    var nodes = new List<CallStackNode>();

    // 从调用栈底部（最外层）到顶部（分配点）遍历
    for (int i = callStack.Frames.Count - 1; i >= 0; i--)
    {
        var frame = callStack.Frames[i];
        
        var node = new CallStackNode
        {
            Description = $"{frame.Module}!{frame.Function}",
            FileLine = string.IsNullOrEmpty(frame.FilePath) ? "" : $"{frame.FilePath}:{frame.LineNumber}",
            FilePath = frame.FilePath,
            LineNumber = frame.LineNumber
        };

        nodes.Add(node);
    }

    return nodes;
}
```

#### 调用位置:
在 `HandlePureCSharpObjectDetails` 方法中，获取 Managed Address 后调用:
```csharp
// Line 224-228: 显示Managed Address
var objectAddress = m_CurrentSelectionObjectData.GetObjectPointer(m_CachedSnapshot, false);
m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Managed Address", 
    Unity.MemoryProfiler.Editor.DetailFormatter.FormatPointer(objectAddress));

// 添加 Managed Callstack (如果有数据)
AddManagedCallStacksInfoToUI(objectAddress);
```

## 🎯 数据流程

```
用户在 All Of Memory 选择 Managed Object
    ↓
SelectedItemDetailsBuilder.HandlePureCSharpObjectDetails()
    ↓
获取对象地址 (objectAddress)
    ↓
AddManagedCallStacksInfoToUI(objectAddress)
    ↓
ManagedAllocations.GetCallStackForAddress(objectAddress)
    ↓
BuildCallStackTreeNodes(callStack)
    ↓
SelectionDetailsPanel.SetupCallStackTreeView(nodes, sourceDirectories)
    ↓
CallStackTreeView.SetData(nodes, sourceDirectories)
    ↓
显示在 "Call Stack Info" 分组的 TreeListView 中
```

## 📝 使用说明

### 1. 加载快照
- 打开应用，加载包含 Managed Objects 的快照文件 (`.snap`)
- 确保同目录下有对应的 `.allocHash.txt` 和 `.stacktrace.txt` 文件

### 2. 查看 Callstack
1. 切换到 **All Of Memory** 标签页
2. 展开 **Managed Objects** 节点
3. 选择任意 Managed Object (如 `System.String`)
4. 在右侧 **SelectionDetails** 面板中查看 **Call Stack Info** 分组
5. 展开 **Allocation Call Stack** 查看 TreeListView

### 3. 跳转到源码
1. 在 `appsettings.json` 中配置源码目录:
```json
{
  "ManagedObjects": {
    "SourceDirectories": [
      "E:\\GameProject\\Assets\\Scripts",
      "E:\\GameProject\\Library\\PackageCache"
    ]
  }
}
```
2. 双击 TreeListView 中的任意行
3. 如果文件存在，将自动在 VS Code 中打开并跳转到对应行

## 🔍 技术细节

### 数据来源
- **对象地址**: 从 `CachedSnapshot.ManagedData` 获取
- **堆栈Hash**: 从 `ManagedAllocations.AddressToStackHash` 字典获取
- **完整堆栈**: 从 `ManagedAllocations.StackHashToCallStack` 字典获取

### CallStack 结构
```csharp
public class CallStack
{
    public uint Hash { get; set; }
    public List<StackFrame> Frames { get; set; }
}

public class StackFrame
{
    public string Module { get; set; }      // 模块名 (如 GameAssembly.dll)
    public string Function { get; set; }    // 函数名
    public string FilePath { get; set; }    // 文件路径
    public int LineNumber { get; set; }     // 行号
    public ulong Address { get; set; }      // 地址
}
```

### 堆栈顺序
- 原始数据: Frames[0] 是分配点 (顶部), Frames[n-1] 是最外层调用
- 显示顺序: 从最外层到分配点 (从底部到顶部)
- 实现: `for (int i = callStack.Frames.Count - 1; i >= 0; i--)`

### 优雅降级
如果以下任一条件不满足，不显示 Call Stack Info:
1. `ManagedAllocations` 为 null
2. 对象地址没有对应的堆栈Hash
3. 堆栈Hash没有对应的 CallStack
4. CallStack 的 Frames 为空

## 🆚 与 Native Callstack 的区别

| 特性 | Native Callstack | Managed Callstack |
|------|------------------|-------------------|
| 显示方式 | TextBlock (富文本) | TreeListView |
| 数据来源 | NativeCallstackSymbols | ManagedAllocations |
| 可点击跳转 | 不支持 (WPF限制) | 支持 (双击行) |
| 地址显示 | 可选 (Toggle) | 不显示 |
| 复制功能 | 支持 | 不支持 (可扩展) |

## 📊 测试场景

### 测试数据
- 快照: `MemoryCaptures/DemoGame.snap`
- 堆栈Hash: `MemoryCaptures/DemoGame.snap.allocHash.txt`
- 堆栈详情: `MemoryCaptures/DemoGame.snap.stacktrace.txt`

### 测试步骤
1. ✅ 加载快照，确认数据加载成功
2. ✅ 在 All Of Memory 中选择 Managed Object
3. ✅ 验证 Call Stack Info 分组显示
4. ✅ 验证 TreeListView 显示完整堆栈
5. ✅ 验证 File:Line 列显示正确
6. ✅ 配置源码目录后双击行跳转到 VS Code
7. ✅ 选择没有堆栈的对象，验证优雅降级

## 🚀 未来优化

### 可选功能
1. **复制功能**: 添加右键菜单或按钮复制整个堆栈
2. **地址显示**: 添加 Toggle 控制是否显示地址列
3. **堆栈过滤**: 支持按模块或函数名过滤
4. **层级显示**: 支持折叠/展开子调用 (如果需要)
5. **性能优化**: 缓存 TreeListView 节点，避免重复构建

### 代码改进
1. 将 `CallStackNode` 移到独立的 Models 文件
2. 提取 `TryNavigateToSourceCode` 为共享服务
3. 支持更多代码编辑器 (Visual Studio, Rider 等)
4. 添加单元测试

## 📌 注意事项

1. **依赖自定义文件**: 此功能依赖 `.allocHash.txt` 和 `.stacktrace.txt`，不是 Unity 官方功能
2. **源码目录配置**: 需要在 `appsettings.json` 中正确配置源码目录才能跳转
3. **VS Code 依赖**: 代码跳转需要安装 VS Code 并配置到 PATH
4. **性能考虑**: 大型堆栈 (>100 帧) 可能影响 TreeListView 性能

## 🔗 相关文档
- [Managed Objects 功能设计](./ManagedObjects_Design.md)
- [SelectionDetailsPanel MVVM 重构](./SelectionDetailsPanel_MVVM_Refactoring.md)
- [Managed Objects Callstack 功能需求](./ManagedObjectsCallstack_Feature.md)

