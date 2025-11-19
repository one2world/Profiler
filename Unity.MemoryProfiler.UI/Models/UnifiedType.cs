using Unity.MemoryProfiler.Editor;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 统一的类型信息（Managed + Native）
    /// 移植自: Unity.MemoryProfiler.Editor.UI.UnifiedType (com.unity.memoryprofiler@1.1.6)
    /// </summary>
    internal struct UnifiedType
    {
        public static UnifiedType Invalid => new UnifiedType(null, default(ObjectData));
        public bool IsValid => (HasManagedType || HasNativeType) && NativeTypeName != null;
        public readonly ObjectData ManagedTypeData;
        public readonly bool ManagedTypeIsBaseTypeFallback;
        public readonly int NativeTypeIndex;
        public readonly int ManagedTypeIndex;
        public bool HasManagedType => ManagedTypeIndex >= 0;
        public bool HasNativeType => NativeTypeIndex >= 0;
        public bool IsUnifiedType => HasManagedType && HasNativeType;

        public readonly string NativeTypeName;
        public readonly string ManagedTypeName;

        public readonly bool IsUnityObjectType;
        public readonly bool IsMonoBehaviourType;
        public readonly bool IsComponentType;
        public readonly bool IsGameObjectType;
        public readonly bool IsTransformType;
        // Derived Meta Types:
        public bool IsSceneObjectType => IsComponentType || IsGameObjectType || IsTransformType;
        public bool IsAssetObjectType => IsValid && !IsSceneObjectType;

        public UnifiedType(CachedSnapshot snapshot, int nativeTypeIndex)
        {
            ManagedTypeIndex = -1;
            NativeTypeName = ManagedTypeName = string.Empty;
            ManagedTypeData = default;
            ManagedTypeIsBaseTypeFallback = false;
            if (nativeTypeIndex >= 0)
            {
                IsUnityObjectType = true;
                NativeTypeIndex = nativeTypeIndex;
                IsMonoBehaviourType = snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.MonoBehaviourIdx);
                IsComponentType = snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.ComponentIdx);
                IsGameObjectType = snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.GameObjectIdx);
                IsTransformType = snapshot.NativeTypes.IsTransformOrRectTransform(NativeTypeIndex);
                NativeTypeName = snapshot.NativeTypes.TypeName[NativeTypeIndex];

                if (snapshot.CrawledData.NativeUnityObjectTypeIndexToManagedBaseTypeIndex.TryGetValue(NativeTypeIndex, out ManagedTypeIndex))
                {
                    // The Managed Crawler had found an object of this type using it's Managed Base Type,
                    // i.e. not a derived one like a MonoBehaviour (those are always in that dictionary but they don't exist without their Managed Shell)
                    ManagedTypeName = snapshot.TypeDescriptions.TypeDescriptionName[ManagedTypeIndex];
                    ManagedTypeData = ObjectData.FromManagedType(snapshot, ManagedTypeIndex);
                    ManagedTypeIsBaseTypeFallback = true;
                }
                else
                {
                    // reset to invalid in case TryGetValue sets this to 0
                    ManagedTypeIndex = -1;
                }
            }
            else
            {
                NativeTypeIndex = -1;
                IsUnityObjectType = IsMonoBehaviourType = IsComponentType = IsGameObjectType = IsTransformType = false;
            }
        }

        public UnifiedType(CachedSnapshot snapshot, ObjectData objectData)
        {
            ManagedTypeIsBaseTypeFallback = false;
            if (snapshot == null || !objectData.IsValid)
            {
                ManagedTypeData = default;
                NativeTypeIndex = -1;
                ManagedTypeIndex = -1;
                NativeTypeName = ManagedTypeName = string.Empty;
                IsUnityObjectType = IsMonoBehaviourType = IsComponentType = IsGameObjectType = IsTransformType = false;
                return;
            }
            if (objectData.isNativeObject)
            {
                IsUnityObjectType = true;
                var nativeObjectData = objectData;
                NativeTypeIndex = snapshot.NativeObjects.NativeTypeArrayIndex[nativeObjectData.nativeObjectIndex];
                var managedObjectIndex = snapshot.NativeObjects.ManagedObjectIndex[nativeObjectData.nativeObjectIndex];
                if (managedObjectIndex >= 0)
                    ManagedTypeIndex = snapshot.CrawledData.ManagedObjects[managedObjectIndex].ITypeDescription;
                else if (snapshot.CrawledData.NativeUnityObjectTypeIndexToManagedBaseTypeIndex.TryGetValue(NativeTypeIndex, out ManagedTypeIndex))
                    ManagedTypeIsBaseTypeFallback = true;
                else
                    ManagedTypeIndex = -1;
            }
            else if (objectData.isManaged)
            {
                IsUnityObjectType = false;
                ManagedTypeIndex = objectData.managedTypeIndex;
                if (snapshot.TypeDescriptions.UnityObjectTypeIndexToNativeTypeIndex.ContainsKey(objectData.managedTypeIndex))
                {
                    IsUnityObjectType = true;
                    NativeTypeIndex = snapshot.TypeDescriptions.UnityObjectTypeIndexToNativeTypeIndex[objectData.managedTypeIndex];
                }
                else
                    NativeTypeIndex = -1;
            }
            else
            {
                ManagedTypeIndex = NativeTypeIndex = -1;
                IsUnityObjectType = false;
            }

            if (ManagedTypeIndex >= 0)
            {
                ManagedTypeName = snapshot.TypeDescriptions.TypeDescriptionName[ManagedTypeIndex];
                ManagedTypeData = ObjectData.FromManagedType(snapshot, ManagedTypeIndex);
            }
            else
            {
                ManagedTypeName = string.Empty;
                ManagedTypeData = default;
            }

            if (IsUnityObjectType && NativeTypeIndex >= 0)
            {
                IsMonoBehaviourType = snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.MonoBehaviourIdx);
                IsComponentType = snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.ComponentIdx);
                IsGameObjectType = snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.GameObjectIdx);
                IsTransformType = snapshot.NativeTypes.IsTransformOrRectTransform(NativeTypeIndex) || /*Is the rest here necessary?*/ snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.TransformIdx) || snapshot.NativeTypes.IsOrDerivesFrom(NativeTypeIndex, snapshot.NativeTypes.RectTransformIdx);
                NativeTypeName = snapshot.NativeTypes.TypeName[NativeTypeIndex];
            }
            else
            {
                IsUnityObjectType = IsMonoBehaviourType = IsComponentType = IsGameObjectType = IsTransformType = false;
                NativeTypeName = string.Empty;
            }
        }
    }
}

