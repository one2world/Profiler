using System.Collections.Generic;

namespace Unity.MemoryProfiler.Editor.Managed
{
    /// <summary>
    /// Managed 对象分配数据的聚合结果
    /// </summary>
    public class ManagedAllocationData
    {
        /// <summary>
        /// 内存地址到堆栈Hash的映射
        /// </summary>
        public Dictionary<ulong, uint> AddressToStackHash { get; set; } = new Dictionary<ulong, uint>();

        /// <summary>
        /// 堆栈Hash到完整调用栈的映射
        /// </summary>
        public Dictionary<uint, CallStack> StackHashToCallStack { get; set; } = new Dictionary<uint, CallStack>();

        /// <summary>
        /// 根据对象地址获取调用栈
        /// </summary>
        public CallStack? GetCallStackForAddress(ulong address)
        {
            if (AddressToStackHash.TryGetValue(address, out var hash))
            {
                if (StackHashToCallStack.TryGetValue(hash, out var callStack))
                {
                    return callStack;
                }
            }
            return null;
        }

        /// <summary>
        /// 根据对象地址获取堆栈Hash
        /// </summary>
        public uint? GetStackHashForAddress(ulong address)
        {
            if (AddressToStackHash.TryGetValue(address, out var hash))
            {
                return hash;
            }
            return null;
        }
    }
}

