using System;
using System.Collections.Generic;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.UI.Controls;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.UIContent;
using UnityEditor;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// Selection Details构建器 - 根据不同的数据类型分发到对应的处理方法
    /// 等价于Unity的 SelectedItemDetailsForTypesAndObjects
    /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsForTypesAndObjects (com.unity.memoryprofiler@1.1.6)
    /// </summary>
    internal class SelectedItemDetailsBuilder
    {
        // 当前选择的对象数据
        private long m_CurrentSelectionIdx;
        private ObjectData m_CurrentSelectionObjectData;
        
        // 核心依赖
        private CachedSnapshot m_CachedSnapshot;
        private SelectionDetailsPanel m_UI;
        private SelectionDetailsPanelAdapter m_Adapter;
        private ManagedFieldsBuilder m_FieldsBuilder;

        // 常量 (参考Unity)
        private const string k_StatusLabelText = "Status";
        private const string k_HintLabelText = "Hint";
        private const string k_TriggerAssetGCHint = "triggering 'Resources.UnloadUnusedAssets()', explicitly or e.g. via a non-additive Scene unload.";

        // CallStacks设置 (参考MemoryProfilerSettings)
        private bool m_ClickableCallStacks = false;  // WPF中不可点击文件，默认false
        private bool m_AddressInCallStacks = true;   // 默认显示地址

        /// <summary>
        /// 构造函数
        /// </summary>
        public SelectedItemDetailsBuilder(CachedSnapshot snapshot, SelectionDetailsPanel detailsUI)
        {
            m_CachedSnapshot = snapshot;
            m_UI = detailsUI;
            m_Adapter = detailsUI.Adapter;
            m_FieldsBuilder = new ManagedFieldsBuilder(snapshot);
        }

        /// <summary>
        /// 设置选择项 - 根据SourceIndex类型分发到对应的Handler
        /// 参考: Unity.MemoryProfiler.Editor.UI.SelectedItemDetailsForTypesAndObjects.SetSelection (Line 29-58)
        /// 注意: Unity在调用SetSelection前会先调用Clear() - 参考 ObjectDetailsViewController.cs Line 309
        /// </summary>
        public void SetSelection(SourceIndex source, string fallbackName = null, string fallbackDescription = null, long childCount = -1)
        {
            // 清空所有分组内容，避免重复添加元素
            m_Adapter.ClearAllGroups();

            m_CurrentSelectionObjectData = ObjectData.FromSourceLink(m_CachedSnapshot, source);
            var type = new UnifiedType(m_CachedSnapshot, m_CurrentSelectionObjectData);
            
            switch (source.Id)
            {
                case SourceIndex.SourceId.NativeObject:
                case SourceIndex.SourceId.ManagedObject:
                    HandleObjectDetails(type);
                    break;
                    
                case SourceIndex.SourceId.GfxResource:
                    if (m_CurrentSelectionObjectData.IsValid)
                        HandleObjectDetails(type);
                    else
                        HandleGfxResourceDetails(source, fallbackName, fallbackDescription);
                    break;
                    
                case SourceIndex.SourceId.NativeType:
                case SourceIndex.SourceId.ManagedType:
                    HandleTypeDetails(type);
                    break;
                    
                case SourceIndex.SourceId.NativeAllocation:
                    HandleNativeAllocationDetails(source, fallbackName, fallbackDescription);
                    break;
                    
                case SourceIndex.SourceId.NativeRootReference:
                    HandleNativeRootReferenceDetails(source, fallbackName, fallbackDescription, childCount);
                    break;
                    
                default:
                    break;
            }
        }

        #region Handler Methods (参考Unity各个Handle方法)

        /// <summary>
        /// 处理Type类型的详情
        /// 参考: Unity Line 62-78
        /// </summary>
        private void HandleTypeDetails(UnifiedType type)
        {
            if (!type.IsValid)
                return;

            // 设置标题 (Line 67)
            m_Adapter.SetItemName(type);

            // 如果有Managed类型数据且不是基类型回退，显示静态字段检查器 (Line 69-70)
            if (type.ManagedTypeData.IsValid && !type.ManagedTypeIsBaseTypeFallback)
            {
                var fields = m_FieldsBuilder.BuildFieldsTree(type.ManagedTypeData);
                m_Adapter.SetupManagedObjectInspector(fields);
            }

            // 设置描述 (Line 72)
            m_Adapter.SetDescription("The selected item is a Type.");

            // 清空现有内容
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameBasic);
            
            // 添加类型信息到Basic分组 (Line 74-77)
            if (type.HasManagedType)
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Managed Type", type.ManagedTypeName);
            }
            
            if (type.HasNativeType)
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Native Type", type.NativeTypeName);
            }

        }

        /// <summary>
        /// 处理Object类型的详情 (分发器)
        /// 参考: Unity Line 80-103
        /// </summary>
        private void HandleObjectDetails(UnifiedType type)
        {
            if (!m_CurrentSelectionObjectData.IsValid)
                return;

            var selectedUnityObject = new UnifiedUnityObjectInfo(m_CachedSnapshot, type, m_CurrentSelectionObjectData);

            if (!selectedUnityObject.IsValid)
            {
                if (m_CurrentSelectionObjectData.isManaged)
                {
                    HandlePureCSharpObjectDetails(type);
                }
                else
                {
                    // 非托管且无效 - 显示无效对象信息
                    HandleInvalidObjectDetails(type);
                }
            }
            else
            {
                HandleUnityObjectDetails(selectedUnityObject);
            }
        }

        /// <summary>
        /// 处理纯C#对象的详情
        /// 参考: Unity Line 317-400
        /// </summary>
        private void HandlePureCSharpObjectDetails(UnifiedType type)
        {
            // Line 320: 设置标题
            m_Adapter.SetItemName(m_CurrentSelectionObjectData, type);

            // Line 322-324: 显示Managed Size
            var managedObjectInfo = m_CurrentSelectionObjectData.GetManagedObject(m_CachedSnapshot);
            var managedSize = managedObjectInfo.Size;
            
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameBasic);
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Managed Size", 
                EditorUtility.FormatBytes(managedSize), 
                $"{managedSize:N0} B");

            // Line 325-341: 如果是数组，显示Length和Rank信息
            if (m_CurrentSelectionObjectData.dataType == ObjectDataType.Array)
            {
                var arrayInfo = m_CurrentSelectionObjectData.GetArrayInfo(m_CachedSnapshot);
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Length", arrayInfo.Length.ToString());
                
                if (arrayInfo.Rank.Length > 1)
                {
                    m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Rank", 
                        m_CurrentSelectionObjectData.GenerateArrayDescription(m_CachedSnapshot, includeTypeName: false));
                    
                    // 检测零维度数组（潜在逻辑错误）
                    for (int i = 0; i < arrayInfo.Rank.Length; i++)
                    {
                        if (arrayInfo.Rank[i] == 0)
                        {
                            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Potential Logic Flaw?", 
                                "This multidimensional array has a zero sized dimension and therefore no elements. Is this intended?");
                            break;
                        }
                    }
                }
            }
            // Line 342-347: 如果是字符串，显示Length和String Value
            else if (type.ManagedTypeIndex == m_CachedSnapshot.TypeDescriptions.ITypeString)
            {
                var str = Unity.MemoryProfiler.Editor.StringTools.ReadString(managedObjectInfo.data, out var fullLength, m_CachedSnapshot.VirtualMachineInformation);
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Length", fullLength.ToString());
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "String Value", 
                    $"\"{str}\"", 
                    options: DynamicElementOptions.PlaceFirstInGroup | DynamicElementOptions.SelectableLabel);
            }

            // Line 348: 显示Referenced By (引用计数)
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Referenced By", managedObjectInfo.RefCount.ToString());

            // Line 349: 设置Managed对象检查器
            var managedFields = m_FieldsBuilder.BuildFieldsTree(m_CurrentSelectionObjectData);
            m_Adapter.SetupManagedObjectInspector(managedFields);

            // Line 351-396: GCHandle存活性分析（复杂的三种状态检测）
            HandleGCHandleLivenessAnalysis(managedObjectInfo);

            // Line 398-399: 显示Managed Address
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameAdvanced);
            var objectAddress = m_CurrentSelectionObjectData.GetObjectPointer(m_CachedSnapshot, false);
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Managed Address", 
                Unity.MemoryProfiler.Editor.DetailFormatter.FormatPointer(objectAddress));

            // 添加 Managed Callstack (如果有数据)
            AddManagedCallStacksInfoToUI(objectAddress);

        }

        /// <summary>
        /// 处理GCHandle存活性分析
        /// 参考: Unity Line 351-396
        /// 检测三种状态：
        /// 1. Used By Native Code - 被Native代码持有
        /// 2. Held By GCHandle - 被GCHandle持有（非UnityObject相关）
        /// 3. Unknown Liveness Reason - 未知原因（可能是Bug）
        /// </summary>
        private void HandleGCHandleLivenessAnalysis(ManagedObjectInfo managedObjectInfo)
        {
            var references = new List<ObjectData>();
            ObjectConnection.GetAllReferencingObjects(m_CachedSnapshot,
                new SourceIndex(SourceIndex.SourceId.ManagedObject, managedObjectInfo.ManagedObjectIndex), 
                ref references, 
                searchMode: ObjectConnection.UnityObjectReferencesSearchMode.Raw);

            // GCHandle引用不会出现在references列表中，但会增加RefCount
            // 如果 RefCount > references.Count，说明有GCHandle持有
            if (managedObjectInfo.RefCount > references.Count)
            {
                // 状态1: Used By Native Code
                if (m_CachedSnapshot.CrawledData.IndicesOfManagedObjectsHeldByRequiredByNativeCodeAttribute.Contains(managedObjectInfo.ManagedObjectIndex))
                {
                    m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, 
                        TextContent.UsedByNativeCodeStatus, 
                        TextContent.UsedByNativeCodeHint);
                }
                // 状态2: Held By GCHandle (非UnityObject相关)
                else if (m_CachedSnapshot.CrawledData.IndicesOfManagedObjectsHeldByNonNativeObjectRelatedGCHandle.Contains(managedObjectInfo.ManagedObjectIndex))
                {
                    m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, 
                        TextContent.HeldByGCHandleStatus, 
                        TextContent.HeldByGCHandleHint);
                }
                // 状态3: Unknown Liveness Reason（深入检查）
                else
                {
                    // 手动检查GcHandles表
                    bool heldByGCHandle = false;
                    for (long i = 0; i < m_CachedSnapshot.GcHandles.UniqueCount; i++)
                    {
                        if (m_CachedSnapshot.GcHandles.Target[i] == managedObjectInfo.PtrObject)
                        {
                            heldByGCHandle = true;
                            break;
                        }
                    }

                    if (heldByGCHandle)
                    {
                        // 被UnityObject相关的GCHandle持有
                        m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, 
                            TextContent.UnityObjectHeldByGCHandleStatus, 
                            TextContent.HeldByGCHandleHint);
                    }
                    else
                    {
                        // 真正的未知原因 - 可能是Managed Crawler的Bug
                        m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, 
                            TextContent.UnkownLivenessReasonStatus, 
                            TextContent.UnkownLivenessReasonHint);
                    }
                }
            }
        }

        /// <summary>
        /// 处理Unity对象的详情
        /// 参考: Unity Line 407-527
        /// </summary>
        private void HandleUnityObjectDetails(UnifiedUnityObjectInfo selectedUnityObject)
        {
            // Line 409: 设置标题
            m_Adapter.SetItemName(selectedUnityObject);

            // Line 410-416: 获取RootSize
            NativeRootSize rootSize = default;
            if (selectedUnityObject.HasNativeSide)
            {
                var rootId = m_CachedSnapshot.NativeObjects.RootReferenceId[selectedUnityObject.NativeObjectIndex];
                var mappedIndex = m_CachedSnapshot.ProcessedNativeRoots.RootIdToMappedIndex(rootId);
                rootSize = m_CachedSnapshot.ProcessedNativeRoots.Data[mappedIndex].AccumulatedRootSizes;
            }

            // Line 418-422: 计算各类内存大小
            var nativeSize = rootSize.NativeSize.Committed;
            var managedSize = selectedUnityObject.HasManagedSide ? (ulong)selectedUnityObject.ManagedSize : 0UL;
            var graphicsSize = rootSize.GfxSize.Committed;

            // Line 423: 判断是否需要分层显示
            bool multipleSizesToDisplay = (nativeSize > 0 ? 1 : 0) + (graphicsSize > 0 ? 1 : 0) + (managedSize > 0 ? 1 : 0) > 1;
            var totalAllocated = nativeSize;

            // 清空Basic组
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameBasic);

            // Line 426-430: 显示Total Allocated（如果有多个尺寸）
            if (multipleSizesToDisplay)
            {
                totalAllocated += managedSize + graphicsSize;
                AddBasicGroupSizeUILine("Total Allocated", totalAllocated);
            }

            // Line 432-436: 显示Native Size
            if (nativeSize > 0)
            {
                var titleText = multipleSizesToDisplay ? "├ Native Size" : "Native Size";
                AddBasicGroupSizeUILine(titleText, nativeSize);
            }

            // Line 438-442: 显示Managed Size
            if (selectedUnityObject.HasManagedSide)
            {
                string titleText = graphicsSize > 0 ? "├ Managed Size" : nativeSize > 0 ? "└ Managed Size" : "Managed Size";
                AddBasicGroupSizeUILine(titleText, managedSize);
            }

            // Line 444-448: 显示Graphics Size
            if (graphicsSize > 0)
            {
                var titleText = multipleSizesToDisplay ? "└ Graphics Size" : "Graphics Size";
                AddBasicGroupSizeUILine(titleText, graphicsSize);
            }

            // Line 450-454: 显示Total Resident Size
            var totalResidentSize = rootSize.NativeSize.Resident + rootSize.ManagedSize.Resident;
            if (totalResidentSize > 0)
            {
                AddBasicGroupSizeUILine("Total Resident Size", totalResidentSize);
            }

            // Line 456-461: 显示Runtime estimation
            if (selectedUnityObject.HasNativeSide)
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Runtime estimation",
                    $"{EditorUtility.FormatBytes((long)selectedUnityObject.NativeSize)}",
                    $"{selectedUnityObject.NativeSize:N0} B\n\nThis is the value that would've been returned by calling GetRuntimeMemorySizeLong at runtime. It combines native and gpu allocation estimates.");
            }

            // Line 463-464: 显示Referenced By（分Native/Managed）
            var refCountExtra = (selectedUnityObject.IsFullUnityObjet && selectedUnityObject.TotalRefCount > 0) 
                ? $"({selectedUnityObject.NativeRefCount} Native + {selectedUnityObject.ManagedRefCount} Managed)" 
                : string.Empty;
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Referenced By", 
                $"{selectedUnityObject.TotalRefCount} {refCountExtra}{(selectedUnityObject.IsFullUnityObjet ? " + 2 Self References" : "")}");

            // Line 466-469: Bug检测（Invalid Managed Object）
            if (selectedUnityObject.IsFullUnityObjet && !selectedUnityObject.ManagedObjectData.IsValid)
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Bug!", 
                    "This Native Object is associated with an invalid Managed Object, " + TextContent.InvalidObjectPleaseReportABugMessage);
            }

            // Line 471-487: MetaData显示
            if (selectedUnityObject.HasNativeSide && Unity.MemoryProfiler.Editor.Containers.MetaDataHelpers.GenerateMetaDataString(m_CachedSnapshot, selectedUnityObject.NativeObjectIndex, out var metaData))
            {
                foreach (var tuple in metaData)
                {
                    if (tuple.Item1 == "Warning")
                    {
                        var infoBox = new InfoBox
                        {
                            Level = InfoBox.IssueLevel.Warning,
                            Message = tuple.Item2
                        };
                        m_Adapter.AddInfoBox(SelectionDetailsPanelAdapter.GroupNameMetaData, infoBox);
                    }
                    else
                    {
                        m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameMetaData, tuple.Item1, tuple.Item2, "");
                    }
                }
            }

            // Line 489-509: Advanced组显示
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameAdvanced);
            
            if (selectedUnityObject.HasNativeSide)
            {
                // Instance ID
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Instance ID", selectedUnityObject.InstanceId.ToString());
                
                // Flags and HideFlags（使用我们自己的PathsToRootTreeNode中的逻辑）
                var flagsLabel = "";
                var flagsTooltip = "";
                var hideFlagsLabel = "";
                var hideFlagsTooltip = "";
                
                // 使用PathsToRootTreeNode的GetObjectFlagsStrings方法
                Models.PathsToRootTreeNode.GetObjectFlagsStrings(
                    selectedUnityObject.NativeObjectData, m_CachedSnapshot,
                    ref flagsLabel, ref flagsTooltip,
                    ref hideFlagsLabel, ref hideFlagsTooltip, false);
                
                if (string.IsNullOrEmpty(flagsLabel))
                    flagsLabel = "None";
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Flags", flagsLabel, flagsTooltip);
                
                if (string.IsNullOrEmpty(hideFlagsLabel))
                    hideFlagsLabel = "None";
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "HideFlags", hideFlagsLabel, hideFlagsTooltip);
                
                // Native Address
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Native Address", 
                    Unity.MemoryProfiler.Editor.DetailFormatter.FormatPointer(selectedUnityObject.NativeObjectData.GetObjectPointer(m_CachedSnapshot, false)));
            }
            
            if (selectedUnityObject.HasManagedSide)
            {
                // Managed Address
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Managed Address", 
                    Unity.MemoryProfiler.Editor.DetailFormatter.FormatPointer(selectedUnityObject.ManagedObjectData.GetObjectPointer(m_CachedSnapshot, false)));
            }

            // Line 515: UpdateStatusAndHint（Phase 2.5实现）
            UpdateStatusAndHint(selectedUnityObject);

            // Line 517-519: Self References提示
            if (selectedUnityObject.IsFullUnityObjet)
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Self References", 
                    "The Managed and Native parts of this UnityEngine.Object reference each other. This is normal."
                    + (selectedUnityObject.TotalRefCount == 0 ? " Nothing else references them though so the Native part keeps the Managed part alive." : ""));
            }

            // Line 521-524: 设置Managed对象检查器
            if (selectedUnityObject.HasManagedSide)
            {
                var unityObjectFields = m_FieldsBuilder.BuildFieldsTree(selectedUnityObject.ManagedObjectData);
                m_Adapter.SetupManagedObjectInspector(unityObjectFields);
            }
            else
            {
            }

            // Line 525-526: CallStacks
            if (selectedUnityObject.HasNativeSide && m_CachedSnapshot.NativeCallstackSymbols.Count > 0)
                AddCallStacksInfoToUI(new SourceIndex(SourceIndex.SourceId.NativeObject, selectedUnityObject.NativeObjectIndex));

        }

        /// <summary>
        /// 处理Native Allocation的详情
        /// 参考: Unity Line 105-142
        /// </summary>
        private void HandleNativeAllocationDetails(SourceIndex source, string fallbackName, string fallbackDescription)
        {
            // Line 107: 设置名称
            m_Adapter.SetItemName(source);
            
            // Line 108: 获取Native Size
            var nativeSize = (long)m_CachedSnapshot.NativeAllocations.Size[source.Index];
            
            // 清空Basic组
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameBasic);
            
            // Line 112: 显示Native Size
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Native Size", 
                EditorUtility.FormatBytes(nativeSize), 
                $"{nativeSize:N0} B");

            // Line 110-114: 获取并显示Found References（简化实现，不使用Feature Flag）
            var references = new List<ObjectData>();
            ObjectConnection.GetAllReferencingObjects(m_CachedSnapshot, source, ref references);
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Found References", 
                references.Count.ToString(), 
                TextContent.NativeAllocationFoundReferencesHint);

            // Line 116-127: 检查Unknown Unknown Allocation错误
            // 当RootReferenceId <= 0时，说明这是"Unknown Unknown Allocation"（无根引用的分配）
            var rootReferenceId = m_CachedSnapshot.NativeAllocations.RootReferenceId[source.Index];
            if (rootReferenceId <= 0)
            {
                // 显示错误InfoBox（简化实现，不检查FeatureFlag和InternalMode）
                m_Adapter.AddInfoBox(SelectionDetailsPanelAdapter.GroupNameBasic, new InfoBox
                {
                    Level = InfoBox.IssueLevel.Error,
                    Message = TextContent.UnknownUnknownAllocationsErrorBoxMessage,
                });
            }

            // Line 129-141: CallStacks显示
            if (m_CachedSnapshot.NativeCallstackSymbols.Count > 0)
            {
                AddCallStacksInfoToUI(source);
            }
            else
            {
                // Line 134-141: 给出CallStacks提示（简化实现）
                m_Adapter.AddInfoBox(SelectionDetailsPanelAdapter.GroupNameBasic, new InfoBox
                {
                    Level = InfoBox.IssueLevel.Info,
                    Message = TextContent.NativeAllocationInternalModeCallStacksInfoBoxMessage,
                });
            }

        }

        /// <summary>
        /// 处理Graphics Resource的详情
        /// 参考: Unity Line 189-217
        /// </summary>
        private void HandleGfxResourceDetails(SourceIndex source, string fallbackName, string fallbackDescription)
        {
            // Line 191: 设置名称（使用fallbackName）
            m_Adapter.SetItemName(fallbackName ?? "Graphics Resource");
            
            // Line 192-194: 获取Graphics相关信息
            var gfxSize = (long)m_CachedSnapshot.NativeGfxResourceReferences.GfxSize[source.Index];
            var rootId = (long)m_CachedSnapshot.NativeGfxResourceReferences.RootId[source.Index];
            var gfxResourceId = (long)m_CachedSnapshot.NativeGfxResourceReferences.GfxResourceId[source.Index];
            
            // 清空组
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameBasic);
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameAdvanced);
            
            // Line 195: 显示Graphics Size
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Graphics Size", 
                EditorUtility.FormatBytes(gfxSize), 
                $"{gfxSize:N0} B");
            
            // Line 196-197: 显示Root ID和Gfx Resource ID（Advanced组）
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Root ID", rootId.ToString());
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameAdvanced, "Gfx Resource ID", gfxResourceId.ToString());

            // Line 199-206: 检查是否是Unrooted Graphics Resource（错误）
            if (rootId <= 0)
            {
                m_Adapter.AddInfoBox(SelectionDetailsPanelAdapter.GroupNameBasic, new InfoBox
                {
                    Level = InfoBox.IssueLevel.Error,
                    Message = TextContent.UnrootedGraphcisResourceErrorBoxMessage,
                });
            }
            // Line 207-215: 如果有CallStacks，显示提示和CallStacks
            else if (m_CachedSnapshot.NativeCallstackSymbols.Count > 0)
            {
                m_Adapter.AddInfoBox(SelectionDetailsPanelAdapter.GroupNameCallStacks, new InfoBox
                {
                    Level = InfoBox.IssueLevel.Info,
                    Message = TextContent.GraphcisResourceWithSnapshotWithCallStacksInfoBoxMessage,
                });
                
                AddCallStacksInfoToUI(source);
            }

        }

        /// <summary>
        /// 处理Native Root Reference的详情
        /// 参考: Unity Line 218-303
        /// </summary>
        private void HandleNativeRootReferenceDetails(SourceIndex source, string fallbackName, string fallbackDescription, long childCount)
        {
            // Line 220: 设置名称
            m_Adapter.SetItemName(source);
            
            // Line 221: 获取Root Reference的Area和Object名称
            GetRootReferenceName(m_CachedSnapshot, source, out var areaName, out var objectName);
            
            // 清空Basic组
            m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameBasic);
            
            // Line 223-224: 显示Area
            if (!string.IsNullOrEmpty(areaName))
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Area", areaName);
            
            // Line 226-227: 显示Object Name
            if (!string.IsNullOrEmpty(objectName))
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Object Name", objectName);
            
            // Line 229-230: 显示Accumulated Size
            var accumulatedSize = (long)m_CachedSnapshot.NativeRootReferences.AccumulatedSize[source.Index];
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Size", 
                EditorUtility.FormatBytes(accumulatedSize), 
                $"{accumulatedSize:N0} B");
            
            // Line 232-233: 显示Child Count（如果有）
            if (childCount > 0)
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Child Count", childCount.ToString());
            
            // Line 235-276: 动态分配分解按钮
            // TODO: Phase 2 - 实现FeatureFlag和Settings系统后再添加此功能
            // 需要MemoryProfilerSettings.FeatureFlags.EnableDynamicAllocationBreakdown_2024_10
            // 需要MemoryProfilerSettings.AllocationRootsToSplit等设置
            
        }

        /// <summary>
        /// 获取Root Reference的名称
        /// 参考: Unity Line 279-287
        /// </summary>
        private static void GetRootReferenceName(CachedSnapshot snapshot, SourceIndex sourceIndex, out string areaName, out string objectName)
        {
            areaName = "";
            objectName = "";
            
            if (sourceIndex.Id != SourceIndex.SourceId.NativeRootReference)
                return;
            
            areaName = snapshot.NativeRootReferences.AreaName[sourceIndex.Index];
            objectName = snapshot.NativeRootReferences.ObjectName[sourceIndex.Index];
        }

        /// <summary>
        /// 处理无效对象的详情
        /// 参考: Unity Line 305-315
        /// </summary>
        private void HandleInvalidObjectDetails(UnifiedType type)
        {
            m_Adapter.SetItemName("Invalid Object");

            m_Adapter.AddInfoBox(SelectionDetailsPanelAdapter.GroupNameBasic, new InfoBox()
            {
                Level = InfoBox.IssueLevel.Info,
                Message = UIContent.TextContent.InvalidObjectErrorBoxMessage,
            });

        }

        /// <summary>
        /// 处理分组的详情
        /// 参考: Unity Line 747-752
        /// </summary>
        private void HandleGroupDetails(string title, string description)
        {
            m_Adapter.SetItemName(title);
            m_Adapter.SetDescription(description);

        }

        #endregion

        #region Helper Methods (辅助方法占位符)

        /// <summary>
        /// 添加基本分组的Size显示行
        /// 参考: Unity Line 402-405
        /// </summary>
        private void AddBasicGroupSizeUILine(string description, ulong value)
        {
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, description, 
                EditorUtility.FormatBytes((long)value), 
                $"{value:N0} B");
        }

        #endregion

        #region UpdateStatusAndHint (参考Unity Line 754-904)

        /// <summary>
        /// 更新Unity对象的Status和Hint信息
        /// 参考: Unity Line 754-775
        /// </summary>
        private void UpdateStatusAndHint(UnifiedUnityObjectInfo selectedUnityObject)
        {
            if (selectedUnityObject.IsLeakedShell)
            {
                UpdateStatusAndHintForLeakedShellObject(selectedUnityObject);
            }
            else if (selectedUnityObject.IsAssetObject && !selectedUnityObject.IsPersistentAsset)
            {
                UpdateStatusAndHintForDynamicAssets(selectedUnityObject);
            }
            else if (selectedUnityObject.IsPersistentAsset)
            {
                UpdateStatusAndHintForPersistentAssets(selectedUnityObject);
            }
            else if (selectedUnityObject.IsSceneObject)
            {
                UpdateStatusAndHintForSceneObjects(selectedUnityObject);
            }
            else if (selectedUnityObject.IsManager)
            {
                UpdateStatusAndHintForManagers(selectedUnityObject);
            }
        }

        /// <summary>
        /// 更新Leaked Shell对象的Status和Hint
        /// 参考: Unity Line 777-797
        /// </summary>
        private void UpdateStatusAndHintForLeakedShellObject(UnifiedUnityObjectInfo selectedUnityObject)
        {
            var statusSummary = (selectedUnityObject.ManagedRefCount > 0 ? "Referenced " : "GC.Collect()-able ") +
                $"{TextContent.LeakedManagedShellName} of " +
                (selectedUnityObject.IsSceneObject ? "a Scene Object" : "an Asset");

            var hint = $"This Unity Object is a {TextContent.LeakedManagedShellName}. That means this object's type derives from UnityEngine.Object " +
                "and the object therefore, normally, has a Native Object accompanying it. " +
                "If it is used by Managed (C#) Code, a Managed Shell Object is created to allow access to the Native Object. " +
                "In this case, the Native Object has been destroyed, either via Destroy() or because the " +
                (selectedUnityObject.IsSceneObject ? "Scene" : "Asset Bundle") + " it was in was unloaded. " +
                "After the Native Object was destroyed, the Managed Garbage Collector hasn't yet collected this object. " +
                (selectedUnityObject.ManagedRefCount > 0
                    ? "This is because the Managed Shell is still being referenced and can therefore not yet be collected. " +
                    "You can fix this by explicitly setting each field referencing this Object to null (comparing it to null will claim it already is null, as it acts as a \"Fake Null\" object), " +
                    "or by ensuring that each referencing object is no longer referenced itself, so that all of them can be unloaded."
                    : "Nothing is referencing this Object anymore, so it should be collected with the next GC.Collect.");

            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, statusSummary, hint);
        }

        /// <summary>
        /// 更新动态Asset的Status和Hint
        /// 参考: Unity Line 799-842
        /// </summary>
        private void UpdateStatusAndHintForDynamicAssets(UnifiedUnityObjectInfo selectedUnityObject)
        {
            var statusSummary = string.Empty;
            if (selectedUnityObject.TotalRefCount > 0)
                statusSummary += "Referenced ";

            // State
            if (selectedUnityObject.IsDontUnload)
                statusSummary += "DontDestroyOnLoad ";
            else if (selectedUnityObject.TotalRefCount == 0)
                statusSummary += "Leaked ";

            // Runtime created or Combined Scene Meshes
            if (selectedUnityObject.IsRuntimeCreated)
                statusSummary += $"{(string.IsNullOrEmpty(statusSummary) ? 'D' : 'd')}ynamically & run-time created ";
            else
                statusSummary += $"{(string.IsNullOrEmpty(statusSummary) ? 'D' : 'd')}ynamically & build-time created ";

            statusSummary += "Asset";

            var newObjectTypeConstruction = "'new " + (selectedUnityObject.Type.HasManagedType ? selectedUnityObject.ManagedTypeName : selectedUnityObject.NativeTypeName) + "()'. ";

            var hint = "This is a dynamically created Asset Type object, that was either Instantiated, implicitly duplicated or explicitly constructed via " +
                newObjectTypeConstruction +
                (selectedUnityObject.IsDontUnload ? "It is marked as 'DontDestroyOnLoad', so that it will never be unloaded by a Scene unload or an explicit call to 'Resources.UnloadUnusedAssets()'." +
                    " If you want to get rid of it, you will need to call 'Destroy()' on it or not mark it as 'DontDestroyOnLoad'"
                    : ((selectedUnityObject.TotalRefCount > 0 ? "It is still referenced, but if it should no longer be used, it will need to be unloaded explicitly by calling 'Destroy()' on it or "
                        : "This object is not referenced anymore. It is therefore leaked! ") +
                        "Remember to unload these objects by explicitly calling 'Destroy()' on them, or via the more costly and broad sweeping indirect method of " +
                        k_TriggerAssetGCHint)
                );

            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, statusSummary, hint);

            if (selectedUnityObject.TotalRefCount == 0 && !selectedUnityObject.IsDontUnload
                && selectedUnityObject.IsRuntimeCreated && string.IsNullOrEmpty(selectedUnityObject.NativeObjectName))
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, "Tip", "This leaked dynamically created Asset doesn't have a name, which will make it harder to find its source. " +
                    "As a first step, search your entire project code for any instances of " + newObjectTypeConstruction +
                    " and every 'Instantiate()' or similar call that would create an instance of this type, " +
                    "and make sure you set the '.name' property of the resulting object to something that will make it easier to understand what this object is being created for.");
            }
        }

        /// <summary>
        /// 更新持久化Asset的Status和Hint
        /// 参考: Unity Line 844-877
        /// </summary>
        private void UpdateStatusAndHintForPersistentAssets(UnifiedUnityObjectInfo selectedUnityObject)
        {
            var statusSummary = (selectedUnityObject.TotalRefCount > 0 ? "Used " : "Unused ") +
                (selectedUnityObject.IsDontUnload ? "DontDestroyOnLoad " : string.Empty) +
                (selectedUnityObject.IsRuntimeCreated ? "Runtime Created " : "Loaded ") +
                "Asset";

            string hint;
            if (selectedUnityObject.IsRuntimeCreated)
                hint = "This is an Asset that was created at runtime and later associated with a file.";
            else
            {
                if (selectedUnityObject.TotalRefCount > 0)
                {
                    hint = "This is an Asset that is used by something in your Application. " +
                        "If you didn't expect to see this Asset at this point in your application's lifetime, check the References panel to see what is using it. " +
                        "If you expected it to be smaller, check the Asset's Import settings.";
                }
                else if (selectedUnityObject.IsDontUnload)
                {
                    hint = "This is an Asset that appears to no longer be used by anything in your Application but his held in Memory because it is marked as 'DontDestroyOnLoad'. " +
                        "To unload it, you need to call 'Destroy()' on it or not mark it as 'DontDestroyOnLoad'";
                }
                else
                {
                    hint = "This is an Asset that appears to no longer be used by anything in your Application. " +
                        "It may have been used earlier but now is just waiting for the next sweep of 'Resources.UnloadUnusedAssets()' to unload it. You can test that hypothesis by " +
                        k_TriggerAssetGCHint;
                }
            }

            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, statusSummary, hint);
        }

        /// <summary>
        /// 更新Scene对象的Status和Hint
        /// 参考: Unity Line 879-895
        /// </summary>
        private void UpdateStatusAndHintForSceneObjects(UnifiedUnityObjectInfo selectedUnityObject)
        {
            var statusSummary = (selectedUnityObject.IsRuntimeCreated ? "Runtime Created " : "Loaded ") +
                (selectedUnityObject.IsDontUnload ? "DontDestroyOnLoad " : string.Empty) +
                "Scene Object";

            var hint = "This is a Scene Object, i.e. a GameObject or a Component on it. " +
                (selectedUnityObject.IsRuntimeCreated ? "It was instantiated after the Scene was loaded. " : "It was loaded in as part of a Scene. ") +
                (selectedUnityObject.IsDontUnload ? "It is marked as 'DontDestroyOnLoad' so to unload it, you would need to call 'Destroy()' on it, or not mark it as 'DontDestroyOnLoad'" :
                    "Its Native Memory will be unloaded once the Scene it resides in is unloaded or the GameObject " +
                    (selectedUnityObject.IsGameObject ? "" : "it is attached to ") +
                    "or its parents are destroyed via 'Destroy()'. " +
                    (selectedUnityObject.HasManagedSide ? "Its Managed memory may live on as a Leaked Shell Object if something else that was not unloaded with it still references it." : ""));

            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, statusSummary, hint);
        }

        /// <summary>
        /// 更新Manager的Status和Hint
        /// 参考: Unity Line 897-904
        /// </summary>
        private void UpdateStatusAndHintForManagers(UnifiedUnityObjectInfo selectedUnityObject)
        {
            var statusSummary = "Native Manager";
            var hint = "This is Native Manager that is represents one of Unity's subsystems.";

            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameBasic, k_StatusLabelText, statusSummary, hint);
        }

        #endregion

        #region Phase 2.4: CallStacks完整实施 (参考: Unity Line 529-744)

        /// <summary>
        /// 添加CallStacks信息到UI
        /// 参考: Unity SelectedItemDetailsForTypesAndObjects.cs Line 529-632
        /// </summary>
        private void AddCallStacksInfoToUI(SourceIndex sourceIndex)
        {
            // 1. 获取rootId
            var rootId = sourceIndex.Id switch
            {
                SourceIndex.SourceId.NativeObject => m_CachedSnapshot.NativeObjects.RootReferenceId[sourceIndex.Index],
                SourceIndex.SourceId.NativeAllocation => m_CachedSnapshot.NativeAllocations.RootReferenceId[sourceIndex.Index],
                SourceIndex.SourceId.GfxResource => m_CachedSnapshot.NativeGfxResourceReferences.RootId[sourceIndex.Index],
                SourceIndex.SourceId.NativeRootReference => sourceIndex.Index,
                _ => throw new NotImplementedException()
            };

            var areaAndObjectName = m_CachedSnapshot.NativeAllocations.ProduceAllocationNameForRootReferenceId(
                m_CachedSnapshot, rootId, higlevelObjectNameOnlyIfAvailable: false);
            
            var callstackCount = 0L;
            var furthercallstackCount = 0L;
            var allocationCount = 0L;

            // 2. 定义内部函数：构建CallStack文本列表
            List<(string, string, string, List<System.Collections.Generic.KeyValuePair<int, string>>)> BuildCallStackTexts(
                long maxEntries, out long callstackCount, out long furthercallstackCount, out long allocationCount, bool forCopy = false)
            {
                var callStacks = new Unity.MemoryProfiler.Editor.Containers.DynamicArray<NativeAllocationSiteEntriesCache.CallStackInfo>(
                    10, Unity.Collections.Allocator.Temp);
                callStacks.Clear(false);
                callstackCount = furthercallstackCount = allocationCount = 0;

                var callStackTexts = new List<(string, string, string, List<System.Collections.Generic.KeyValuePair<int, string>>)>((int)maxEntries);
                BuildCallStackInfo(
                    ref callStackTexts, ref allocationCount, ref callstackCount, ref furthercallstackCount, ref callStacks,
                    sourceIndex, areaAndObjectName, maxUniqueEntries: maxEntries,
                    clickableCallStacks: forCopy ? false : m_ClickableCallStacks,
                    simplifyCallStacks: forCopy ? false : m_AddressInCallStacks);

                callStacks.Dispose();
                return callStackTexts;
            }

            // 3. 构建前10个CallStacks
            var callStackTexts = BuildCallStackTexts(10, out callstackCount, out furthercallstackCount, out allocationCount);
            
            // 4. 如果没有CallStacks，直接返回
            if (callstackCount == 0)
                return;

            // 5. 定义内部函数：复制CallStacks到剪贴板
            void CopyAllCallStacksToClipboard(List<(string, string, string, List<System.Collections.Generic.KeyValuePair<int, string>>)> callStackTexts)
            {
                var stringBuilder = new System.Text.StringBuilder();
                foreach (var item in callStackTexts)
                {
                    stringBuilder.AppendFormat("{0}\n{1}{2}\n\n", item.Item1, item.Item2, item.Item3);
                }
                // WPF剪贴板
                System.Windows.Clipboard.SetText(stringBuilder.ToString());
            }

            // 6. 添加Copy按钮（智能文本）
            var copyButtonText = $"Copy {(furthercallstackCount > 0 ? "First " : (callstackCount > 1 ? "All " : ""))}{callstackCount} Call Stack{(callstackCount > 1 ? "s" : "")}";
            
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks, copyButtonText, copyButtonText, copyButtonText + " to the clipboard.",
                DynamicElementOptions.Button, 
                () => CopyAllCallStacksToClipboard(BuildCallStackTexts(callstackCount, out var _, out var _, out var _, forCopy: true)));

            // 7. 如果有更多CallStacks，添加Copy All按钮和Further Call Stacks计数
            if (furthercallstackCount > 0)
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks, $"Copy All Call Stacks", "Copy All Call Stacks", 
                    $"Copy All {callstackCount + furthercallstackCount} Call Stacks to the clipboard.",
                    DynamicElementOptions.Button, 
                    () => CopyAllCallStacksToClipboard(BuildCallStackTexts(callstackCount + furthercallstackCount, out var _, out var _, out var _, forCopy: true)));

                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks, $"Further Call Stacks", furthercallstackCount.ToString());
            }

            // 8. 添加Allocations Count
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks, "Allocations Count", allocationCount.ToString());
            
            // 9. 添加Call Stacks计数
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks, 
                $"{(furthercallstackCount > 0 ? "Shown " : "")}Call Stacks", callstackCount.ToString());

            // 10. 添加Clickable Call Stacks Toggle
            // 注意：WPF中无法跳转到源文件，但保留Toggle以对齐Unity
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks, "Clickable Call Stacks", "Clickable Call Stacks",
                "Call Stacks can either be clickable (leading to the source file) or selectable. Toggle this off if you want them to be selectable.\nNote: File jumping is not available in WPF version.",
                DynamicElementOptions.Toggle | (m_ClickableCallStacks ? DynamicElementOptions.ToggleOn : 0), 
                () =>
                {
                    m_ClickableCallStacks = !m_ClickableCallStacks;
                    m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameCallStacks);
                    AddCallStacksInfoToUI(sourceIndex);  // 递归调用，重新渲染
                });

            // 11. 添加Show Address in Call Stacks Toggle
            m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks, "Show Address in Call Stacks", "Show Address in Call Stacks",
                "Show or hide Address in Call Stacks.",
                DynamicElementOptions.Toggle | (m_AddressInCallStacks ? DynamicElementOptions.ToggleOn : 0), 
                () =>
                {
                    m_AddressInCallStacks = !m_AddressInCallStacks;
                    m_Adapter.ClearGroup(SelectionDetailsPanelAdapter.GroupNameCallStacks);
                    AddCallStacksInfoToUI(sourceIndex);  // 递归调用，重新渲染
                });

            // 12. TODO: 如果是内部模式，添加导出和TreeView按钮
            // if (MemoryProfilerSettings.InternalMode)
            // {
            //     AddCallstacksExportAndTreeViewButtons(rootId);
            // }

            // 13. 使用SubFoldout显示每个CallStack
            const bool k_UseFullDetailsPanelWidth = true;
            foreach (var text in callStackTexts)
            {
                m_Adapter.AddDynamicElement(SelectionDetailsPanelAdapter.GroupNameCallStacks,
                    text.Item1, k_UseFullDetailsPanelWidth ? $"{text.Item2}{text.Item3}" : text.Item2 + text.Item3, 
                    options:
                    (k_UseFullDetailsPanelWidth ? 0 : DynamicElementOptions.ShowTitle) | 
                    DynamicElementOptions.SubFoldout |
                    // TextField (which enables Selectable Label) does not properly support rich text, so these options are mutually exclusive
                    (m_ClickableCallStacks ? DynamicElementOptions.EnableRichText : DynamicElementOptions.SelectableLabel)
                    );
                
                // 14. 添加文件链接Hash (如果有)
                // 注意：WPF版本中这个功能无法实现（无法跳转到文件）
                if (text.Item4 != null)
                {
                    // TODO: WPF版本中暂时跳过文件链接功能
                    // foreach (var item in text.Item4)
                    // {
                    //     m_UI.AddLinkHashToLinkText(item.Key, item.Value);
                    // }
                }
            }

        }

        /// <summary>
        /// 构建CallStack信息 (重载1: 处理SourceIndex，分发到Allocation索引)
        /// 参考: Unity SelectedItemDetailsForTypesAndObjects.cs Line 645-678
        /// </summary>
        private void BuildCallStackInfo(ref List<(string, string, string, List<System.Collections.Generic.KeyValuePair<int, string>>)> callStackTexts, 
            ref long allocationCount,
            ref long callstackCount, ref long furtherCallstacks,
            ref Unity.MemoryProfiler.Editor.Containers.DynamicArray<NativeAllocationSiteEntriesCache.CallStackInfo> callStacks, 
            SourceIndex sourceIndex,
            string areaAndObjectName, long startIndex = 0, long maxUniqueEntries = 10, bool clickableCallStacks = true, bool simplifyCallStacks = true)
        {
            var rootId = sourceIndex.Id switch
            {
                SourceIndex.SourceId.NativeObject => m_CachedSnapshot.NativeObjects.RootReferenceId[sourceIndex.Index],
                SourceIndex.SourceId.NativeAllocation => m_CachedSnapshot.NativeAllocations.RootReferenceId[sourceIndex.Index],
                SourceIndex.SourceId.GfxResource => m_CachedSnapshot.NativeGfxResourceReferences.RootId[sourceIndex.Index],
                SourceIndex.SourceId.NativeRootReference => sourceIndex.Index,
                _ => throw new NotImplementedException()
            };

            // 如果是NativeAllocation，直接处理单个Allocation
            if (sourceIndex.Id == SourceIndex.SourceId.NativeAllocation)
            {
                BuildCallStackInfo(ref callStackTexts, ref allocationCount, ref callstackCount, ref furtherCallstacks,
                    ref callStacks, sourceIndex.Index, areaAndObjectName,
                    maxUniqueEntries, clickableCallStacks, simplifyCallStacks);
                return;
            }

            // 否则遍历所有NativeAllocations，找到匹配rootId的
            for (long i = startIndex; i < m_CachedSnapshot.NativeAllocations.RootReferenceId.Count; i++)
            {
                if (m_CachedSnapshot.NativeAllocations.RootReferenceId[i] == rootId)
                {
                    var continueBuilding = BuildCallStackInfo(ref callStackTexts, ref allocationCount, ref callstackCount, ref furtherCallstacks,
                        ref callStacks, i, areaAndObjectName,
                        maxUniqueEntries, clickableCallStacks, simplifyCallStacks);
                    if (!continueBuilding)
                        break;
                }
            }
        }

        /// <summary>
        /// 构建CallStack信息 (重载2: 处理单个NativeAllocation)
        /// 参考: Unity SelectedItemDetailsForTypesAndObjects.cs Line 693-744
        /// </summary>
        /// <returns>Returns false if maxUniqueEntries has been reached and further allocations should not be examined</returns>
        private bool BuildCallStackInfo(ref List<(string, string, string, List<System.Collections.Generic.KeyValuePair<int, string>>)> callStackTexts, 
            ref long allocationCount,
            ref long callstackCount, ref long furtherCallstacks,
            ref Unity.MemoryProfiler.Editor.Containers.DynamicArray<NativeAllocationSiteEntriesCache.CallStackInfo> callStacks, 
            long nativeAllocationIndex,
            string areaAndObjectName, long maxUniqueEntries = 10, bool clickableCallStacks = true, bool simplifyCallStacks = true)
        {
            ++allocationCount;
            
            // 1. 获取AllocationSite和CallStackInfo
            var siteId = m_CachedSnapshot.NativeAllocations.AllocationSiteId[nativeAllocationIndex];
            if (siteId == NativeAllocationSiteEntriesCache.SiteIdNullPointer)
                return true;
            var callstackInfo = m_CachedSnapshot.NativeAllocationSites.GetCallStackInfo(siteId);
            if (!callstackInfo.Valid)
                return true;
            
            // 2. 获取Allocation信息
            var address = m_CachedSnapshot.NativeAllocations.Address[nativeAllocationIndex];
            var regionIndex = m_CachedSnapshot.NativeAllocations.MemoryRegionIndex[nativeAllocationIndex];
            var region = m_CachedSnapshot.NativeMemoryRegions.MemoryRegionName[regionIndex];
            
            // 3. 去重：检查是否已有相同的CallStack
            var isNew = true;
            for (long j = 0; j < callStacks.Count; j++)
            {
                if (callStacks[j].Equals(callstackInfo))
                {
                    // 追加Allocation信息到现有CallStack
                    var texts = callStackTexts[(int)j];
                    texts.Item2 += $"\nAnd Allocation {Unity.MemoryProfiler.Editor.DetailFormatter.FormatPointer(address)} made in {region} : {areaAndObjectName}";
                    callStackTexts[(int)j] = texts;
                    isNew = false;
                    break;
                }
            }
            if (!isNew)
                return true;

            // 4. 检查是否达到maxUniqueEntries
            if (callStackTexts.Count >= maxUniqueEntries)
            {
                if (furtherCallstacks == -1)
                {
                    --allocationCount;
                    return false;  // 停止构建
                }
                ++furtherCallstacks;
                return true;
            }
            
            // 5. 获取ReadableCallstack
            var callstack = m_CachedSnapshot.NativeAllocationSites.GetReadableCallstack(
                m_CachedSnapshot.NativeCallstackSymbols, callstackInfo.Index, 
                simplifyCallStacks: simplifyCallStacks, 
                clickableCallStacks: clickableCallStacks);
            
            // 6. 添加到callStackTexts
            if (!string.IsNullOrEmpty(callstack.Callstack))
            {
                callStackTexts.Add((
                    $"Call Stack {++callstackCount}",
                    $"Allocation {Unity.MemoryProfiler.Editor.DetailFormatter.FormatPointer(address)} made in {region} : {areaAndObjectName}",
                    $"\n\n{callstack.Callstack}",
                    callstack.FileLinkHashToFileName));
            }
            
            return true;
        }

        #endregion

        #region Managed CallStacks

        /// <summary>
        /// 添加 Managed CallStacks 信息到 UI
        /// </summary>
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

        /// <summary>
        /// 构建 CallStack TreeListView 节点
        /// </summary>
        private List<CallStackNode> BuildCallStackTreeNodes(Unity.MemoryProfiler.Editor.Managed.CallStack callStack)
        {
            var nodes = new List<CallStackNode>();

            // 从调用栈底部（最外层）到顶部（分配点）遍历
            // 参考 ManagedObjectsDataBuilder 的实现
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

        #endregion
    }
}

