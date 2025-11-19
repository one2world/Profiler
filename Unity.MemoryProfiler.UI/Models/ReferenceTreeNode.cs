using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Format;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// References/Referenced By树节点
    /// 对应Unity: PathsToRootDetailTreeViewItem
    /// </summary>
    internal class ReferenceTreeNode : INotifyPropertyChanged
    {
        private bool _isExpanded;
        private bool _isSelected;
        private string _displayName = string.Empty;
        private string _typeName = string.Empty;
        private bool _hasCircularReference;
        private int _circularRefId = -1;

        public ReferenceTreeNode()
        {
            Children = new ObservableCollection<ReferenceTreeNode>();
        }

        public ReferenceTreeNode(ObjectData data, CachedSnapshot snapshot, bool truncateTypeNames, bool isReferencesToItem = false)
        {
            Children = new ObservableCollection<ReferenceTreeNode>();
            Data = data;
            Snapshot = snapshot;

            if (snapshot != null && data.IsValid)
            {
                TypeName = data.GenerateTypeName(snapshot, truncateTypeName: false);
                TruncatedTypeName = data.GenerateTypeName(snapshot, truncateTypeName: true);
                DisplayName = GenerateDisplayName(data, snapshot, truncateTypeNames, isReferencesToItem);
                UpdateFlags(data, snapshot);
            }
        }

        /// <summary>
        /// 对象数据
        /// </summary>
        public ObjectData Data { get; }

        /// <summary>
        /// 快照引用
        /// </summary>
        public CachedSnapshot? Snapshot { get; }

        /// <summary>
        /// 显示名称
        /// </summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>
        /// 类型名称（完整）
        /// </summary>
        public string TypeName
        {
            get => _typeName;
            set => SetProperty(ref _typeName, value);
        }

        /// <summary>
        /// 类型名称（截断）
        /// </summary>
        public string TruncatedTypeName { get; private set; } = string.Empty;

        /// <summary>
        /// 是否展开
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }

        /// <summary>
        /// 是否选中
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        /// <summary>
        /// 是否有循环引用
        /// </summary>
        public bool HasCircularReference
        {
            get => _hasCircularReference;
            set => SetProperty(ref _hasCircularReference, value);
        }

        /// <summary>
        /// 循环引用目标ID
        /// </summary>
        public int CircularRefId
        {
            get => _circularRefId;
            set => SetProperty(ref _circularRefId, value);
        }

        /// <summary>
        /// 对象标志（IsDontDestroyOnLoad等）
        /// </summary>
        public string FlagsText { get; private set; } = string.Empty;

        /// <summary>
        /// Hide标志
        /// </summary>
        public string HideFlagsText { get; private set; } = string.Empty;

        /// <summary>
        /// Flags工具提示
        /// </summary>
        public string FlagsToolTip { get; private set; } = string.Empty;

        /// <summary>
        /// 是否是C#对象
        /// </summary>
        public bool IsManaged => Data.IsValid && Data.isManaged;

        /// <summary>
        /// 是否是Native对象
        /// </summary>
        public bool IsNative => Data.IsValid && !Data.isManaged;

        /// <summary>
        /// 子节点
        /// </summary>
        public ObservableCollection<ReferenceTreeNode> Children { get; }

        /// <summary>
        /// 父节点
        /// </summary>
        public ReferenceTreeNode? Parent { get; set; }

        /// <summary>
        /// 添加子节点
        /// </summary>
        public void AddChild(ReferenceTreeNode child)
        {
            if (child != null)
            {
                child.Parent = this;
                Children.Add(child);
            }
        }

        /// <summary>
        /// 检查循环引用
        /// </summary>
        public bool CheckCircularReference(ReferenceTreeNode? potentialParent)
        {
            if (potentialParent == null)
                return false;

            var current = potentialParent.Parent;
            while (current != null)
            {
                if (current.Data.Equals(Data))
                {
                    CircularRefId = current.GetHashCode(); // 使用HashCode作为ID
                    HasCircularReference = true;
                    return true;
                }
                current = current.Parent;
            }

            CircularRefId = -1;
            HasCircularReference = false;
            return false;
        }

        /// <summary>
        /// 生成显示名称
        /// 对应Unity: PathsToRootDetailTreeViewItem.GetDisplayName
        /// </summary>
        private string GenerateDisplayName(ObjectData data, CachedSnapshot snapshot, bool truncateTypeNames, bool isReferencesToItem)
        {
            var referencedItemName = "";
            ObjectData displayObject = data.displayObject;

            // 对于ReferencesTo的Field/Array项，需要调整显示
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
                    var fieldDesc = displayObject.GetFieldDescription(snapshot, truncateTypeNames: truncateTypeNames);
                    var fieldType = snapshot.FieldDescriptions.TypeIndex[displayObject.fieldIndex];
                    var isPointerField = fieldType == snapshot.TypeDescriptions.ITypeIntPtr ||
                                       snapshot.TypeDescriptions.TypeDescriptionName[fieldType].EndsWith('*');
                    var referencingNativeData = data.codeType == CodeType.Native;
                    var hint = (!referencingNativeData && !isPointerField) ? " (Boehm GC pointer scan)" : "";
                    return referencedItemName + fieldDesc + hint;

                case ObjectDataType.BoxedValue:
                case ObjectDataType.Object:
                    if (displayObject.isManaged)
                    {
                        if (displayObject.IsField())
                        {
                            return referencedItemName + displayObject.GetFieldDescription(snapshot, truncateTypeNames: truncateTypeNames);
                        }
                        var managedInfo = displayObject.GetManagedObject(snapshot);
                        if (managedInfo.NativeObjectIndex != -1)
                        {
                            return referencedItemName + snapshot.NativeObjects.ObjectName[managedInfo.NativeObjectIndex];
                        }
                        if (managedInfo.ITypeDescription == snapshot.TypeDescriptions.ITypeString)
                        {
                            return referencedItemName + StringTools.ReadFirstStringLine(managedInfo.data, snapshot.VirtualMachineInformation, false);
                        }
                    }
                    return $"{referencedItemName}[0x{displayObject.hostManagedObjectPtr:x8}]";

                case ObjectDataType.Array:
                case ObjectDataType.ReferenceArray:
                    var arrayData = data;
                    while (!arrayData.IsArrayItem() && arrayData.dataType != ObjectDataType.Array && arrayData.dataType != ObjectDataType.ReferenceArray)
                    {
                        arrayData = arrayData.Parent.Obj;
                    }
                    return referencedItemName + arrayData.GenerateArrayDescription(snapshot, truncateTypeName: truncateTypeNames);

                case ObjectDataType.ReferenceObject:
                    if (displayObject.IsField()) 
                        return referencedItemName + displayObject.GetFieldDescription(snapshot, truncateTypeNames: truncateTypeNames);
                    if (displayObject.IsArrayItem()) 
                        return referencedItemName + displayObject.GenerateArrayDescription(snapshot, truncateTypeName: truncateTypeNames);
                    var refManagedInfo = displayObject.GetManagedObject(snapshot);
                    if (refManagedInfo.NativeObjectIndex != -1)
                    {
                        return referencedItemName + snapshot.NativeObjects.ObjectName[refManagedInfo.NativeObjectIndex];
                    }
                    return $"{referencedItemName}Unknown {displayObject.dataType}";

                case ObjectDataType.Type:
                    var fieldName = data.IsField() ? $".{data.GetFieldName(snapshot)}" : "";
                    var typeNameStr = truncateTypeNames ? TruncatedTypeName : TypeName;
                    return $"{referencedItemName}Static field type reference on {typeNameStr}{fieldName}";

                case ObjectDataType.NativeAllocation:
                default:
                    return referencedItemName + data.GenerateTypeName(snapshot, truncateTypeName: truncateTypeNames);
            }
        }

        /// <summary>
        /// 更新Flags信息
        /// </summary>
        private void UpdateFlags(ObjectData data, CachedSnapshot snapshot)
        {
            if (data.nativeObjectIndex == -1)
                return;

            var flagsNames = string.Empty;
            var flagsExplanations = string.Empty;
            var hideFlagsNames = string.Empty;
            var hideFlagsExplanations = string.Empty;

            var flags = data.GetFlags(snapshot);
            if ((flags & ObjectFlags.IsDontDestroyOnLoad) != 0)
            {
                flagsNames += "IsDontDestroyOnLoad\n";
                flagsExplanations += "Object is marked as DontDestroyOnLoad.\n";
            }
            if ((flags & ObjectFlags.IsPersistent) != 0)
            {
                flagsNames += "IsPersistent\n";
                flagsExplanations += "Object is persistent (saved to disk).\n";
            }
            if ((flags & ObjectFlags.IsManager) != 0)
            {
                flagsNames += "IsManager\n";
                flagsExplanations += "Object is a Unity Manager.\n";
            }

            var hideFlags = snapshot.NativeObjects.HideFlags[data.nativeObjectIndex];
            if ((hideFlags & UnityEngine.HideFlags.DontSave) != 0)
            {
                hideFlagsNames += "DontSave\n";
                hideFlagsExplanations += "Object will not be saved.\n";
            }
            if ((hideFlags & UnityEngine.HideFlags.NotEditable) != 0)
            {
                hideFlagsNames += "NotEditable\n";
                hideFlagsExplanations += "Object is not editable.\n";
            }
            if ((hideFlags & UnityEngine.HideFlags.HideInHierarchy) != 0)
            {
                hideFlagsNames += "HideInHierarchy\n";
                hideFlagsExplanations += "Object is hidden in hierarchy.\n";
            }
            if ((hideFlags & UnityEngine.HideFlags.HideInInspector) != 0)
            {
                hideFlagsNames += "HideInInspector\n";
                hideFlagsExplanations += "Object is hidden in inspector.\n";
            }

            FlagsText = flagsNames.TrimEnd('\n');
            HideFlagsText = hideFlagsNames.TrimEnd('\n');
            FlagsToolTip = (flagsExplanations + hideFlagsExplanations).TrimEnd('\n');
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}

