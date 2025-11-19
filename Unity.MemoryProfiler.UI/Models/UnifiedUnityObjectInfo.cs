using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.Format;
using UnityEngine;
using static Unity.MemoryProfiler.Editor.CachedSnapshot;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// 统一的Unity对象信息（Native + Managed 完整信息）
    /// 移植自: Unity.MemoryProfiler.Editor.UI.UnifiedUnityObjectInfo (com.unity.memoryprofiler@1.1.6)
    /// </summary>
    internal struct UnifiedUnityObjectInfo
    {
        public static UnifiedUnityObjectInfo Invalid => new UnifiedUnityObjectInfo(null, UnifiedType.Invalid, default(ObjectData));
        public bool IsValid => Type.IsUnityObjectType && (NativeObjectIndex != -1 || ManagedObjectIndex != -1);

        public long NativeObjectIndex => NativeObjectData.nativeObjectIndex;
        public ObjectData NativeObjectData;
        public readonly long ManagedObjectIndex;
        public ObjectData ManagedObjectData;

        public UnifiedType Type;
        public int NativeTypeIndex => Type.NativeTypeIndex;
        public int ManagedTypeIndex => Type.ManagedTypeIndex;
        public string NativeTypeName => Type.NativeTypeName;
        public string ManagedTypeName => Type.ManagedTypeName;

        public ulong TotalSize => NativeSize + (ulong)ManagedSize;
        public int TotalRefCount => ManagedRefCount + NativeRefCount;

        public bool IsLeakedShell => !HasNativeSide && HasManagedSide;
        public bool IsFullUnityObjet => HasNativeSide && HasManagedSide;

        public bool IsComponent => Type.IsComponentType;
        public bool IsMonoBehaviour => Type.IsMonoBehaviourType;
        public bool IsGameObject => Type.IsGameObjectType;
        public bool IsTransform => Type.IsTransformType;
        // Derived Meta Types:
        // Scene Objects are GameObjects and Components, unless they are attached to a prefab (IsPersistent), then they are assets
        public bool IsSceneObject => Type.IsSceneObjectType && !IsPersistentAsset;
        public bool IsAssetObject => Type.IsAssetObjectType && !IsManager || Type.IsSceneObjectType && IsPersistentAsset;

        // Native Object Only info
        public bool HasNativeSide => NativeObjectIndex != -1;
        public readonly InstanceID InstanceId;
        public readonly ulong NativeSize;
        public readonly string NativeObjectName;
        public readonly HideFlags HideFlags;
        public readonly ObjectFlags Flags;
        public readonly int NativeRefCount;
        public bool IsPersistentAsset => Flags.HasFlag(ObjectFlags.IsPersistent) && !IsManager;
        public bool IsRuntimeCreated => InstanceId.IsRuntimeCreated();
        public bool IsManager => Flags.HasFlag(ObjectFlags.IsManager);
        public bool IsDontUnload => Flags.HasFlag(ObjectFlags.IsDontDestroyOnLoad) || HideFlags.HasFlag(HideFlags.DontUnloadUnusedAsset);

        // Managed Object Only info
        public bool HasManagedSide => ManagedObjectIndex != -1;
        public readonly int ManagedRefCount;
        public readonly long ManagedSize;

        public UnifiedUnityObjectInfo(CachedSnapshot snapshot, ObjectData unityObject)
            : this(snapshot, new UnifiedType(snapshot, unityObject), unityObject)
        { }

        public UnifiedUnityObjectInfo(CachedSnapshot snapshot, UnifiedType type, ObjectData unityObject)
        {
            Type = type;
            if (snapshot == null || !unityObject.IsValid || !type.IsValid || !type.IsUnityObjectType)
            {
                NativeObjectData = default;
                ManagedObjectData = default;
                ManagedObjectIndex = -1;
                InstanceId = InstanceID.None;
                NativeSize = 0;
                NativeObjectName = string.Empty;
                HideFlags = 0;
                Flags = 0;
                ManagedSize = ManagedRefCount = NativeRefCount = 0;
                return;
            }

            ManagedObjectInfo managedObjectInfo = default;
            // get the managed/native counterpart and/or type
            if (unityObject.isNativeObject)
            {
                NativeObjectData = unityObject;
                ManagedObjectIndex = snapshot.NativeObjects.ManagedObjectIndex[NativeObjectData.nativeObjectIndex];
                ManagedObjectData = ObjectData.FromManagedObjectIndex(snapshot, ManagedObjectIndex);
                if (ManagedObjectData.IsValid)
                    managedObjectInfo = ManagedObjectData.GetManagedObject(snapshot);
            }
            else if (unityObject.isManaged)
            {
                ManagedObjectData = unityObject;
                managedObjectInfo = unityObject.GetManagedObject(snapshot);
                ManagedObjectIndex = managedObjectInfo.ManagedObjectIndex;
                if (managedObjectInfo.NativeObjectIndex >= -1)
                    NativeObjectData = ObjectData.FromNativeObjectIndex(snapshot, managedObjectInfo.NativeObjectIndex);
                else
                    NativeObjectData = ObjectData.Invalid;
            }
            else
            {
                ManagedObjectData = ObjectData.Invalid;
                NativeObjectData = ObjectData.Invalid;
                ManagedObjectIndex = -1;
            }

            // Native Object Only
            if (NativeObjectData.IsValid)
            {
                Flags = NativeObjectData.GetFlags(snapshot);

                InstanceId = NativeObjectData.GetInstanceID(snapshot);
                NativeSize = snapshot.NativeObjects.Size[NativeObjectData.nativeObjectIndex];
                NativeObjectName = snapshot.NativeObjects.ObjectName[NativeObjectData.nativeObjectIndex];
                HideFlags = snapshot.NativeObjects.HideFlags[NativeObjectData.nativeObjectIndex];
                NativeRefCount = snapshot.NativeObjects.RefCount[NativeObjectData.nativeObjectIndex];
                // Discount the Native Reference to the Managed Object, that is established via a GCHandle
                if (ManagedObjectData.IsValid && NativeRefCount >= 1)
                    --NativeRefCount;
            }
            else
            {
                InstanceId = InstanceID.None;
                NativeSize = 0;
                NativeObjectName = string.Empty;
                HideFlags = 0;
                Flags = 0;
                NativeRefCount = 0;
            }

            // Managed Object Only
            if (ManagedObjectData.IsValid)
            {
                ManagedRefCount = managedObjectInfo.RefCount;
                // Discount the Managed Reference to the Native Object, that is established via m_CachedPtr
                if (NativeObjectData.IsValid && ManagedRefCount >= 1)
                    --ManagedRefCount;
                ManagedSize = managedObjectInfo.Size;
            }
            else
            {
                ManagedRefCount = 0;
                ManagedSize = 0;
            }
        }
    }
}

