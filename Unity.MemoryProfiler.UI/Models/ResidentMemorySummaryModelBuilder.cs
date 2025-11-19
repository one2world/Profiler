using System;
using System.Collections.Generic;
using Unity.MemoryProfiler.Editor;
using UnityEngine;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Builds Resident Memory summary from CachedSnapshot.
    /// 对应Unity的ResidentMemorySummaryModelBuilder (Line 11-104)
    /// </summary>
    internal class ResidentMemorySummaryModelBuilder : IMemorySummaryModelBuilder<MemorySummaryModel>
    {
        const string k_WarningMessageTemplate = "On {0}, all memory that is Allocated is also Resident on device";
        const string k_WarningMessageSingle = "this platform";
        const string k_WarningMessageComparePlatformA = "Platform A";
        const string k_WarningMessageComparePlatformB = "Platform B";
        const string k_WarningMessageCompareBothPlatforms = "these platforms";

        private readonly CachedSnapshot m_SnapshotA;
        private readonly CachedSnapshot m_SnapshotB;

        public ResidentMemorySummaryModelBuilder(CachedSnapshot snapshotA, CachedSnapshot snapshotB = null)
        {
            m_SnapshotA = snapshotA;
            m_SnapshotB = snapshotB;
        }

        public MemorySummaryModel Build()
        {
            MemorySize a, b;
            CalculateTotals(m_SnapshotA, out a);
            if (m_SnapshotB != null)
                CalculateTotals(m_SnapshotB, out b);
            else
                b = new MemorySize();

            bool compareMode = m_SnapshotB != null;
            return new MemorySummaryModel(
                "Resident Memory", // SummaryTextContent.kResidentMemoryTitle
                "Memory resident on device", // SummaryTextContent.kResidentMemoryDescription
                compareMode,
                (long)a.Committed,
                (long)b.Committed,
                new List<MemorySummaryModel.Row>()
                {
                    new MemorySummaryModel.Row(
                        "Resident", // SummaryTextContent.kResidentMemoryCategoryResident
                        a,
                        b,
                        "resident",
                        "Memory resident on device", // TextContent.ResidentMemoryDescription
                        null)
                },
                MakeResidentMemoryWarning());
        }

        /// <summary>
        /// 对应Unity ResidentMemorySummaryModelBuilder.CalculateTotals (Line 50-72)
        /// </summary>
        private void CalculateTotals(CachedSnapshot cs, out MemorySize total)
        {
            var _total = new MemorySize();

            // For the newer captures calculates total values based on system regions and resident pages
            // For the old captures use system regions only, which might produce slightly less accurate values.
            if (cs.HasSystemMemoryRegionsInfo && cs.HasSystemMemoryResidentPages)
            {
                // Calculate total committed and resident from system regions
                cs.EntriesMemoryMap.ForEachFlatWithResidentSize((_, address, size, residentSize, source) =>
                {
                    _total += new MemorySize(size, residentSize);
                });
            }
            else if (cs.HasSystemMemoryRegionsInfo)
            {
                for (int i = 0; i < cs.SystemMemoryRegions.Count; i++)
                    _total += new MemorySize(cs.SystemMemoryRegions.RegionSize[i], cs.SystemMemoryRegions.RegionResident[i]);
            }

            total = _total;
        }

        /// <summary>
        /// 对应Unity ResidentMemorySummaryModelBuilder.MakeResidentMemoryWarning (Line 74-102)
        /// </summary>
        public string MakeResidentMemoryWarning()
        {
            if (m_SnapshotA == null)
                return string.Empty;

            // Single snapshot mode
            bool warnPlatformA = m_SnapshotA.MetaData.TargetInfo.HasValue &&
                                 IsResidentMemoryBlacklistedPlatform(m_SnapshotA.MetaData.TargetInfo.Value.RuntimePlatform);
            if (m_SnapshotB == null)
            {
                if (warnPlatformA)
                    return string.Format(k_WarningMessageTemplate, k_WarningMessageSingle);
                else
                    return string.Empty;
            }

            // Compare mode
            string platformText;
            bool warnPlatformB = m_SnapshotB.MetaData.TargetInfo.HasValue &&
                                 IsResidentMemoryBlacklistedPlatform(m_SnapshotB.MetaData.TargetInfo.Value.RuntimePlatform);
            if (warnPlatformA && !warnPlatformB)
                platformText = k_WarningMessageComparePlatformA;
            else if (!warnPlatformA && warnPlatformB)
                platformText = k_WarningMessageComparePlatformB;
            else if (warnPlatformA && warnPlatformB)
                platformText = k_WarningMessageCompareBothPlatforms;
            else
                return string.Empty;

            return string.Format(k_WarningMessageTemplate, platformText);
        }

        /// <summary>
        /// 检查是否是Resident Memory不可用的平台（例如Windows/macOS/Linux）
        /// 对应Unity PlatformsHelper.IsResidentMemoryBlacklistedPlatform
        /// </summary>
        private bool IsResidentMemoryBlacklistedPlatform(RuntimePlatform platform)
        {
            // Windows, macOS, Linux Desktop platforms treat all allocated memory as resident
            return platform == RuntimePlatform.WindowsEditor ||
                   platform == RuntimePlatform.WindowsPlayer ||
                   platform == RuntimePlatform.OSXEditor ||
                   platform == RuntimePlatform.OSXPlayer ||
                   platform == RuntimePlatform.LinuxEditor ||
                   platform == RuntimePlatform.LinuxPlayer;
        }
    }
}

