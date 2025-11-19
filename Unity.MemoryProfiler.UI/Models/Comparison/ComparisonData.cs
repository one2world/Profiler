using System;
using System.Collections.Generic;

namespace Unity.MemoryProfiler.UI.Models.Comparison
{
    /// <summary>
    /// 对比数据结构，表示两个快照之间某一项的差异
    /// 等价于Unity的ComparisonTableModel.ComparisonData
    /// </summary>
    public readonly struct ComparisonData
    {
        public ComparisonData(
            string name,
            ulong totalSizeInA,
            ulong totalSizeInB,
            uint countInA,
            uint countInB,
            List<string> itemPath)
        {
            Name = name;
            SizeDelta = Convert.ToInt64(totalSizeInB) - Convert.ToInt64(totalSizeInA);
            TotalSizeInA = totalSizeInA;
            TotalSizeInB = totalSizeInB;
            CountInA = countInA;
            CountInB = countInB;
            CountDelta = Convert.ToInt32(countInB) - Convert.ToInt32(countInA);
            HasChanged = TotalSizeInA != TotalSizeInB || CountInA != CountInB;
            ItemPath = itemPath ?? new List<string>();
        }

        /// <summary>
        /// 项名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 大小差值（字节），计算方式：B - A
        /// 正值表示增长，负值表示减少
        /// </summary>
        public long SizeDelta { get; }

        /// <summary>
        /// A快照中的总大小（含子项），单位：字节
        /// </summary>
        public ulong TotalSizeInA { get; }

        /// <summary>
        /// B快照中的总大小（含子项），单位：字节
        /// </summary>
        public ulong TotalSizeInB { get; }

        /// <summary>
        /// A快照中的数量
        /// </summary>
        public uint CountInA { get; }

        /// <summary>
        /// B快照中的数量
        /// </summary>
        public uint CountInB { get; }

        /// <summary>
        /// 数量差值，计算方式：B - A
        /// 正值表示增加，负值表示减少
        /// </summary>
        public int CountDelta { get; }

        /// <summary>
        /// 是否有变化（大小或数量不同）
        /// </summary>
        public bool HasChanged { get; }

        /// <summary>
        /// 项路径（从根到当前节点的路径），用于过滤详细表
        /// </summary>
        public List<string> ItemPath { get; }
    }
}

