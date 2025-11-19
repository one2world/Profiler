using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Format;
using UnityEngine;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// PathsToRoot引用树的树节点
    /// 参考: Unity.MemoryProfiler.Editor.UI.PathsToRoot.PathsToRootDetailTreeViewItem
    /// </summary>
    internal class PathsToRootTreeNode : ITreeNode
    {
        private static int s_IdGenerator = 0;

        public int Id { get; }
        public int CircularRefId { get; private set; } = -1;
        public ObjectData Data { get; }
        public string TypeName { get; }
        public string TruncatedTypeName { get; }
        public string DisplayName { get; }
        public string FullTooltip { get; }
        public string FlagsInfo { get; private set; } = "";
        public string FlagsTooltip { get; private set; } = "";
        public bool HasCircularReference { get; private set; }
        public bool IsGCRoot { get; set; }  // 标记是否为 GC Root
        public string Address { get; private set; } = "";

        // 父节点引用（用于循环引用检测）
        public PathsToRootTreeNode? Parent { get; private set; }

        public ObservableCollection<PathsToRootTreeNode> Children { get; } = new ObservableCollection<PathsToRootTreeNode>();

        // 用于ITreeNode接口
        // 参考Unity的ManagedObjectInspectorItem：如果是循环引用，不展开子节点
        IEnumerable<object>? ITreeNode.GetChildren() => HasCircularReference ? null : Children;

        // UI绑定属性
        public ImageSource? TypeIconSource { get; private set; }
        public string TypeIconTooltip { get; private set; } = "";
        public ImageSource? FlagsIconSource { get; private set; }
        public Visibility CircularRefIconVisibility => HasCircularReference ? Visibility.Visible : Visibility.Collapsed;
        public Visibility FlagsIconVisibility => !string.IsNullOrEmpty(FlagsInfo) ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>
        /// 默认构造函数（用于根节点）
        /// </summary>
        public PathsToRootTreeNode()
        {
            Id = s_IdGenerator++;
            Data = default;
            TypeName = TruncatedTypeName = "";
            DisplayName = "Root";
            FullTooltip = "";
        }

        /// <summary>
        /// 从ObjectData创建树节点
        /// </summary>
        public PathsToRootTreeNode(ObjectData data, CachedSnapshot snapshot, bool truncateTypeNames, bool isReferencesToItem = false)
        {
            Id = s_IdGenerator++;
            Data = data;
            HasCircularReference = false;

            if (snapshot != null)
            {
                TypeName = data.GenerateTypeName(snapshot, truncateTypeName: false);
                TruncatedTypeName = data.GenerateTypeName(snapshot, truncateTypeName: true);
                DisplayName = GetDisplayName(data, snapshot, truncateTypeNames, isReferencesToItem);
                FullTooltip = GenerateTooltip(data, snapshot, truncateTypeNames);
                Address = GetAddressString(data);

                SetObjectFlagsData(data, snapshot);
                SetTypeIcon(data, TypeName, snapshot);
            }
            else
            {
                TypeName = TruncatedTypeName = "";
                DisplayName = "";
                FullTooltip = "";
                Address = "";
            }
        }

        /// <summary>
        /// 从ObjectData创建树节点，并检测循环引用
        /// </summary>
        public PathsToRootTreeNode(ObjectData data, CachedSnapshot snapshot, PathsToRootTreeNode potentialParent, bool truncateTypeNames, bool isReferencesToItem = false)
            : this(data, snapshot, truncateTypeNames, isReferencesToItem)
        {
            HasCircularReference = CircularReferenceCheck(potentialParent);
        }

        /// <summary>
        /// 添加子节点（并设置Parent引用）
        /// </summary>
        public void AddChild(PathsToRootTreeNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        /// <summary>
        /// 获取对象地址字符串
        /// </summary>
        private static string GetAddressString(ObjectData data)
        {
            // 根据对象类型获取地址
            switch (data.dataType)
            {
                case ObjectDataType.Object:
                case ObjectDataType.BoxedValue:
                    return data.hostManagedObjectPtr != 0 ? $"0x{data.hostManagedObjectPtr:X}" : "";
                
                case ObjectDataType.NativeObject:
                    return data.nativeObjectIndex >= 0 ? $"Native[{data.nativeObjectIndex}]" : "";
                
                case ObjectDataType.Type:
                    return data.managedTypeIndex >= 0 ? $"Type[{data.managedTypeIndex}]" : "";
                
                default:
                    return "";
            }
        }

        /// <summary>
        /// 检测循环引用
        /// Bug修复：遍历完整的父节点链，而不是只检查直接父节点
        /// </summary>
        public bool CircularReferenceCheck(PathsToRootTreeNode? potentialParent)
        {
            var current = potentialParent;

            while (current != null)
            {
                if (current.Data.Equals(Data))
                {
                    CircularRefId = current.Id;
                    return true;
                }

                // 遍历整个父节点链
                current = current.Parent;
            }

            CircularRefId = -1;
            return false;
        }

        /// <summary>
        /// 生成显示名称
        /// 参考: PathsToRootDetailTreeViewItem.GetDisplayName
        /// </summary>
        private string GetDisplayName(ObjectData data, CachedSnapshot snapshot, bool truncateTypeNames, bool isReferencesToItem)
        {
            var referencedItemName = "";
            ObjectData displayObject = data.displayObject;

            // 对于ReferencesTo项，调整显示
            if (isReferencesToItem && (data.IsField() || data.IsArrayItem()))
            {
                displayObject = data.Parent.Obj;
                referencedItemName = $"{data.GenerateObjectName(snapshot)} referenced by: ";
            }

            switch (displayObject.dataType)
            {
                case ObjectDataType.NativeObject:
                    var name = snapshot.NativeObjects.ObjectName[displayObject.nativeObjectIndex];
                    return referencedItemName + (string.IsNullOrEmpty(name) ? "Unnamed Object" : name);

                case ObjectDataType.Unknown:
                    return referencedItemName + "<unknown>";

                case ObjectDataType.Value:
                    if (!displayObject.IsField())
                    {
                        return referencedItemName + "Connection to Value";
                    }
                    var fieldType = snapshot.FieldDescriptions.TypeIndex[displayObject.fieldIndex];
                    var isPointerField = fieldType == snapshot.TypeDescriptions.ITypeIntPtr ||
                                         snapshot.TypeDescriptions.TypeDescriptionName[fieldType].EndsWith('*');
                    var referencingNativeData = data.codeType == CodeType.Native;
                    return referencedItemName + displayObject.GetFieldDescription(snapshot, truncateTypeNames)
                        + (!referencingNativeData && !isPointerField ? " (Boehm reads pointer sized field as potential pointers)" : "");

                case ObjectDataType.BoxedValue:
                case ObjectDataType.Object:
                    if (displayObject.isManaged)
                    {
                        if (displayObject.IsField())
                        {
                            return referencedItemName + displayObject.GetFieldDescription(snapshot, truncateTypeNames);
                        }
                        var managedObjectInfo = displayObject.GetManagedObject(snapshot);
                        if (managedObjectInfo.NativeObjectIndex != -1)
                        {
                            return referencedItemName + snapshot.NativeObjects.ObjectName[managedObjectInfo.NativeObjectIndex];
                        }
                        if (managedObjectInfo.ITypeDescription == snapshot.TypeDescriptions.ITypeString)
                        {
                            return StringTools.ReadFirstStringLine(managedObjectInfo.data, snapshot.VirtualMachineInformation, false);
                        }
                    }
                    return $"{referencedItemName}[0x{displayObject.hostManagedObjectPtr:x8}]";

                case ObjectDataType.Array:
                    var arrayData = data;
                    while (!arrayData.IsArrayItem() && arrayData.dataType != ObjectDataType.Array && arrayData.dataType != ObjectDataType.ReferenceArray)
                    {
                        arrayData = arrayData.Parent.Obj;
                    }
                    return referencedItemName + arrayData.GenerateArrayDescription(snapshot, truncateTypeName: truncateTypeNames);

                case ObjectDataType.ReferenceObject:
                case ObjectDataType.ReferenceArray:
                    if (displayObject.IsField()) return referencedItemName + displayObject.GetFieldDescription(snapshot, truncateTypeNames);
                    if (displayObject.IsArrayItem()) return referencedItemName + displayObject.GenerateArrayDescription(snapshot, truncateTypeName: truncateTypeNames);
                    var refManagedObjectInfo = displayObject.GetManagedObject(snapshot);
                    if (refManagedObjectInfo.NativeObjectIndex != -1)
                    {
                        return referencedItemName + snapshot.NativeObjects.ObjectName[refManagedObjectInfo.NativeObjectIndex];
                    }
                    return $"{referencedItemName}Unknown {displayObject.dataType}. Is not a field or array item";

                case ObjectDataType.Type:
                    var fieldName = string.Empty;
                    if (data.IsField())
                        fieldName = $".{data.GetFieldName(snapshot)}";
                    var typeName = truncateTypeNames ? TruncatedTypeName : TypeName;
                    return $"{referencedItemName}Static field type reference on {typeName}{fieldName}";

                case ObjectDataType.NativeAllocation:
                default:
                    return referencedItemName + displayObject.dataType.ToString();
            }
        }

        /// <summary>
        /// 生成工具提示
        /// </summary>
        private string GenerateTooltip(ObjectData data, CachedSnapshot snapshot, bool truncateTypeNames)
        {
            var tooltip = DisplayName;
            if (!string.IsNullOrEmpty(TypeName))
            {
                tooltip += $"\nType: {(truncateTypeNames ? TruncatedTypeName : TypeName)}";
            }
            if (!string.IsNullOrEmpty(FlagsTooltip))
            {
                tooltip += $"\n{FlagsTooltip}";
            }
            return tooltip;
        }

        /// <summary>
        /// 设置对象Flags数据
        /// 参考: PathsToRootDetailTreeViewItem.SetObjectFlagsDataAndToolTip
        /// </summary>
        private void SetObjectFlagsData(ObjectData data, CachedSnapshot snapshot)
        {
            FlagsInfo = "";
            FlagsTooltip = "";

            if (data.nativeObjectIndex != -1)
            {
                var flagsNames = "";
                var flagsExplanations = "";
                var hideFlagsNames = "";
                var hideFlagsExplanations = "";

                GetObjectFlagsStrings(data, snapshot, ref flagsNames, ref flagsExplanations, ref hideFlagsNames, ref hideFlagsExplanations);

                FlagsInfo = flagsNames + hideFlagsNames;
                FlagsTooltip = flagsExplanations + hideFlagsExplanations;

                if (!string.IsNullOrEmpty(FlagsInfo))
                {
                    // 创建Flags图标 (简化为使用系统图标或固定颜色标记)
                    // Unity使用特定的Flag图标，这里我们简化处理
                }
            }
        }

        /// <summary>
        /// 获取对象Flags字符串
        /// 参考: PathsToRootDetailTreeViewItem.GetObjectFlagsStrings
        /// </summary>
        internal static void GetObjectFlagsStrings(ObjectData data, CachedSnapshot snapshot,
            ref string flagsNames, ref string flagsExplanations,
            ref string hideFlagsNames, ref string hideFlagsExplanations, bool lineBreak = true)
        {
            if (data.nativeObjectIndex != -1)
            {
                var flags = data.GetFlags(snapshot);
                if (flags != 0x0)
                {
                    if ((flags & Unity.MemoryProfiler.Editor.Format.ObjectFlags.IsDontDestroyOnLoad) != 0)
                    {
                        flagsNames += " 'IsDontDestroyOnLoad'" + (lineBreak ? "\n" : ",");
                        flagsExplanations += "This object is marked as DontDestroyOnLoad and will persist across scene loads.\n";
                    }
                    if ((flags & Unity.MemoryProfiler.Editor.Format.ObjectFlags.IsPersistent) != 0)
                    {
                        flagsNames += " 'IsPersistent'" + (lineBreak ? "\n" : ",");
                        flagsExplanations += "This object is saved to disk (Asset).\n";
                    }
                    if ((flags & Unity.MemoryProfiler.Editor.Format.ObjectFlags.IsManager) != 0)
                    {
                        flagsNames += " 'IsManager'";
                        flagsExplanations += "This object is a Unity Manager.\n";
                    }
                }

                var hideFlags = snapshot.NativeObjects.HideFlags[data.nativeObjectIndex];
                if (hideFlags != 0x0)
                {
                    if ((hideFlags & HideFlags.DontSave) != 0)
                    {
                        hideFlagsNames += " 'HideFlags.DontSave'" + (lineBreak ? "\n" : ",");
                        hideFlagsExplanations += "This object will not be saved.\n";
                    }
                    if ((hideFlags & HideFlags.NotEditable) != 0)
                    {
                        hideFlagsNames += " 'HideFlags.NotEditable'" + (lineBreak ? "\n" : ",");
                        hideFlagsExplanations += "This object is not editable.\n";
                    }
                    if ((hideFlags & HideFlags.HideInHierarchy) != 0)
                    {
                        hideFlagsNames += " 'HideFlags.HideInHierarchy'" + (lineBreak ? "\n" : ",");
                        hideFlagsExplanations += "This object is hidden in the Hierarchy.\n";
                    }
                    if ((hideFlags & HideFlags.HideInInspector) != 0)
                    {
                        hideFlagsNames += " 'HideFlags.HideInInspector'" + (lineBreak ? "\n" : ",");
                        hideFlagsExplanations += "This object is hidden in the Inspector.\n";
                    }
                    if ((hideFlags & HideFlags.DontSaveInEditor) != 0)
                    {
                        hideFlagsNames += " 'HideFlags.DontSaveInEditor'" + (lineBreak ? "\n" : ",");
                        hideFlagsExplanations += "This object will not be saved in the Editor.\n";
                    }
                    if ((hideFlags & HideFlags.DontUnloadUnusedAsset) != 0)
                    {
                        hideFlagsNames += " 'HideFlags.DontUnloadUnusedAsset'" + (lineBreak ? "\n" : ",");
                        hideFlagsExplanations += "This asset will not be unloaded when unused.\n";
                    }
                    if ((hideFlags & HideFlags.HideAndDontSave) != 0)
                    {
                        hideFlagsNames += " 'HideFlags.HideAndDontSave'";
                        hideFlagsExplanations += "This object is hidden and will not be saved.\n";
                    }
                }

                // 移除尾部逗号
                if (!string.IsNullOrEmpty(flagsNames) && flagsNames.LastIndexOf(',') == flagsNames.Length - 1)
                    flagsNames = flagsNames.Substring(0, flagsNames.Length - 1);
                if (!string.IsNullOrEmpty(hideFlagsNames) && hideFlagsNames.LastIndexOf(',') == hideFlagsNames.Length - 1)
                    hideFlagsNames = hideFlagsNames.Substring(0, hideFlagsNames.Length - 1);
            }
        }

        /// <summary>
        /// 设置类型图标
        /// 参考: PathsToRootDetailTreeViewItem.GetIcon
        /// </summary>
        private void SetTypeIcon(ObjectData data, string typeName, CachedSnapshot snapshot)
        {
            // 这里简化处理，使用固定的C#/C++图标
            // Unity官方实现会加载对应类型的图标（如Transform Icon, Camera Icon等）
            if (data.isManaged)
            {
                // C# (Managed) - 紫色圆点
                TypeIconSource = CreateColorIcon(Color.FromRgb(0x6A, 0x00, 0xFF)); // Purple
                TypeIconTooltip = "C# Object (Managed)";
            }
            else if (data.isNativeObject)
            {
                // C++ (Native) - 蓝色圆点
                TypeIconSource = CreateColorIcon(Color.FromRgb(0x00, 0x7A, 0xCC)); // Blue
                TypeIconTooltip = "C++ Object (Native)";
            }
            else
            {
                // Unknown - 灰色圆点
                TypeIconSource = CreateColorIcon(Color.FromRgb(0x80, 0x80, 0x80)); // Gray
                TypeIconTooltip = "Unknown Type";
            }
        }

        /// <summary>
        /// 创建颜色图标（简化版）
        /// </summary>
        private ImageSource CreateColorIcon(Color color)
        {
            var drawingGroup = new DrawingGroup();
            drawingGroup.Children.Add(new GeometryDrawing
            {
                Brush = new SolidColorBrush(color),
                Geometry = new EllipseGeometry(new Point(8, 8), 6, 6)
            });

            return new DrawingImage(drawingGroup);
        }
    }
}

