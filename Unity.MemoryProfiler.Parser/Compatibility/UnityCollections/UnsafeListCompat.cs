// ============================================================================
// Unity 官方 API 等效实现
// 
// 官方命名空间: Unity.Collections.LowLevel.Unsafe
// 官方类型: UnsafeList<T>
// 官方包版本: com.unity.collections@2.1.4
// 
// 实现说明:
// - Unity 的 UnsafeList<T> 是基于非托管内存的动态数组，用于高性能数据处理
// - 官方实现使用指针和手动内存管理，支持 Burst 编译
// - 本实现使用 Marshal.AllocHGlobal 分配非托管内存，保持与官方相同的内存语义
// - 必须保持 unmanaged 约束，因为官方代码需要对整个结构进行内存操作
// 
// 与官方的差异:
// - 使用 Marshal.AllocHGlobal/FreeHGlobal 而非 Unity 的 UnsafeUtility.Malloc/Free
// - 移除了 Burst 编译优化和 Safety Checks（仅用于 Unity Editor 调试）
// - 移除了 IEnumerable 支持（因为 unmanaged 约束不允许接口实现）
// - 保持了所有核心 API 的行为一致性（Add、Clear、索引器、Length 等）
// ============================================================================

using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Unity.Collections.LowLevel.Unsafe
{
    /// <summary>
    /// 非托管动态数组（.NET 等效实现，使用指针和手动内存管理）
    /// </summary>
    public unsafe struct UnsafeList<T> : IDisposable where T : unmanaged
    {
        T* m_Ptr;
        int m_Length;
        int m_Capacity;
        Unity.Collections.Allocator m_Allocator;

        public UnsafeList(int initialCapacity, Unity.Collections.Allocator allocator)
        {
            m_Length = 0;
            m_Capacity = initialCapacity;
            m_Allocator = allocator;
            
            if (initialCapacity > 0)
            {
                m_Ptr = (T*)Marshal.AllocHGlobal(initialCapacity * sizeof(T));
            }
            else
            {
                m_Ptr = null;
            }
        }

        public readonly int Length
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Length;
        }

        public readonly int Capacity
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => m_Capacity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T value)
        {
            if (m_Length >= m_Capacity)
            {
                Resize(m_Capacity == 0 ? 4 : m_Capacity * 2);
            }
            m_Ptr[m_Length++] = value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Clear()
        {
            m_Length = 0;
        }

        void Resize(int newCapacity)
        {
            T* newPtr = (T*)Marshal.AllocHGlobal(newCapacity * sizeof(T));
            
            if (m_Ptr != null)
            {
                // 复制现有数据
                Buffer.MemoryCopy(m_Ptr, newPtr, newCapacity * sizeof(T), m_Length * sizeof(T));
                Marshal.FreeHGlobal((IntPtr)m_Ptr);
            }
            
            m_Ptr = newPtr;
            m_Capacity = newCapacity;
        }

        public T this[int index]
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            readonly get
            {
                if ((uint)index >= (uint)m_Length)
                    throw new IndexOutOfRangeException();
                return m_Ptr[index];
            }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            set
            {
                if ((uint)index >= (uint)m_Length)
                    throw new IndexOutOfRangeException();
                m_Ptr[index] = value;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly T* GetUnsafePtr()
        {
            return m_Ptr;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly T* GetUnsafeReadOnlyPtr()
        {
            return m_Ptr;
        }

        public void Dispose()
        {
            if (m_Ptr != null)
            {
                Marshal.FreeHGlobal((IntPtr)m_Ptr);
                m_Ptr = null;
            }
            m_Length = 0;
            m_Capacity = 0;
        }

        /// <summary>
        /// 获取枚举器（用于 foreach 支持，但不实现 IEnumerable 以保持 unmanaged）
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary>
        /// 枚举器结构（unmanaged）
        /// </summary>
        public struct Enumerator
        {
            readonly UnsafeList<T> m_List;
            int m_Index;

            internal Enumerator(UnsafeList<T> list)
            {
                m_List = list;
                m_Index = -1;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext()
            {
                m_Index++;
                return m_Index < m_List.Length;
            }

            public readonly T Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get => m_List[m_Index];
            }
        }
    }
}
