# 移除 SelectionDetailsPanelAdapter - 完成 MVVM 重构

## 📋 背景

### 当前状态
- ✅ ViewModel 层已创建（`SelectionDetailsViewModel` 及各个 `SectionViewModel`）
- ✅ XAML 已重构为静态布局 + 数据绑定
- ✅ `SelectionDetailsPanel` 已简化
- ⚠️ **但仍在使用 `SelectionDetailsPanelAdapter` 作为过渡层**

### 问题
`SelectionDetailsPanelAdapter` 是一个**临时适配层**，用于：
- 提供旧的 API（`AddDynamicElement`、`ClearGroup` 等）
- 将旧 API 调用转换为 ViewModel 操作
- 让 `SelectedItemDetailsBuilder` 无需立即重写（81 处调用）

**这违背了 MVVM 原则**：
- Presenter/Builder 应该直接操作 ViewModel
- 不应该有中间适配层
- Adapter 增加了复杂性和维护成本

## 🎯 目标

**完全移除 `SelectionDetailsPanelAdapter`**，实现真正的 MVVM 架构：

```
数据源 (CachedSnapshot)
    ↓
Presenter (ISelectionDetailsPresenter)
    ↓
直接操作 ViewModel
    ↓
WPF 数据绑定
    ↓
UI 自动更新
```

## 📊 影响范围分析

### 需要修改的文件

#### 1. SelectedItemDetailsBuilder.cs
- **81 处** `m_Adapter.` 调用需要改为直接操作 ViewModel
- 主要方法：
  - `SetSelection()` - 多个重载
  - `HandlePureCSharpObjectDetails()`
  - `HandleNativeObjectDetails()`
  - `HandleManagedObjectDetails()`
  - `AddManagedCallStacksInfoToUI()` (如果已实现)

#### 2. Presenters
- `SummarySelectionDetailsPresenter.cs` - 使用 `adapter`
- `ManagedObjectsSelectionDetailsPresenter.cs` - 使用 `adapter`
- `UnityObjectsSelectionDetailsPresenter.cs` - 使用 `builder`（已正确）
- `AllTrackedMemorySelectionDetailsPresenter.cs` - 使用 `builder`（已正确）

#### 3. 删除的文件
- `Unity.MemoryProfiler.UI/Controls/SelectionDetailsPanelAdapter.cs`

## 🔧 重构方案

### API 映射表

#### 旧 API (Adapter) → 新 API (ViewModel)

```csharp
// 清空所有
m_Adapter.ClearAllGroups()
→ viewModel.Clear()

// 设置标题
m_Adapter.SetItemName(title)
→ viewModel.Title = title; viewModel.ShowDetails()

// 设置描述
m_Adapter.SetDescription(desc)
→ viewModel.Description.Text = desc

// 添加基本信息
m_Adapter.AddDynamicElement(GroupNameBasic, label, value, tooltip)
→ viewModel.BasicInfo.AddProperty(label, value, tooltip)

// 添加内存信息
m_Adapter.AddDynamicElement(GroupNameMemory, label, value, tooltip)
→ viewModel.MemoryInfo.AddProperty(label, value, tooltip)

// 添加高级信息
m_Adapter.AddDynamicElement(GroupNameAdvanced, label, value, tooltip)
→ viewModel.AdvancedInfo.AddProperty(label, value, tooltip)

// 添加帮助信息
m_Adapter.AddDynamicElement(GroupNameHelp, label, value, tooltip)
→ viewModel.Help.AddProperty(label, value, tooltip)

// 添加元数据
m_Adapter.AddDynamicElement(GroupNameMetaData, label, value, tooltip)
→ viewModel.MetaData.AddProperty(label, value, tooltip)

// 清空指定分组
m_Adapter.ClearGroup(groupName)
→ viewModel.BasicInfo.Clear() // 根据 groupName 选择对应的 Section
```

### 重构步骤

#### Phase 1: 准备工作
1. ✅ 分析所有 Adapter 调用点
2. ✅ 创建 API 映射表
3. 🔲 确保所有 ViewModel 有必要的方法

#### Phase 2: 重构 SelectedItemDetailsBuilder
1. 🔲 修改构造函数：
   ```csharp
   // 旧
   public SelectedItemDetailsBuilder(CachedSnapshot snapshot, SelectionDetailsPanelAdapter adapter)
   
   // 新
   public SelectedItemDetailsBuilder(CachedSnapshot snapshot, SelectionDetailsViewModel viewModel)
   ```

2. 🔲 替换所有 `m_Adapter.` 调用为 ViewModel 操作

3. 🔲 示例重构：
   ```csharp
   // 旧代码
   m_Adapter.ClearAllGroups();
   m_Adapter.SetItemName(type);
   m_Adapter.SetDescription("The selected item is a Type.");
   m_Adapter.AddDynamicElement(GroupNameBasic, "Managed Type", type.ManagedTypeName);
   
   // 新代码
   m_ViewModel.Clear();
   m_ViewModel.Title = type.ManagedTypeName ?? type.NativeTypeName ?? "Type";
   m_ViewModel.Description.Text = "The selected item is a Type.";
   m_ViewModel.BasicInfo.AddProperty("Managed Type", type.ManagedTypeName);
   m_ViewModel.ShowDetails();
   ```

#### Phase 3: 重构 Presenters
1. 🔲 `SummarySelectionDetailsPresenter`
   ```csharp
   // 旧
   var adapter = panel.Adapter;
   adapter.ClearAllGroups();
   
   // 新
   var viewModel = panel.ViewModel;
   viewModel.Clear();
   ```

2. 🔲 `ManagedObjectsSelectionDetailsPresenter`
   - 同样改为直接操作 ViewModel

#### Phase 4: 更新 SelectionDetailsPanel
1. 🔲 移除 `Adapter` 属性
2. 🔲 更新 `DetailsBuilder` 的创建：
   ```csharp
   // 旧
   m_DetailsBuilder = new SelectedItemDetailsBuilder(m_Snapshot, Adapter);
   
   // 新
   m_DetailsBuilder = new SelectedItemDetailsBuilder(m_Snapshot, ViewModel);
   ```

#### Phase 5: 删除 Adapter
1. 🔲 删除 `SelectionDetailsPanelAdapter.cs`
2. 🔲 清理所有 `using` 引用

#### Phase 6: 测试验证
1. 🔲 Summary 功能
2. 🔲 Unity Objects 功能
3. 🔲 All Of Memory 功能
4. 🔲 Managed Objects 功能
5. 🔲 Diff 模式

## 📝 重构示例

### 示例 1: SetSelection(UnifiedType)

#### 旧代码（使用 Adapter）
```csharp
public void SetSelection(UnifiedType type)
{
    m_Adapter.ClearAllGroups();
    m_Adapter.SetItemName(type);
    m_Adapter.SetDescription("The selected item is a Type.");
    m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameBasic);
    
    if (type.HasManagedType)
    {
        m_Adapter.AddDynamicElement(GroupNameBasic, "Managed Type", type.ManagedTypeName);
    }
    
    if (type.HasNativeType)
    {
        m_Adapter.AddDynamicElement(GroupNameBasic, "Native Type", type.NativeTypeName);
    }
}
```

#### 新代码（直接操作 ViewModel）
```csharp
public void SetSelection(UnifiedType type)
{
    m_ViewModel.Clear();
    m_ViewModel.Title = type.ManagedTypeName ?? type.NativeTypeName ?? "Type";
    m_ViewModel.Description.Text = "The selected item is a Type.";
    
    if (type.HasManagedType)
    {
        m_ViewModel.BasicInfo.AddProperty("Managed Type", type.ManagedTypeName);
    }
    
    if (type.HasNativeType)
    {
        m_ViewModel.BasicInfo.AddProperty("Native Type", type.NativeTypeName);
    }
    
    m_ViewModel.ShowDetails();
}
```

### 示例 2: HandlePureCSharpObjectDetails

#### 旧代码
```csharp
private void HandlePureCSharpObjectDetails(ObjectData objectData, UnifiedType type)
{
    m_Adapter.SetItemName(objectData, type);
    m_Adapter.ClearGroup(GroupNameBasic);
    m_Adapter.AddDynamicElement(GroupNameBasic, "Type", type.ManagedTypeName);
    m_Adapter.AddDynamicElement(GroupNameMemory, "Size", 
        EditorUtility.FormatBytes(objectData.size));
}
```

#### 新代码
```csharp
private void HandlePureCSharpObjectDetails(ObjectData objectData, UnifiedType type)
{
    // 生成标题
    string title = objectData.isManaged && m_Snapshot != null
        ? $"Managed Object: 0x{objectData.hostManagedObjectPtr:X} ({objectData.GenerateTypeName(m_Snapshot, false)})"
        : objectData.GenerateTypeName(m_Snapshot, false) ?? "Unknown Object";
    
    m_ViewModel.Title = title;
    m_ViewModel.BasicInfo.Clear();
    m_ViewModel.BasicInfo.AddProperty("Type", type.ManagedTypeName);
    m_ViewModel.MemoryInfo.AddProperty("Size", EditorUtility.FormatBytes(objectData.size));
    m_ViewModel.ShowDetails();
}
```

## 🎯 预期收益

### 代码质量
- ✅ **真正的 MVVM**：Presenter → ViewModel → View
- ✅ **更清晰**：移除中间层，逻辑更直接
- ✅ **更易维护**：ViewModel API 更语义化
- ✅ **更易测试**：可以独立测试 ViewModel

### 性能
- ✅ **减少一层调用**：Adapter 转换开销消失
- ✅ **更高效**：直接操作 ViewModel 属性

### 可扩展性
- ✅ **添加新字段更容易**：直接在 ViewModel 中添加属性
- ✅ **UI 定制更灵活**：XAML 可以自由绑定任何 ViewModel 属性

## ⚠️ 风险评估

### 高风险
- `SelectedItemDetailsBuilder` 有 **81 处**调用需要修改
- 逻辑复杂，容易引入 bug

### 缓解措施
1. **分阶段重构**：一个方法一个方法地改
2. **充分测试**：每改一个方法都要测试
3. **保留 Git 历史**：每个阶段都提交
4. **对比测试**：与原有功能逐一对比

## 📅 时间估算

- Phase 1: 准备工作 - 0.5 小时（已完成）
- Phase 2: 重构 SelectedItemDetailsBuilder - **3-4 小时**（核心工作）
- Phase 3: 重构 Presenters - 1 小时
- Phase 4: 更新 SelectionDetailsPanel - 0.5 小时
- Phase 5: 删除 Adapter - 0.5 小时
- Phase 6: 测试验证 - 2 小时

**总计：7-8 小时**

## 📌 结论

**Adapter 必须被移除**，它只是一个过渡方案。完成这个重构后，我们将拥有：
- 真正的 MVVM 架构
- 清晰的代码结构
- 更好的可维护性
- 更好的性能

这是完成 SelectionDetailsPanel MVVM 重构的**最后一步**。

