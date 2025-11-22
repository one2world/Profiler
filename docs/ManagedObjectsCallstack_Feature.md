# All Of Memory - Managed Objects Callstack 功能需求

## 📋 功能概述

为 **All Of Memory** 标签页中的 **Managed Objects** 添加分配堆栈（Callstack）显示功能，使其与 Native Objects 的功能保持一致。

## 🎯 需求背景

### 当前状态
- ✅ **Managed Objects 标签页**：已有完整的堆栈主导视图（左侧调用栈树，右侧对象列表）
- ✅ **All Of Memory - Native Objects**：支持在 SelectionDetails 面板中显示 Callstack
- ❌ **All Of Memory - Managed Objects**：Unity 官方没有 Callstack 功能

### 数据来源
我们通过以下自定义文件获取 Managed Objects 的分配堆栈：
- `{snapshot}.allocHash.txt`：对象地址 → 堆栈Hash 映射
- `{snapshot}.stacktrace.txt`：堆栈Hash → 完整调用栈

### 用户价值
1. 在 All Of Memory 视图中快速查看单个 Managed Object 的分配堆栈
2. 无需切换到 Managed Objects 标签页
3. 与 Native Objects 的体验保持一致

## 🔧 技术方案

### 数据流程
```
用户在 All Of Memory 选择 Managed Object
    ↓
SelectedItemDetailsBuilder.HandlePureCSharpObjectDetails()
    ↓
获取对象地址 (objectAddress)
    ↓
查询 ManagedAllocations.GetStackHashForAddress(objectAddress)
    ↓
获取 CallStack = ManagedAllocations.StackHashToCallStack[stackHash]
    ↓
格式化堆栈 (FormatManagedCallStack)
    ↓
显示在 SelectionDetails 的 "Call Stack Info" 分组
```

### 核心实现

#### 1. AddManagedCallStacksInfoToUI 方法
```csharp
private void AddManagedCallStacksInfoToUI(ulong objectAddress)
{
    // 1. 检查数据可用性
    if (m_CachedSnapshot.ManagedAllocations == null) return;
    
    // 2. 获取堆栈Hash
    var stackHash = m_CachedSnapshot.ManagedAllocations.GetStackHashForAddress(objectAddress);
    if (!stackHash.HasValue || stackHash.Value == 0) return;
    
    // 3. 获取完整CallStack
    if (!m_CachedSnapshot.ManagedAllocations.StackHashToCallStack.TryGetValue(
        stackHash.Value, out var callStack)) return;
    if (callStack == null || callStack.Frames.Count == 0) return;
    
    // 4. 格式化堆栈
    var (plainText, richText) = FormatManagedCallStack(callStack, m_AddressInCallStacks);
    
    // 5. 添加到 UI
    // - Copy 按钮
    // - Call Stacks 计数
    // - Clickable Call Stacks Toggle
    // - Show Address Toggle
    // - SubFoldout 显示堆栈详情
}
```

#### 2. FormatManagedCallStack 方法
```csharp
private (string plainText, string richText) FormatManagedCallStack(
    CallStack callStack, bool includeAddress)
{
    // 从调用栈底部（最外层）到顶部（分配点）遍历
    for (int i = callStack.Frames.Count - 1; i >= 0; i--)
    {
        var frame = callStack.Frames[i];
        
        // 格式化：[地址] at Module!Function in FilePath:line LineNumber
        // 纯文本：用于复制
        // 富文本：用于显示（带颜色）
    }
}
```

#### 3. FormatStackFrame 方法
```csharp
private string FormatStackFrame(StackFrame frame, bool includeAddress, bool useRichText)
{
    // 地址（可选）：0x{Address:X16}
    // 前缀：at
    // 模块名：Module!
    // 函数名：Function
    // 文件位置：in FilePath:line LineNumber
    
    // 富文本着色：
    // - 地址：灰色 #808080
    // - 模块名：蓝色 #569CD6
    // - 函数名：加粗
    // - 文件路径：灰色 #808080
}
```

### UI 显示效果

```
📦 Call Stack Info
   - [Copy Call Stack] 按钮
   - Call Stacks: 1
   - [Clickable Call Stacks] Toggle (默认关闭)
   - [Show Address in Call Stacks] Toggle (默认开启)
   
   ▼ Allocation Call Stack
     0x00007FF8A1234567 at GameLogic!GameManager.InitializePlayer in GameManager.cs:line 123
     0x00007FF8A1234890 at GameLogic!GameManager.Start in GameManager.cs:line 45
     0x00007FF8A1235ABC at UnityEngine.CoreModule!UnityEngine.MonoBehaviour.StartCoroutine
     ...
```

## 📊 实施状态

### 已完成
- ✅ `AddManagedCallStacksInfoToUI` 方法实现
- ✅ `FormatManagedCallStack` 堆栈格式化逻辑
- ✅ `FormatStackFrame` 单帧格式化逻辑
- ✅ 在 `HandlePureCSharpObjectDetails` 中集成调用
- ✅ 数据获取和格式化功能验证（调试日志显示正常）
- ✅ 添加 `CallStacksExpander` 到 XAML

### 当前问题
- ❌ **WPF Visual Tree 冲突**：动态 UI 创建方式导致异常
  ```
  System.ArgumentException: 指定的 Visual 已经是另一个 Visual 的子级
  ```
- ❌ **代码组织混乱**：SelectionDetailsPanel 的动态 UI 逻辑难以维护

### 阻塞原因
需要先完成 **SelectionDetailsPanel 重构**（见 `SelectionDetailsPanel_Refactoring.md`）

## 🔄 实施计划

### Phase 1: SelectionDetailsPanel 重构（前置条件）
1. 重构 XAML 为静态布局
2. 移除动态创建逻辑
3. 使用 Visibility 控制显示/隐藏
4. 确保所有现有功能正常工作

### Phase 2: 恢复并适配 Callstack 功能
1. 从 Git Stash 恢复代码
   ```bash
   git stash list
   git stash apply stash@{0}
   ```

2. 适配新的 SelectionDetailsPanel API
   ```csharp
   // 旧方式
   m_UI.AddDynamicElement(GroupNameCallStacks, "Copy Call Stack", ...);
   
   // 新方式
   var button = CreateCopyButton(callStackText);
   m_UI.AddToSection(GroupNameCallStacks, button);
   ```

3. 测试验证
   - 加载 `DemoGame.snap` + `.allocHash.txt` + `.stacktrace.txt`
   - 在 All Of Memory 中选择 Managed Object
   - 验证 Call Stack Info 分组正确显示
   - 验证复制功能
   - 验证 Toggle 功能

### Phase 3: 完善和优化
1. 添加错误处理
2. 优化性能（缓存格式化结果）
3. 添加单元测试
4. 更新用户文档

## 📝 测试用例

### 测试数据
- 快照文件：`MemoryCaptures/DemoGame.snap`
- 堆栈Hash文件：`MemoryCaptures/DemoGame.snap.allocHash.txt`
- 堆栈详情文件：`MemoryCaptures/DemoGame.snap.stacktrace.txt`

### 测试场景

#### 1. 正常显示
- **步骤**：
  1. 加载快照
  2. 进入 All Of Memory 标签页
  3. 展开 Managed Objects
  4. 选择任意 Managed Object（如 `System.String`）
- **预期**：
  - SelectionDetails 面板显示 "Call Stack Info" 分组
  - 显示完整的调用栈
  - 包含文件路径和行号

#### 2. 复制功能
- **步骤**：点击 "Copy Call Stack" 按钮
- **预期**：调用栈纯文本被复制到剪贴板

#### 3. Toggle 功能
- **步骤**：切换 "Show Address in Call Stacks"
- **预期**：堆栈显示中的地址信息显示/隐藏

#### 4. 无堆栈数据
- **步骤**：选择没有堆栈信息的对象
- **预期**：不显示 "Call Stack Info" 分组（优雅降级）

#### 5. 没有堆栈文件
- **步骤**：加载没有 `.allocHash.txt` 的快照
- **预期**：不显示 "Call Stack Info" 分组（优雅降级）

## 🔗 相关文档
- [SelectionDetailsPanel 重构方案](./SelectionDetailsPanel_Refactoring.md)
- [Managed Objects 功能设计](./ManagedObjects_Design.md)（如果有）

## 📌 注意事项
1. 此功能依赖自定义的堆栈文件，不是 Unity 官方功能
2. 需要确保 `.allocHash.txt` 和 `.stacktrace.txt` 文件与 `.snap` 文件在同一目录
3. 堆栈格式化需要考虑性能（31 帧堆栈格式化为 5760 字符的富文本）
4. 富文本显示和可选文本显示是互斥的（WPF 限制）

