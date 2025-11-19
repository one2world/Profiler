using System;
using System.Collections.Generic;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Format;
using Unity.MemoryProfiler.Editor.Managed;
using Unity.MemoryProfiler.UI.Models;

namespace Unity.MemoryProfiler.UI.Services
{
    /// <summary>
    /// Managed对象字段构建器 - 将ObjectData解析为字段树
    /// 参考: Unity.MemoryProfiler.Editor.UI.ManagedObjectInspector
    /// </summary>
    internal class ManagedFieldsBuilder
    {
        private readonly CachedSnapshot m_CachedSnapshot;
        private readonly DetailFormatter m_Formatter;
        private readonly Dictionary<ulong, int> m_IdentifyingPointerToId = new Dictionary<ulong, int>();
        private int m_NextId = 0;
        
        // 递归和深度限制（参考Unity Line 36-38）
        private const int k_MaxDepth = 10;
        private const int k_MaxArrayElements = 100;
        
        public ManagedFieldsBuilder(CachedSnapshot snapshot)
        {
            m_CachedSnapshot = snapshot;
            m_Formatter = new DetailFormatter(snapshot);
        }

        /// <summary>
        /// 从ObjectData构建字段树
        /// 参考: Unity ManagedObjectInspector.SetupManagedObject (Line 179-197)
        /// </summary>
        public List<ManagedFieldInfo> BuildFieldsTree(ObjectData managedObjectData)
        {
            m_IdentifyingPointerToId.Clear();
            m_NextId = 0;

            var rootFields = new List<ManagedFieldInfo>();

            if (!managedObjectData.IsValid)
            {
                return rootFields;
            }

            // 特殊处理：Type类型（静态字段）
            if (managedObjectData.dataType == ObjectDataType.Type)
            {
                ProcessManagedObjectFields(rootFields, managedObjectData, 0);
                return rootFields;
            }

            if (!managedObjectData.isManaged)
            {
                return rootFields;
            }

            // 处理对象字段
            ProcessManagedObjectFields(rootFields, managedObjectData, 0);

            return rootFields;
        }

        /// <summary>
        /// 处理Managed对象的字段
        /// 参考: Unity ManagedObjectInspector.ProcessManagedObjectFields (Line 279-311)
        /// </summary>
        private void ProcessManagedObjectFields(List<ManagedFieldInfo> parentList, ObjectData obj, int depth)
        {
            if (depth > k_MaxDepth)
            {
                parentList.Add(ManagedFieldInfo.CreatePendingField(depth));
                return;
            }

            if (!ValidateManagedObject(ref obj))
                return;

            // 检查递归引用（参考Unity Line 284-285）
            if (obj.dataType != ObjectDataType.Value && CheckRecursion(obj, out var recursiveId))
            {
                parentList.Add(new ManagedFieldInfo
                {
                    DisplayName = "(Recursive Reference)",
                    Value = "",
                    TypeName = m_CachedSnapshot.TypeDescriptions.TypeDescriptionName[obj.managedTypeIndex],
                    IsRecursive = true,
                    Notes = $"See item #{recursiveId}",
                    Depth = depth
                });
                return;
            }

            // 字符串特殊处理（参考Unity Line 288-292）
            if (m_CachedSnapshot.TypeDescriptions.ITypeString == obj.managedTypeIndex && obj.dataType != ObjectDataType.Type)
            {
                // 字符串值已经在父字段中显示，不展开字段
                return;
            }

            // 数组特殊处理（参考Unity ManagedObjectInspector.ProcessManagedArrayElements Line 583-620）
            if (obj.dataType == ObjectDataType.Array || obj.dataType == ObjectDataType.ReferenceArray)
            {
                ProcessArrayElements(parentList, obj, depth);
                return;
            }

            // 构建字段列表（参考Unity Line 302）
            var fieldList = BuildFieldList(obj);

            // 处理每个字段
            for (var i = 0; i < fieldList.Length; i++)
            {
                var fieldByIndex = obj.GetFieldByFieldDescriptionsIndex(m_CachedSnapshot, fieldList[i], false);
                ProcessField(parentList, fieldByIndex, fieldList[i], depth);
            }
        }

        /// <summary>
        /// 处理数组元素
        /// 参考: Unity ManagedObjectInspector.ProcessManagedArrayElements (Line 583-620)
        /// </summary>
        private void ProcessArrayElements(List<ManagedFieldInfo> parentList, ObjectData arrayObj, int depth)
        {
            try
            {
                var arrayInfo = arrayObj.GetArrayInfo(m_CachedSnapshot);
                if (arrayInfo == null)
                {
                    return;
                }

                var elementCount = arrayInfo.Length;

                // 限制显示的元素数量，避免UI卡顿（最多显示1000个元素）
                const int maxElementsToShow = 1000;
                var elementsToShow = Math.Min(elementCount, maxElementsToShow);

                for (var i = 0; i < elementsToShow; i++)
                {
                    var elementData = arrayObj.GetArrayElement(m_CachedSnapshot, arrayInfo, i, true);
                    ProcessArrayElement(parentList, elementData, arrayInfo, i, depth);
                }

                // 如果数组太大，添加一个提示
                if (elementCount > maxElementsToShow)
                {
                    parentList.Add(new ManagedFieldInfo
                    {
                        DisplayName = $"... ({elementCount - maxElementsToShow} more elements)",
                        Value = "",
                        TypeName = "",
                        Notes = $"Array has {elementCount} elements total, showing first {maxElementsToShow}",
                        Depth = depth
                    });
                }
            }
            catch (Exception ex)
            {
                parentList.Add(new ManagedFieldInfo
                {
                    DisplayName = "(Error reading array)",
                    Value = ex.Message,
                    TypeName = "",
                    Depth = depth
                });
            }
        }

        /// <summary>
        /// 处理单个数组元素
        /// 参考: Unity ManagedObjectInspector.ProcessField (Line 313-366)
        /// </summary>
        private void ProcessArrayElement(List<ManagedFieldInfo> parentList, ObjectData elementData, ArrayInfo arrayInfo, long elementIndex, int depth)
        {
            var typeIdx = arrayInfo.ElementTypeDescription;
            var actualTypeIdx = typeIdx;

            // 获取元素值
            string value = GetValue(elementData);

            // 处理引用对象
            if (elementData.dataType == ObjectDataType.ReferenceArray || elementData.dataType == ObjectDataType.ReferenceObject)
            {
                var referencedObject = elementData;
                if (ValidateManagedObject(ref referencedObject))
                {
                    actualTypeIdx = referencedObject.managedTypeIndex;

                    if (referencedObject.dataType == ObjectDataType.BoxedValue)
                    {
                        referencedObject = referencedObject.GetBoxedValue(m_CachedSnapshot, true);
                    }

                    if (actualTypeIdx != typeIdx)
                    {
                        var actualTypeName = m_CachedSnapshot.TypeDescriptions.TypeDescriptionName[actualTypeIdx];
                        value = FormatFieldValueWithContentTypeNotMatchingFieldType(value, actualTypeName);
                    }
                }
            }

            // 创建字段信息
            var fieldInfo = new ManagedFieldInfo
            {
                DisplayName = $"[{elementIndex}]",
                Value = value,
                TypeName = m_CachedSnapshot.TypeDescriptions.TypeDescriptionName[actualTypeIdx],
                Depth = depth,
                IsStatic = false
            };

            // 检查是否需要展开子元素
            if (ShouldExpandField(elementData, actualTypeIdx))
            {
                fieldInfo.Children = new List<ManagedFieldInfo>();
                ProcessManagedObjectFields(fieldInfo.Children, elementData, depth + 1);
            }

            parentList.Add(fieldInfo);
        }

        /// <summary>
        /// 处理单个字段
        /// 参考: Unity ManagedObjectInspector.ProcessField (Line 313-366)
        /// </summary>
        private void ProcessField(List<ManagedFieldInfo> parentList, ObjectData fieldData, int fieldDescriptionIndex, int depth)
        {
            var typeIdx = m_CachedSnapshot.FieldDescriptions.TypeIndex[fieldDescriptionIndex];
            var actualFieldTypeIdx = typeIdx;
            var isStatic = m_CachedSnapshot.FieldDescriptions.IsStatic[fieldDescriptionIndex] == 1;
            var fieldName = m_CachedSnapshot.FieldDescriptions.FieldDescriptionName[fieldDescriptionIndex];

            // 获取字段值
            string value = GetValue(fieldData);

            // 处理引用对象（参考Unity Line 319-338）
            if (fieldData.dataType == ObjectDataType.ReferenceArray || fieldData.dataType == ObjectDataType.ReferenceObject)
            {
                var referencedObject = fieldData;
                if (ValidateManagedObject(ref referencedObject))
                {
                    actualFieldTypeIdx = referencedObject.managedTypeIndex;

                    if (referencedObject.dataType == ObjectDataType.BoxedValue)
                    {
                        referencedObject = referencedObject.GetBoxedValue(m_CachedSnapshot, true);
                    }

                    if (actualFieldTypeIdx != typeIdx)
                    {
                        var actualTypeName = m_CachedSnapshot.TypeDescriptions.TypeDescriptionName[actualFieldTypeIdx];
                        value = FormatFieldValueWithContentTypeNotMatchingFieldType(value, actualTypeName);
                    }
                }
            }

            var typeName = m_CachedSnapshot.TypeDescriptions.TypeDescriptionName[typeIdx];
            var fieldSize = GetFieldSize(fieldData);

            // 创建字段信息
            var fieldInfo = new ManagedFieldInfo
            {
                DisplayName = fieldName,
                Value = value,
                TypeName = typeName,
                IsStatic = isStatic,
                Size = FormatBytes(fieldSize),
                ManagedTypeIndex = typeIdx,
                IdentifyingPointer = GetIdentifyingPointer(fieldData),
                Depth = depth,
                Notes = isStatic ? "Static" : ""
            };

            // 处理需要展开的字段（参考Unity Line 621-674）
            if (ShouldExpandField(fieldData, actualFieldTypeIdx))
            {
                fieldInfo.Children = new List<ManagedFieldInfo>();
                ExpandField(fieldInfo.Children, fieldData, actualFieldTypeIdx, depth + 1);
            }

            parentList.Add(fieldInfo);
        }

        /// <summary>
        /// 判断字段是否需要展开
        /// 参考: Unity ManagedObjectInspector.ProcessSpecialFieldsAndQueueChildElements (Line 621-674)
        /// </summary>
        private bool ShouldExpandField(ObjectData field, int actualFieldTypeIdx)
        {
            // Value type with fields
            if (field.dataType == ObjectDataType.Value &&
                m_CachedSnapshot.TypeDescriptions.FieldIndicesInstance[actualFieldTypeIdx].Length >= 1)
                return true;

            // Array
            if (field.dataType == ObjectDataType.Array || field.dataType == ObjectDataType.ReferenceArray)
                return true;

            // Object
            if (field.dataType == ObjectDataType.Object || field.dataType == ObjectDataType.ReferenceObject)
            {
                // 不是null，不是string
                if (GetValue(field) != "null" && actualFieldTypeIdx != m_CachedSnapshot.TypeDescriptions.ITypeString)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 展开字段（添加子字段）
        /// </summary>
        private void ExpandField(List<ManagedFieldInfo> childList, ObjectData field, int actualFieldTypeIdx, int depth)
        {
            if (depth > k_MaxDepth)
            {
                childList.Add(ManagedFieldInfo.CreatePendingField(depth));
                return;
            }

            // 处理数组
            if (field.dataType == ObjectDataType.Array || field.dataType == ObjectDataType.ReferenceArray)
            {
                ProcessArray(childList, field, depth);
                return;
            }

            // 处理对象和Value type
            if (field.dataType == ObjectDataType.Value ||
                field.dataType == ObjectDataType.Object ||
                field.dataType == ObjectDataType.ReferenceObject)
            {
                // 获取引用的对象
                var objToProcess = field;
                if (field.dataType == ObjectDataType.ReferenceObject)
                {
                    objToProcess = field.GetReferencedObject(m_CachedSnapshot);
                    if (!objToProcess.IsValid)
                        return;
                }

                ProcessManagedObjectFields(childList, objToProcess, depth);
            }
        }

        /// <summary>
        /// 处理数组元素
        /// 参考: Unity ManagedObjectInspector.ProcessManagedArrayElements (Line 583-619)
        /// </summary>
        private void ProcessArray(List<ManagedFieldInfo> childList, ObjectData arrayData, int depth)
        {
            if (!ValidateManagedObject(ref arrayData))
                return;

            var arrayInfo = arrayData.GetArrayInfo(m_CachedSnapshot);
            if (arrayInfo == null)
                return;

            var elementCount = Math.Min(arrayInfo.Length, k_MaxArrayElements);

            for (var i = 0; i < elementCount; i++)
            {
                var arrayElement = arrayData.GetArrayElement(m_CachedSnapshot, arrayInfo, i, true);
                ProcessArrayElement(childList, arrayElement, arrayInfo, i, depth);
            }

            // 如果数组太大，添加"继续加载"占位符
            if (arrayInfo.Length > k_MaxArrayElements)
            {
                childList.Add(new ManagedFieldInfo
                {
                    DisplayName = $"... and {arrayInfo.Length - k_MaxArrayElements} more elements",
                    Value = "(Click to load more)",
                    TypeName = "",
                    IsPending = true,
                    Depth = depth
                });
            }
        }

        /// <summary>
        /// 处理数组元素
        /// </summary>
        private void ProcessArrayElement(List<ManagedFieldInfo> parentList, ObjectData arrayElement, ArrayInfo arrayInfo, int index, int depth)
        {
            var typeIdx = arrayInfo.ElementTypeDescription;
            var typeName = m_CachedSnapshot.TypeDescriptions.TypeDescriptionName[typeIdx];
            var value = GetValue(arrayElement);
            var size = GetFieldSize(arrayElement);

            var elementInfo = new ManagedFieldInfo
            {
                DisplayName = $"[{index}]",
                Value = value,
                TypeName = typeName,
                Size = FormatBytes(size),
                ManagedTypeIndex = typeIdx,
                IdentifyingPointer = GetIdentifyingPointer(arrayElement),
                Depth = depth
            };

            // 检查是否需要展开
            var actualTypeIdx = typeIdx;
            if (arrayElement.dataType == ObjectDataType.ReferenceObject || arrayElement.dataType == ObjectDataType.ReferenceArray)
            {
                var refObj = arrayElement.GetReferencedObject(m_CachedSnapshot);
                if (refObj.IsValid)
                    actualTypeIdx = refObj.managedTypeIndex;
            }

            if (ShouldExpandField(arrayElement, actualTypeIdx))
            {
                elementInfo.Children = new List<ManagedFieldInfo>();
                ExpandField(elementInfo.Children, arrayElement, actualTypeIdx, depth + 1);
            }

            parentList.Add(elementInfo);
        }

        #region Helper Methods

        /// <summary>
        /// 格式化字节大小
        /// </summary>
        private string FormatBytes(ulong bytes)
        {
            if (bytes == 0)
                return "0 B";

            const double k = 1024;
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int i = 0;
            double size = bytes;

            while (size >= k && i < sizes.Length - 1)
            {
                size /= k;
                i++;
            }

            return $"{size:F2} {sizes[i]}";
        }

        /// <summary>
        /// 获取字段值的字符串表示
        /// 参考: Unity ManagedObjectInspector.GetValue (Line 786-844)
        /// </summary>
        private string GetValue(ObjectData od)
        {
            if (!od.IsValid || !od.HasValidFieldOrArrayElementData(m_CachedSnapshot))
                return "failed to read data";

            // 检查未初始化的静态字段
            if (od.managedObjectData.Bytes.Count == 0
                && od.IsField() && m_CachedSnapshot.FieldDescriptions.IsStatic[od.fieldIndex] == 1
                && m_CachedSnapshot.TypeDescriptions.HasStaticFieldData(od.managedTypeIndex))
            {
                return "uninitialized static field data";
            }

            switch (od.dataType)
            {
                case ObjectDataType.BoxedValue:
                    return m_Formatter.FormatValueType(od.GetBoxedValue(m_CachedSnapshot, true), false, false);
                case ObjectDataType.Value:
                    return m_Formatter.FormatValueType(od, false, false);
                case ObjectDataType.Object:
                    return m_Formatter.FormatObject(od, false, false);
                case ObjectDataType.Array:
                    return m_Formatter.FormatArray(od, false);
                case ObjectDataType.ReferenceObject:
                    {
                        ulong ptr = od.GetReferencePointer();
                        if (ptr == 0)
                            return "null";
                        
                        var o = ObjectData.FromManagedPointer(m_CachedSnapshot, ptr, od.managedTypeIndex);
                        if (!o.IsValid)
                            return "failed to read object";
                        if (o.dataType == ObjectDataType.BoxedValue)
                            return m_Formatter.FormatValueType(o.GetBoxedValue(m_CachedSnapshot, true), false, false);
                        return m_Formatter.FormatObject(o, false, false);
                    }
                case ObjectDataType.ReferenceArray:
                    {
                        ulong ptr = od.GetReferencePointer();
                        if (ptr == 0)
                            return "null";
                        var arr = ObjectData.FromManagedPointer(m_CachedSnapshot, ptr, od.managedTypeIndex);
                        if (!arr.IsValid)
                            return "failed to read pointer";
                        return m_Formatter.FormatArray(arr, false);
                    }
                case ObjectDataType.Type:
                    return m_CachedSnapshot.TypeDescriptions.TypeDescriptionName[od.managedTypeIndex];
                case ObjectDataType.NativeObject:
                    return DetailFormatter.FormatPointer(m_CachedSnapshot.NativeObjects.NativeObjectAddress[od.nativeObjectIndex]);
                default:
                    return "<uninitialized type>";
            }
        }

        /// <summary>
        /// 获取字段大小
        /// 参考: Unity ManagedObjectInspector.GetFieldSize (Line 519-538)
        /// </summary>
        private ulong GetFieldSize(ObjectData fieldByIndex)
        {
            switch (fieldByIndex.dataType)
            {
                case ObjectDataType.Object:
                case ObjectDataType.BoxedValue:
                case ObjectDataType.Array:
                    return (ulong)fieldByIndex.GetManagedObject(m_CachedSnapshot).Size;
                case ObjectDataType.ReferenceObject:
                case ObjectDataType.ReferenceArray:
                    return (ulong)m_CachedSnapshot.VirtualMachineInformation.PointerSize;
                case ObjectDataType.Type:
                case ObjectDataType.Value:
                    return (ulong)m_CachedSnapshot.TypeDescriptions.Size[fieldByIndex.managedTypeIndex];
                case ObjectDataType.NativeObject:
                    return m_CachedSnapshot.NativeObjects.Size[fieldByIndex.nativeObjectIndex];
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 构建字段列表
        /// 参考: Unity ManagedObjectInspector.BuildFieldList (Line 846-863)
        /// </summary>
        private int[] BuildFieldList(ObjectData obj)
        {
            List<int> fields = new List<int>();
            switch (obj.dataType)
            {
                case ObjectDataType.Type:
                    // 只取静态字段
                    return m_CachedSnapshot.TypeDescriptions.fieldIndicesStatic[obj.managedTypeIndex];
                case ObjectDataType.BoxedValue:
                case ObjectDataType.Object:
                case ObjectDataType.Value:
                case ObjectDataType.ReferenceObject:
                    fields.AddRange(m_CachedSnapshot.TypeDescriptions.fieldIndicesStatic[obj.managedTypeIndex]);
                    fields.AddRange(m_CachedSnapshot.TypeDescriptions.FieldIndicesInstance[obj.managedTypeIndex]);
                    break;
            }
            return fields.ToArray();
        }

        /// <summary>
        /// 验证Managed对象
        /// 参考: Unity ManagedObjectInspector.ValidateManagedObject (Line 552-564)
        /// </summary>
        private bool ValidateManagedObject(ref ObjectData obj)
        {
            if (!obj.IsValid)
                return false;
            if (obj.dataType == ObjectDataType.ReferenceObject || obj.dataType == ObjectDataType.ReferenceArray)
            {
                var validObj = obj.GetReferencedObject(m_CachedSnapshot);
                if (!validObj.IsValid)
                    return false;
                obj = validObj;
            }
            return true;
        }

        /// <summary>
        /// 检查递归引用
        /// 参考: Unity ManagedObjectInspector.CheckRecursion (Line 567-581)
        /// </summary>
        private bool CheckRecursion(ObjectData obj, out int recursiveId)
        {
            recursiveId = -1;
            var identifyingPointer = GetIdentifyingPointer(obj);
            if (identifyingPointer != 0)
            {
                if (m_IdentifyingPointerToId.TryGetValue(identifyingPointer, out recursiveId))
                {
                    return true;
                }
                else
                {
                    recursiveId = m_NextId++;
                    m_IdentifyingPointerToId.Add(identifyingPointer, recursiveId);
                }
            }
            return false;
        }

        /// <summary>
        /// 获取标识指针
        /// 参考: Unity ManagedObjectInspector.GetIdentifyingPointer (Line 541-549)
        /// </summary>
        private ulong GetIdentifyingPointer(ObjectData obj)
        {
            var address = obj.GetObjectPointer(m_CachedSnapshot);
            if (obj.dataType == ObjectDataType.Type)
            {
                address += m_CachedSnapshot.TypeDescriptions.TypeInfoAddress[obj.managedTypeIndex];
            }
            return address;
        }

        /// <summary>
        /// 格式化字段值（当实际类型与字段类型不匹配时）
        /// 参考: Unity ManagedObjectInspector.FormatFieldValueWithContentTypeNotMatchingFieldType (Line 776-784)
        /// </summary>
        private string FormatFieldValueWithContentTypeNotMatchingFieldType(string value, string actualTypeName)
        {
            if (value == null)
                return $"({actualTypeName})";
            else
                return $"{value} ({actualTypeName})";
        }

        #endregion
    }
}

