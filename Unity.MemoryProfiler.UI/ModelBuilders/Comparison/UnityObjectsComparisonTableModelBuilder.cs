using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MemoryProfiler.Editor;
using Unity.MemoryProfiler.Editor.UI.Models;
using Unity.MemoryProfiler.UI.Models;
using Unity.MemoryProfiler.UI.Models.Comparison;

namespace Unity.MemoryProfiler.UI.ModelBuilders.Comparison
{
    /// <summary>
    /// UnityObjects对比表模型构建器
    /// 等价于Unity的UnityObjectsComparisonTableModelBuilder
    /// </summary>
    internal static class UnityObjectsComparisonTableModelBuilder
    {
        /// <summary>
        /// 构建对比表模型
        /// </summary>
        /// <param name="snapshotA">A快照</param>
        /// <param name="snapshotB">B快照</param>
        /// <param name="flattenHierarchy">是否扁平化层级（只显示叶子节点）</param>
        /// <param name="includeUnchanged">是否包含未变化的项</param>
        /// <returns>对比表模型</returns>
        internal static ComparisonTableModel Build(
            CachedSnapshot snapshotA,
            CachedSnapshot snapshotB,
            bool flattenHierarchy = false,
            bool includeUnchanged = false)
        {
            if (snapshotA == null)
                throw new ArgumentNullException(nameof(snapshotA));
            if (snapshotB == null)
                throw new ArgumentNullException(nameof(snapshotB));

            // 步骤1：使用UnityObjectsDataBuilder分别构建A和B的模型
            // 注意：这里总是构建层级结构，扁平化在对比树构建后进行
            var buildOptions = new Unity.MemoryProfiler.UI.Services.UnityObjectsDataBuilder.BuildOptions
            {
                FlattenHierarchy = false, // 总是构建层级结构
                ShowPotentialDuplicatesOnly = false,
                DisambiguateByInstanceId = false
            };

            var builderA = new Unity.MemoryProfiler.UI.Services.UnityObjectsDataBuilder(snapshotA);
            var modelA = builderA.Build(buildOptions);

            var builderB = new Unity.MemoryProfiler.UI.Services.UnityObjectsDataBuilder(snapshotB);
            var modelB = builderB.Build(buildOptions);

            // 步骤2：直接构建对比树（保留原始节点列表）
            var comparisonTree = BuildComparisonTree(
                modelA.RootNodes,
                modelB.RootNodes,
                includeUnchanged,
                out var largestAbsoluteSizeDelta);
            
            // 步骤3：如果需要扁平化，提取所有叶子节点
            if (flattenHierarchy)
            {
                comparisonTree = RetrieveLeafNodes(comparisonTree);
            }

            // 步骤4：获取快照的总内存大小
            var totalSnapshotSizeA = snapshotA.MetaData.TargetMemoryStats.HasValue 
                ? snapshotA.MetaData.TargetMemoryStats.Value.TotalVirtualMemory 
                : modelA.TotalSnapshotMemory;
            var totalSnapshotSizeB = snapshotB.MetaData.TargetMemoryStats.HasValue 
                ? snapshotB.MetaData.TargetMemoryStats.Value.TotalVirtualMemory 
                : modelB.TotalSnapshotMemory;

            // 步骤5：创建并返回ComparisonTableModel
            var comparisonModel = new ComparisonTableModel(
                comparisonTree,
                totalSnapshotSizeA,
                totalSnapshotSizeB,
                largestAbsoluteSizeDelta);

            return comparisonModel;
        }

        /// <summary>
        /// 构建对比树（层级结构）
        /// Unity 逻辑：
        /// - 类型节点（Type）：父节点，显示在主对比表
        /// - 对象组节点（Object Group）：子节点，显示在主对比表的子级
        /// - 具体对象（Objects）：保存在 SourceNodesA/B 中，用于子表显示
        /// </summary>
        private static List<Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode> BuildComparisonTree(
            List<UnityObjectTreeNode> nodesA,
            List<UnityObjectTreeNode> nodesB,
            bool includeUnchanged,
            out long largestAbsoluteSizeDelta)
        {
            largestAbsoluteSizeDelta = 0;
            var result = new List<Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode>();

            // 按类型名称分组（根节点是类型节点）
            var typeGroupsA = nodesA.GroupBy(n => n.Name).ToDictionary(g => g.Key, g => g.ToList());
            var typeGroupsB = nodesB.GroupBy(n => n.Name).ToDictionary(g => g.Key, g => g.ToList());

            // 合并所有类型名称
            var allTypeNames = new HashSet<string>(typeGroupsA.Keys);
            allTypeNames.UnionWith(typeGroupsB.Keys);

            foreach (var typeName in allTypeNames.OrderBy(n => n))
            {
                typeGroupsA.TryGetValue(typeName, out var typeNodesA);
                typeGroupsB.TryGetValue(typeName, out var typeNodesB);

                // 获取类型节点（应该只有一个）
                var typeNodeA = typeNodesA?.FirstOrDefault();
                var typeNodeB = typeNodesB?.FirstOrDefault();

                // 构建子节点（对象组级别的对比）
                var childComparisonNodes = BuildObjectGroupComparisonNodes(
                    typeNodeA?.Children,
                    typeNodeB?.Children,
                    includeUnchanged,
                    ref largestAbsoluteSizeDelta);

                // 计算类型级别的统计信息（汇总所有子节点）
                var sizeInA = typeNodeA?.TotalSize ?? 0;
                var sizeInB = typeNodeB?.TotalSize ?? 0;
                var countInA = (uint)(childComparisonNodes.Sum(c => (long)c.Data.CountInA));
                var countInB = (uint)(childComparisonNodes.Sum(c => (long)c.Data.CountInB));

                var sizeDelta = (long)sizeInB - (long)sizeInA;
                var countDelta = (int)countInB - (int)countInA;

                // 创建类型级别的对比数据
                var data = new ComparisonData(
                    typeName,
                    sizeInA,
                    sizeInB,
                    countInA,
                    countInB,
                    itemPath: null); // Unity Objects 不使用 itemPath
                
                // Unity 逻辑：如果不包含未变化的项，检查是否有变化
                if (!includeUnchanged && !data.HasChanged)
                    continue;

                // 更新最大绝对差值
                largestAbsoluteSizeDelta = Math.Max(Math.Abs(sizeDelta), largestAbsoluteSizeDelta);

                // 收集该类型下的所有对象（扁平化，用于子表显示）
                var allObjectsA = new List<UnityObjectTreeNode>();
                var allObjectsB = new List<UnityObjectTreeNode>();
                if (typeNodeA?.Children != null)
                    CollectAllChildren(typeNodeA.Children, allObjectsA);
                if (typeNodeB?.Children != null)
                    CollectAllChildren(typeNodeB.Children, allObjectsB);

                // 创建类型节点（带子节点）
                var comparisonNode = new Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode(
                    data,
                    children: childComparisonNodes, // 包含对象组子节点
                    sourceNodesA: allObjectsA.Count > 0 ? allObjectsA : null,
                    sourceNodesB: allObjectsB.Count > 0 ? allObjectsB : null);

                result.Add(comparisonNode);
            }

            return result;
        }

        /// <summary>
        /// 构建对象组级别的对比节点
        /// </summary>
        private static List<Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode> BuildObjectGroupComparisonNodes(
            List<UnityObjectTreeNode>? objectGroupsA,
            List<UnityObjectTreeNode>? objectGroupsB,
            bool includeUnchanged,
            ref long largestAbsoluteSizeDelta)
        {
            var result = new List<Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode>();

            // 按对象名称分组
            var groupsA = objectGroupsA?.GroupBy(n => n.Name).ToDictionary(g => g.Key, g => g.ToList()) 
                ?? new Dictionary<string, List<UnityObjectTreeNode>>();
            var groupsB = objectGroupsB?.GroupBy(n => n.Name).ToDictionary(g => g.Key, g => g.ToList()) 
                ?? new Dictionary<string, List<UnityObjectTreeNode>>();

            // 合并所有对象名称
            var allObjectNames = new HashSet<string>(groupsA.Keys);
            allObjectNames.UnionWith(groupsB.Keys);

            foreach (var objectName in allObjectNames.OrderBy(n => n))
            {
                groupsA.TryGetValue(objectName, out var objectsA);
                groupsB.TryGetValue(objectName, out var objectsB);

                // 计算对象组的统计信息
                var sizeInA = (ulong)(objectsA?.Sum(n => (long)n.TotalSize) ?? 0);
                var sizeInB = (ulong)(objectsB?.Sum(n => (long)n.TotalSize) ?? 0);
                var countInA = (uint)(objectsA?.Count ?? 0);
                var countInB = (uint)(objectsB?.Count ?? 0);

                // 创建对象组的对比数据
                var data = new ComparisonData(
                    objectName,
                    sizeInA,
                    sizeInB,
                    countInA,
                    countInB,
                    itemPath: null);
                
                // Unity 逻辑：如果不包含未变化的项，检查是否有变化
                if (!includeUnchanged && !data.HasChanged)
                    continue;

                var sizeDelta = (long)sizeInB - (long)sizeInA;
                
                // 更新最大绝对差值
                largestAbsoluteSizeDelta = Math.Max(Math.Abs(sizeDelta), largestAbsoluteSizeDelta);

                // 创建对象组节点（保留原始对象列表）
                var comparisonNode = new Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode(
                    data,
                    children: null, // 对象组节点没有子节点
                    sourceNodesA: objectsA,
                    sourceNodesB: objectsB);

                result.Add(comparisonNode);
            }

            return result;
        }

        /// <summary>
        /// 递归收集所有子节点（扁平化）
        /// </summary>
        private static void CollectAllChildren(List<UnityObjectTreeNode> nodes, List<UnityObjectTreeNode> result)
        {
            foreach (var node in nodes)
            {
                result.Add(node);
                if (node.Children != null && node.Children.Count > 0)
                {
                    CollectAllChildren(node.Children, result);
                }
            }
        }
        
        /// <summary>
        /// 提取所有叶子节点（扁平化）
        /// Unity 逻辑：TreeModelUtility.RetrieveLeafNodesOfTree
        /// </summary>
        private static List<Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode> RetrieveLeafNodes(
            List<Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode> nodes)
        {
            var result = new List<Unity.MemoryProfiler.UI.Models.Comparison.ComparisonTreeNode>();
            
            foreach (var node in nodes)
            {
                if (node.Children == null || node.Children.Count == 0)
                {
                    // 叶子节点，直接添加
                    result.Add(node);
                }
                else
                {
                    // 非叶子节点，递归提取子节点的叶子节点
                    result.AddRange(RetrieveLeafNodes(node.Children));
                }
            }
            
            return result;
        }
    }
}

