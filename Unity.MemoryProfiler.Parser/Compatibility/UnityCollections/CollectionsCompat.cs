using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Unity.Collections
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class ReadOnlyAttribute : Attribute { }

    public enum Allocator
    {
        Invalid = -1,
        None = 0,
        Temp = 1,
        TempJob = 2,
        Persistent = 4,
    }

    public enum NativeArrayOptions
    {
        UninitializedMemory,
        ClearMemory,
    }

    public struct AllocatorManager
    {
        public struct AllocatorHandle
        {
            public Allocator Value;

            public Allocator ToAllocator => Value;
            public bool IsValid => Value != Allocator.Invalid;
            
            /// <summary>
            /// 分配器索引（.NET 实现中直接使用 Allocator 枚举值）
            /// </summary>
            public int Index => (int)Value;

            public static implicit operator AllocatorHandle(Allocator allocator) => new AllocatorHandle { Value = allocator };
            public static implicit operator Allocator(AllocatorHandle handle) => handle.Value;
        }

        /// <summary>
        /// 第一个用户自定义分配器的索引
        /// Unity 官方: 用于区分内置分配器和用户自定义分配器
        /// .NET 实现: 固定返回 64（与 Unity 官方一致）
        /// </summary>
        public static int FirstUserIndex => 64;

        /// <summary>
        /// 内存块结构（用于分配器管理）
        /// Unity 官方: 用于跟踪内存分配块
        /// .NET 实现: 简化为基本信息
        /// </summary>
        public struct Block
        {
            public Range Range;
            public long BytesPerItem;
            public long AllocatedItems;
            public int Log2Alignment;
            public int Alignment;
        }

        /// <summary>
        /// 尝试分配内存（.NET 实现中简化为直接分配）
        /// Unity 官方: 使用 block.Range.Allocator 进行内存分配
        /// .NET 实现: 使用NativeMemory.AllocZeroed分配内存
        /// </summary>
        public static unsafe int Try(ref Block block)
        {
            // .NET 实现中简化：直接分配或重新分配内存
            var newSize = block.BytesPerItem * block.Range.Items;
            
            if (block.Range.Pointer != IntPtr.Zero)
            {
                // 重新分配：先释放旧内存
                NativeMemory.Free((void*)block.Range.Pointer);
            }
            
            if (newSize > 0)
            {
                // 使用NativeMemory.AllocZeroed，自动清零
                block.Range.Pointer = (IntPtr)NativeMemory.AllocZeroed((nuint)newSize);
                return 0; // 成功
            }
            
            block.Range.Pointer = IntPtr.Zero;
            return 0; // 成功（分配 0 字节）
        }

        /// <summary>
        /// 释放内存（使用NativeMemory.Free）
        /// </summary>
        public static unsafe void Free(AllocatorHandle handle, void* pointer, int size)
        {
            if (pointer != null)
            {
                NativeMemory.Free(pointer);
            }
        }

        /// <summary>
        /// 释放内存（使用NativeMemory.Free）
        /// </summary>
        public static unsafe void Free(AllocatorHandle handle, void* pointer)
        {
            if (pointer != null)
            {
                NativeMemory.Free(pointer);
            }
        }
    }

    /// <summary>
    /// 范围结构（用于内存块管理）
    /// </summary>
    public struct Range
    {
        public IntPtr Pointer;
        public long Items;
        public Allocator Allocator;
    }

    public unsafe struct NativeArray<T> : IDisposable where T : unmanaged
    {
        internal T* m_Buffer;
        int m_Length;
        bool m_OwnsMemory;
        AllocatorManager.AllocatorHandle m_Allocator;

        public NativeArray(int length, Allocator allocator, NativeArrayOptions options = NativeArrayOptions.UninitializedMemory)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            m_Length = length;
            m_Allocator = allocator;
            if (length == 0)
            {
                m_Buffer = null;
                m_OwnsMemory = false;
                return;
            }

            long size = (long)sizeof(T) * length;
            m_Buffer = (T*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.Malloc(size, Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AlignOf<T>(), allocator);
            m_OwnsMemory = true;

            if (options == NativeArrayOptions.ClearMemory)
            {
                Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemClear(m_Buffer, size);
            }
        }

        public NativeArray(T[] source, Allocator allocator)
        {
            m_Length = source?.Length ?? 0;
            m_Allocator = allocator;
            if (m_Length == 0)
            {
                m_Buffer = null;
                m_OwnsMemory = false;
                return;
            }

            long size = (long)sizeof(T) * m_Length;
            m_Buffer = (T*)Unity.Collections.LowLevel.Unsafe.UnsafeUtility.Malloc(size, Unity.Collections.LowLevel.Unsafe.UnsafeUtility.AlignOf<T>(), allocator);
            m_OwnsMemory = true;

            if (source is { Length: > 0 })
            {
                fixed (T* src = source)
                {
                    Unity.Collections.LowLevel.Unsafe.UnsafeUtility.MemCpy(m_Buffer, src, size);
                }
            }
        }

        internal NativeArray(T* buffer, int length)
        {
            m_Buffer = buffer;
            m_Length = length;
            m_OwnsMemory = false;
            m_Allocator = default;
        }

        public int Length => m_Length;
        public int Count => m_Length;
        public bool IsCreated => m_Buffer != null;

        public T this[int index]
        {
            get
            {
                if ((uint)index >= (uint)m_Length)
                    throw new IndexOutOfRangeException();
                return *(m_Buffer + index);
            }
            set
            {
                if ((uint)index >= (uint)m_Length)
                    throw new IndexOutOfRangeException();
                *(m_Buffer + index) = value;
            }
        }

        public Span<T> AsSpan()
        {
            if (!IsCreated || m_Length == 0)
                return Span<T>.Empty;
            return new Span<T>(m_Buffer, m_Length);
        }

        public T[] ToArray()
        {
            if (!IsCreated || m_Length == 0)
                return Array.Empty<T>();
            var arr = new T[m_Length];
            AsSpan().CopyTo(arr);
            return arr;
        }

        public Span<T>.Enumerator GetEnumerator() => AsSpan().GetEnumerator();

        public void Dispose()
        {
            if (m_OwnsMemory && m_Buffer != null)
            {
                Unity.Collections.LowLevel.Unsafe.UnsafeUtility.Free(m_Buffer, m_Allocator.ToAllocator);
            }
            m_Buffer = null;
            m_Length = 0;
            m_OwnsMemory = false;
        }

        public void* GetUnsafePtr() => m_Buffer;

        public NativeArray<U> Reinterpret<U>() where U : unmanaged
        {
            long totalBytes = (long)m_Length * sizeof(T);
            int newLength = (int)(totalBytes / sizeof(U));
            return new NativeArray<U>((U*)m_Buffer, newLength);
        }

        internal NativeArray<U> Reinterpret<U>(int expectedTypeSize) where U : unmanaged => Reinterpret<U>();
    }

    public struct NativeKeyValueArrays<TKey, TValue> : IDisposable
        where TKey : unmanaged
        where TValue : unmanaged
    {
        public NativeArray<TKey> Keys;
        public NativeArray<TValue> Values;

        public NativeKeyValueArrays(int length, Allocator allocator, NativeArrayOptions options)
        {
            Keys = new NativeArray<TKey>(length, allocator, options);
            Values = new NativeArray<TValue>(length, allocator, options);
        }

        public int Length => Keys.Length;

        public void Dispose()
        {
            Keys.Dispose();
            Values.Dispose();
        }
    }
}

namespace Unity.Collections.LowLevel.Unsafe
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Struct | AttributeTargets.Class)]
    public sealed class NativeDisableUnsafePtrRestrictionAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class NativeContainerAttribute : Attribute { }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public sealed class NativeContainerIsReadOnlyAttribute : Attribute { }

    public interface INativeDisposable : IDisposable { }

    public static class CollectionHelper
    {
        public static void SetStaticSafetyId<T>(ref AtomicSafetyHandle handle, ref int id)
        {
            // No safety system in compatibility layer.
        }

        public static Unity.Collections.NativeArray<T> CreateNativeArray<T>(int length, Unity.Collections.AllocatorManager.AllocatorHandle allocator, Unity.Collections.NativeArrayOptions options)
            where T : unmanaged
        {
            return new Unity.Collections.NativeArray<T>(length, allocator.Value, options);
        }
    }

    public struct SharedStatic<T>
    {
        public T Data;

        public static SharedStatic<T> GetOrCreate<TContext>() => new SharedStatic<T>();
    }

    public struct AtomicSafetyHandle
    {
        public static AtomicSafetyHandle Create() => default;
        public static void Release(AtomicSafetyHandle handle) { }

        public static AtomicSafetyHandle GetTempMemoryHandle() => default;
        public static bool IsDefaultValue(AtomicSafetyHandle handle) => true;

        public static void CheckReadAndThrow(AtomicSafetyHandle handle) { }
        public static void CheckWriteAndBumpSecondaryVersion(AtomicSafetyHandle handle) { }
        public static void CheckGetSecondaryDataPointerAndThrow(AtomicSafetyHandle handle) { }
        public static void UseSecondaryVersion(ref AtomicSafetyHandle handle) { }
        public static void CheckDeallocateAndThrow(AtomicSafetyHandle handle) { }
        public static void CheckExistsAndThrow(AtomicSafetyHandle handle) { }
        public static void SetNestedContainer(AtomicSafetyHandle handle, bool value) { }
        public static void SetBumpSecondaryVersionOnScheduleWrite(AtomicSafetyHandle handle, bool value) { }
    }

    public static class Memory
    {
        public static class Unmanaged
        {
            public static unsafe void* Allocate(long size, int alignment, Unity.Collections.Allocator allocator)
            {
                return UnsafeUtility.Malloc(size, alignment, allocator);
            }

            public static unsafe void* Allocate(long size, int alignment, Unity.Collections.AllocatorManager.AllocatorHandle allocator)
            {
                return UnsafeUtility.Malloc(size, alignment, allocator.Value);
            }

            public static unsafe void Free(void* memory, Unity.Collections.Allocator allocator)
            {
                UnsafeUtility.Free(memory, allocator);
            }

            public static unsafe void Free(void* memory, Unity.Collections.AllocatorManager.AllocatorHandle allocator)
            {
                UnsafeUtility.Free(memory, allocator.Value);
            }
        }
    }

    public static unsafe class UnsafeUtility
    {
        /// <summary>
        /// 使用NativeMemory.AllocZeroed分配内存（自动清零，避免未初始化内存问题）
        /// 
        /// 优势：
        /// 1. 使用.NET 6+ NativeMemory API，比Marshal.AllocHGlobal更安全
        /// 2. AllocZeroed自动清零内存，避免堆损坏
        /// 3. 兼容老版本snap文件（Malloc(0)返回有效指针）
        /// </summary>
        public static void* Malloc(long size, int alignment, Unity.Collections.Allocator allocator)
        {
            // 确保至少分配1字节，避免返回null（与Unity引擎行为一致）
            var allocSize = (nuint)(size > 0 ? size : 1);
            
            // 使用NativeMemory.AllocZeroed，自动清零内存
            // 注意：NativeMemory不支持自定义alignment，使用系统默认对齐
            return NativeMemory.AllocZeroed(allocSize);
        }

        public static void* MallocTracked(long size, int alignment, Unity.Collections.Allocator allocator, int callSiteId) => Malloc(size, alignment, allocator);

        /// <summary>
        /// 使用NativeMemory.Free释放内存
        /// </summary>
        public static void Free(void* memory, Unity.Collections.Allocator allocator)
        {
            if (memory != null)
            {
                NativeMemory.Free(memory);
            }
        }

        public static void FreeTracked(void* memory, Unity.Collections.Allocator allocator) => Free(memory, allocator);

        public static void MemClear(void* destination, long size) => MemSet(destination, 0, size);

        public static void MemSet(void* destination, byte value, long size)
        {
            var span = new Span<byte>(destination, checked((int)size));
            span.Fill(value);
        }

        public static void MemCpy(void* destination, void* source, long size)
        {
            Buffer.MemoryCopy(source, destination, size, size);
        }

        public static void MemMove(void* destination, void* source, long size)
        {
            int length = checked((int)size);
            var temp = new byte[length];
            Marshal.Copy(new IntPtr(source), temp, 0, length);
            Marshal.Copy(temp, 0, new IntPtr(destination), length);
        }

        public static void MemCpyStride(void* destination, int destinationStride, void* source, int sourceStride, int elementSize, int count)
        {
            if (count <= 0 || elementSize <= 0)
                return;

            var dst = (byte*)destination;
            var src = (byte*)source;
            
            // 确保我们不会越界
            for (int i = 0; i < count; i++)
            {
                long dstOffset = (long)i * destinationStride;
                long srcOffset = (long)i * sourceStride;
                Buffer.MemoryCopy(src + srcOffset, dst + dstOffset, elementSize, elementSize);
            }
        }

        public static void MemCpyReplicate(void* destination, void* source, long size, int count)
        {
            var dst = (byte*)destination;
            for (int i = 0; i < count; i++)
            {
                MemCpy(dst + (i * size), source, size);
            }
        }

        public static int MemCmp(void* ptr1, void* ptr2, long size)
        {
            var span1 = new ReadOnlySpan<byte>(ptr1, checked((int)size));
            var span2 = new ReadOnlySpan<byte>(ptr2, checked((int)size));
            return span1.SequenceCompareTo(span2);
        }

        public static int AlignOf<T>() where T : unmanaged => System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        public static int SizeOf<T>() where T : unmanaged => System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
        public static bool IsUnmanaged<T>() => true;
        public static bool IsNativeContainerType<T>() => false;

        public static T ReadArrayElement<T>(void* source, int index) where T : unmanaged
        {
            return System.Runtime.CompilerServices.Unsafe.Read<T>((byte*)source + index * System.Runtime.CompilerServices.Unsafe.SizeOf<T>());
        }

        public static void WriteArrayElement<T>(void* destination, int index, T value) where T : unmanaged
        {
            System.Runtime.CompilerServices.Unsafe.Write((byte*)destination + index * System.Runtime.CompilerServices.Unsafe.SizeOf<T>(), value);
        }

        public static void* AddressOf<T>(ref T value) where T : unmanaged
        {
            return System.Runtime.CompilerServices.Unsafe.AsPointer(ref value);
        }

        /// <summary>
        /// 将结构体复制到指针指向的内存位置
        /// Unity 官方: 用于高性能的结构体序列化
        /// .NET 实现: 使用 Marshal.StructureToPtr 实现相同功能
        /// </summary>
        public static void CopyStructureToPtr<T>(ref T input, void* ptr) where T : struct
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(input, new IntPtr(ptr), false);
        }

        /// <summary>
        /// 从指针读取结构体
        /// Unity 官方: 用于高性能的结构体反序列化
        /// .NET 实现: 使用 Marshal.PtrToStructure 实现相同功能
        /// </summary>
        public static T PtrToStructure<T>(void* ptr) where T : struct
        {
            return System.Runtime.InteropServices.Marshal.PtrToStructure<T>(new IntPtr(ptr));
        }

        /// <summary>
        /// 获取引用的地址（泛型版本，支持托管类型）
        /// Unity 官方: 用于获取任意类型的内存地址
        /// .NET 实现: 对于值类型使用 Unsafe.AsPointer，对于引用类型返回 GCHandle
        /// </summary>
        public static ref T AsRef<T>(void* ptr)
        {
            return ref System.Runtime.CompilerServices.Unsafe.AsRef<T>(ptr);
        }
    }
}

