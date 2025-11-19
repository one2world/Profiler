using System;
using System.Collections.Generic;
using System.Text;
using Unity.MemoryProfiler.Editor;
using Unity.Profiling.Memory;

namespace Unity.MemoryProfiler.UI.Models
{
    /// <summary>
    /// Builds SnapshotIssuesModel by detecting common issues in captured snapshots.
    /// 对应Unity的SnapshotIssuesModelBuilder (Line 10-290)
    /// 实现核心检测逻辑，简化部分Editor特定功能
    /// </summary>
    internal class SnapshotIssuesModelBuilder
    {
        const string kEditorHint = "Editor capture! Get better insights by building and profiling a development build, as memory behaves quite differently in the Editor.";
        const string kEditorHintDiffBoth = "Comparing Editor captures! Get better insights by building and profiling a development build, as memory behaves quite differently in the Editor.";
        const string kEditorHintDiffOne = "Comparing an Editor capture to a Player Capture!";
        const string kEditorHintDiffOneDetails = "Get better insights by building and profiling a development build, as memory behaves quite differently in the Editor. These differences will make this comparison look quite odd.";
        const string kEditorHintDetails = "The memory used in the Editor is not cleanly separated into Play Mode and Edit Mode, but is a fluid mix. Things might be held in memory by editor related functionality, load and unload into memory at different points in time and can take up a different amount than it would in a build on your targeted hardware. Make a development build, attach the Memory Profiler to it and take a capture from that Player to get a more accurate and relevant picture of your memory usage.";

        const string kOldSnapshotWarning = "Snapshot from an outdated Unity version that is not fully supported.";
        static readonly string kOldSnapshotWarningContent = "The functionality of the Memory Profiler package version 0.4 and newer builds on data only reported in newer Unity versions.";

        const string kSystemAllocatorWarning = "System Allocator is used. It is generally advised to use the Dynamic Heap Allocator instead.";
        const string kSystemAllocatorWarningDetailsDiffA = "System Allocator is used in snapshot A.";
        const string kSystemAllocatorWarningDetailsDiffB = "System Allocator is used in snapshot B.";
        const string kSystemAllocatorWarningDetailsDiffBoth = "System Allocator is used in both snapshots.";
        const string kSystemAllocatorWarningDetails = "Dynamic Heap Allocator is generally more efficient than the System Allocator. Additionally, Native Objects can be allocated outside of Native Regions when using the System Allocator.";

        bool IsOutdated2022Version(CachedSnapshot snapshot) =>
            snapshot.HasGfxResourceReferencesAndAllocators && snapshot.MetaData.UnityVersionMajor == 2022 &&
            (snapshot.MetaData.UnityVersionMinor < 3 || snapshot.MetaData.UnityVersionPatch < 7);

        const string kBuggyGfxSizeReportingWarning = "Outdated 2022.2/3 version. Graphics sizes might be off.";
        const string kBuggyGfxSizeReportingWarningDetails = "You are looking at data from a Unity 2022 version which we know had bugs in the sizes reported for Graphics memory. Some types such as RenderTextures, Cubemap Arrays, Texture2DArray/Texture3D, might have vastly under reported graphics sizes. Please update to at least 2022.3.7f1, but ideally 2022.3.43f1 to get the most accurate picture of graphics memory usage.";
        const string kBuggyGfxSizeReportingWarningDetailsDifferentVersions = "\nYou are comparing snapshot A from version {0} and snapshot B from version {1}. Comparative graphics sizes, and in some cases native sizes, might not be different due to an actual change in memory usage but due to fixed bugs in their reported sizes.";
        const string kBuggyGfxSizeReportingWarningDetailsSameVersion = "\nBoth snapshots are from the same version {0}";

        const string kCaptureFlagsMissing = "{0} were not captured.";

        const string kCaptureFlagsNativeObjects = "Native Objects";
        const string kCaptureFlagsNativeObjectsDetails = "To capture and inspect Native Object memory usage, select the Native Objects option from the capture options or via CaptureFlags.NativeObjects when using the API to take a capture.";

        const string kCaptureFlagsNativeAllocations = "Native Allocations";
        const string kCaptureFlagsNativeAllocationsDetails = "To capture and inspect Native Allocations, select the Native Allocations option from the capture options or via CaptureFlags.NativeAllocations when using the API to take a capture. Also note: Without Native Allocations being captured, no determination can be made regarding whether or not the System Allocator was used.";

        const string kCaptureFlagsManagedObjects = "Managed Objects";
        const string kCaptureFlagsManagedObjectsDetails = "To capture and inspect Managed Object memory usage, select the Managed Objects option from the capture options or via CaptureFlags.ManagedObjects when using the API to take a capture.";

        const string kCaptureFlagsDiff = "Comparing snapshots with different Capture options.";
        const string kCaptureFlagsDiffDetails = "{0} were not captured in snapshot {1}.";
        const string kCaptureFlagsDiffDetailsAllWereCaptured = "All details were captured in snapshot {0}.";

        const string kCrossSessionDiff = "Comparing snapshots from different sessions.";
        const string kUnknownSessionDiff = "Comparing snapshots with an unknown session.";
        const string kCrossSessionDiffDetails = "The Memory Profiler can only compare snapshots at full detail level when both originate from the same (known) session. In all other cases, memory layout, addresses and instance IDs can not be assumed to be comparable.";

        const string kCrossUnityVersionDiff = "Comparing snapshots from different Unity versions.";
        const string kCrossUnityVersionDiffDetail = "Snapshot A was taken from Unity version '{0}', while snapshot B was taken from Unity version '{1}'. Some change in memory usage might be due to the different base Unity versions used while some might be due to changes in the project.";

        const string kEnumerating3 = "{0}, {1} and {2}";
        const string kEnumerating2 = "{0} and {1}";

        /// <summary>
        /// Not All Capture Flags are important to check to raise issues for their presence or absence
        /// </summary>
        const CaptureFlags kCaptureFlagsRelevantForIssueEntries =
            (CaptureFlags.ManagedObjects | CaptureFlags.NativeObjects | CaptureFlags.NativeAllocations);

        // Data
        private readonly CachedSnapshot m_BaseSnapshot;
        private readonly CachedSnapshot m_CompareSnapshot;

        public SnapshotIssuesModelBuilder(CachedSnapshot baseSnapshot, CachedSnapshot compareSnapshot = null)
        {
            m_BaseSnapshot = baseSnapshot;
            m_CompareSnapshot = compareSnapshot;
        }

        /// <summary>
        /// 对应Unity SnapshotIssuesModelBuilder.Build (Line 84-100)
        /// </summary>
        public SnapshotIssuesModel Build()
        {
            var issues = new List<SnapshotIssuesModel.Issue>();

            if (m_CompareSnapshot == null)
                GatherIssuesSingle(issues, m_BaseSnapshot);
            else
                GatherIssuesCompare(issues, m_BaseSnapshot, m_CompareSnapshot);

            // Sort by issue level, reverse order so that higher priority issues are first
            issues.Sort((a, b) => { return -a.IssueLevel.CompareTo(b.IssueLevel); });

            return new SnapshotIssuesModel(issues);
        }

        /// <summary>
        /// 对应Unity SnapshotIssuesModelBuilder.GatherIssuesSingle (Line 102-117)
        /// </summary>
        void GatherIssuesSingle(List<SnapshotIssuesModel.Issue> results, CachedSnapshot snapshot)
        {
            if (snapshot.MetaData.IsEditorCapture)
                AddIssue(results, kEditorHint, kEditorHintDetails, SnapshotIssuesModel.IssueLevel.Warning);

            if (!snapshot.HasTargetAndMemoryInfo)
                AddIssue(results, kOldSnapshotWarning, kOldSnapshotWarningContent,
                    SnapshotIssuesModel.IssueLevel.Warning);

            if (snapshot.NativeMemoryRegions.UsesSystemAllocator)
                AddIssue(results, kSystemAllocatorWarning, kSystemAllocatorWarningDetails,
                    SnapshotIssuesModel.IssueLevel.Warning);

            if (IsOutdated2022Version(snapshot))
                AddIssue(results, kBuggyGfxSizeReportingWarning, kBuggyGfxSizeReportingWarningDetails,
                    SnapshotIssuesModel.IssueLevel.Warning);

            AddCaptureFlagsInfo(results, snapshot);
        }

        /// <summary>
        /// 对应Unity SnapshotIssuesModelBuilder.GatherIssuesCompare (Line 119-204)
        /// </summary>
        void GatherIssuesCompare(List<SnapshotIssuesModel.Issue> results, CachedSnapshot snapshotA,
            CachedSnapshot snapshotB)
        {
            // Check snapshot source - warn if it's Editor
            if (snapshotA.MetaData.IsEditorCapture)
            {
                if (snapshotB.MetaData.IsEditorCapture)
                    AddIssue(results, kEditorHintDiffBoth, kEditorHintDetails,
                        SnapshotIssuesModel.IssueLevel.Warning);
                else
                    AddIssue(results, kEditorHintDiffOne, kEditorHintDiffOneDetails,
                        SnapshotIssuesModel.IssueLevel.Warning);
            }
            else if (snapshotB.MetaData.IsEditorCapture)
                AddIssue(results, kEditorHintDiffOne, kEditorHintDiffOneDetails,
                    SnapshotIssuesModel.IssueLevel.Warning);

            // mask flags to only contain flags relevant for issue entries
            var flagsA = snapshotA.MetaData.CaptureFlags & kCaptureFlagsRelevantForIssueEntries;
            var flagsB = snapshotB.MetaData.CaptureFlags & kCaptureFlagsRelevantForIssueEntries;
            // Compare capture flags
            if (flagsA != flagsB)
            {
                var strBuilder = new StringBuilder();
                var missingSupportedCaptureFlagCountA = CheckMissingCaptureFlags(flagsA,
                    out bool noNativeObjectsA, out bool noNativeAllocationsA, out bool noManagedObjectsA);

                var missingSupportedCaptureFlagCountB = CheckMissingCaptureFlags(flagsB,
                    out bool noNativeObjectsB, out bool noNativeAllocationsB, out bool noManagedObjectsB);

                if (missingSupportedCaptureFlagCountA == 0)
                    strBuilder.AppendFormat(kCaptureFlagsDiffDetailsAllWereCaptured, "A");
                else
                    strBuilder.AppendFormat(kCaptureFlagsDiffDetails,
                        BuildCaptureFlagEnumeration(missingSupportedCaptureFlagCountA, noNativeObjectsA,
                            noNativeAllocationsA, noManagedObjectsA), "A");
                strBuilder.AppendLine();
                if (missingSupportedCaptureFlagCountB == 0)
                    strBuilder.AppendFormat(kCaptureFlagsDiffDetailsAllWereCaptured, "B");
                else
                    strBuilder.AppendFormat(kCaptureFlagsDiffDetails,
                        BuildCaptureFlagEnumeration(missingSupportedCaptureFlagCountB, noNativeObjectsB,
                            noNativeAllocationsB, noManagedObjectsB), "B");
                AddIssue(results, kCaptureFlagsDiff, strBuilder.ToString(), SnapshotIssuesModel.IssueLevel.Warning);
            }
            else
                AddCaptureFlagsInfo(results, snapshotA);

            // Warn if it's not from the same session
            if (snapshotA.MetaData.SessionGUID == 0 || snapshotB.MetaData.SessionGUID == 0)
            {
                AddIssue(results, kUnknownSessionDiff, kCrossSessionDiffDetails,
                    SnapshotIssuesModel.IssueLevel.Info);
            }
            else if (snapshotA.MetaData.SessionGUID != snapshotB.MetaData.SessionGUID)
            {
                AddIssue(results, kCrossSessionDiff, kCrossSessionDiffDetails, SnapshotIssuesModel.IssueLevel.Info);
            }

            // Warn if system allocator is used
            if (snapshotA.NativeMemoryRegions.UsesSystemAllocator || snapshotB.NativeMemoryRegions.UsesSystemAllocator)
            {
                var strBuilder = new StringBuilder();
                if (snapshotA.NativeMemoryRegions.UsesSystemAllocator ==
                    snapshotB.NativeMemoryRegions.UsesSystemAllocator)
                    strBuilder.AppendLine(kSystemAllocatorWarningDetailsDiffBoth);
                else if (snapshotA.NativeMemoryRegions.UsesSystemAllocator)
                    strBuilder.AppendLine(kSystemAllocatorWarningDetailsDiffA);
                else
                    strBuilder.AppendLine(kSystemAllocatorWarningDetailsDiffB);

                strBuilder.Append(kSystemAllocatorWarningDetails);
                AddIssue(results, kSystemAllocatorWarning, strBuilder.ToString(),
                    SnapshotIssuesModel.IssueLevel.Warning);
            }

            // Warn if different Unity versions
            var comparingDifferentUnityVersions = snapshotA.MetaData.UnityVersion != snapshotB.MetaData.UnityVersion;
            if (comparingDifferentUnityVersions)
            {
                var strBuilder = new StringBuilder(kCrossUnityVersionDiffDetail.Length +
                                                    snapshotA.MetaData.UnityVersion.Length +
                                                    snapshotB.MetaData.UnityVersion.Length);
                strBuilder.AppendFormat(kCrossUnityVersionDiffDetail, snapshotA.MetaData.UnityVersion,
                    snapshotB.MetaData.UnityVersion);
                AddIssue(results, kCrossUnityVersionDiff, strBuilder.ToString(), SnapshotIssuesModel.IssueLevel.Info);
            }

            var snapshotAOutdated2022Version = IsOutdated2022Version(snapshotA);
            var snapshotBOutdated2022Version = IsOutdated2022Version(snapshotB);
            if (snapshotAOutdated2022Version || snapshotBOutdated2022Version)
            {
                if (comparingDifferentUnityVersions)
                    AddIssue(results, kBuggyGfxSizeReportingWarning,
                        kBuggyGfxSizeReportingWarningDetails +
                        string.Format(kBuggyGfxSizeReportingWarningDetailsDifferentVersions,
                            snapshotA.MetaData.UnityVersion, snapshotB.MetaData.UnityVersion),
                        SnapshotIssuesModel.IssueLevel.Warning);
                else
                    AddIssue(results, kBuggyGfxSizeReportingWarning,
                        kBuggyGfxSizeReportingWarningDetails +
                        string.Format(kBuggyGfxSizeReportingWarningDetailsSameVersion,
                            snapshotA.MetaData.UnityVersion),
                        SnapshotIssuesModel.IssueLevel.Warning);
            }
        }

        /// <summary>
        /// 对应Unity SnapshotIssuesModelBuilder.AddIssue (Line 206-214)
        /// </summary>
        public void AddIssue(List<SnapshotIssuesModel.Issue> results, string message, string tooltip,
            SnapshotIssuesModel.IssueLevel issueLevel)
        {
            results.Add(new SnapshotIssuesModel.Issue()
            {
                Summary = message,
                IssueLevel = issueLevel,
                Details = tooltip,
            });
        }

        /// <summary>
        /// 对应Unity SnapshotIssuesModelBuilder.AddCaptureFlagsInfo (Line 216-252)
        /// </summary>
        void AddCaptureFlagsInfo(List<SnapshotIssuesModel.Issue> results, CachedSnapshot snapshot)
        {
            // mask flags to only contain flags relevant for issue entries
            var flags = snapshot.MetaData.CaptureFlags & kCaptureFlagsRelevantForIssueEntries;

            var missingSupportedCaptureFlagCount = CheckMissingCaptureFlags(flags, out bool noNativeObjects,
                out bool noNativeAllocations, out bool noManagedObjects);
            if (missingSupportedCaptureFlagCount > 0)
            {
                var detailsStringBuilder = new StringBuilder();
                var issueTitle = string.Format(kCaptureFlagsMissing,
                    BuildCaptureFlagEnumeration(missingSupportedCaptureFlagCount, noNativeObjects,
                        noNativeAllocations, noManagedObjects));
                switch (missingSupportedCaptureFlagCount)
                {
                    case 3:
                        detailsStringBuilder.AppendLine(kCaptureFlagsNativeObjectsDetails);
                        detailsStringBuilder.AppendLine(kCaptureFlagsNativeAllocationsDetails);
                        detailsStringBuilder.Append(kCaptureFlagsManagedObjectsDetails);
                        AddIssue(results, issueTitle, detailsStringBuilder.ToString(),
                            SnapshotIssuesModel.IssueLevel.Info);
                        break;
                    case 2:
                        detailsStringBuilder.AppendLine(noNativeObjects
                            ? kCaptureFlagsNativeObjectsDetails
                            : kCaptureFlagsNativeAllocationsDetails);
                        detailsStringBuilder.Append(noNativeAllocations
                            ? (noNativeObjects ? kCaptureFlagsNativeAllocationsDetails : kCaptureFlagsManagedObjectsDetails)
                            : kCaptureFlagsManagedObjectsDetails);

                        AddIssue(results, issueTitle, detailsStringBuilder.ToString(),
                            SnapshotIssuesModel.IssueLevel.Info);
                        break;
                    case 1:
                        if (noNativeObjects)
                            AddIssue(results, issueTitle, kCaptureFlagsNativeObjectsDetails,
                                SnapshotIssuesModel.IssueLevel.Info);
                        else if (noNativeAllocations)
                            AddIssue(results, issueTitle, kCaptureFlagsNativeAllocationsDetails,
                                SnapshotIssuesModel.IssueLevel.Info);
                        else if (noManagedObjects)
                            AddIssue(results, issueTitle, kCaptureFlagsManagedObjectsDetails,
                                SnapshotIssuesModel.IssueLevel.Info);
                        break;
                    default:
                        break;
                }
            }
        }

        /// <summary>
        /// 对应Unity SnapshotIssuesModelBuilder.CheckMissingCaptureFlags (Line 254-264)
        /// </summary>
        int CheckMissingCaptureFlags(CaptureFlags flags, out bool noNativeObjects, out bool noNativeAllocations,
            out bool noManagedObjects)
        {
            int missingSupportedCaptureFlagCount = 0;
            noNativeObjects = !flags.HasFlag(CaptureFlags.NativeObjects);
            missingSupportedCaptureFlagCount += noNativeObjects ? 1 : 0;
            noNativeAllocations = !flags.HasFlag(CaptureFlags.NativeAllocations);
            missingSupportedCaptureFlagCount += noNativeAllocations ? 1 : 0;
            noManagedObjects = !flags.HasFlag(CaptureFlags.ManagedObjects);
            missingSupportedCaptureFlagCount += noManagedObjects ? 1 : 0;
            return missingSupportedCaptureFlagCount;
        }

        /// <summary>
        /// 对应Unity SnapshotIssuesModelBuilder.BuildCaptureFlagEnumeration (Line 266-288)
        /// </summary>
        string BuildCaptureFlagEnumeration(int missingSupportedCaptureFlagCount, bool noNativeObjects,
            bool noNativeAllocations, bool noManagedObjects)
        {
            switch (missingSupportedCaptureFlagCount)
            {
                case 3:
                    return string.Format(kEnumerating3, kCaptureFlagsNativeObjects, kCaptureFlagsNativeAllocations,
                        kCaptureFlagsManagedObjects);
                case 2:
                    return string.Format(kEnumerating2,
                        noNativeObjects ? kCaptureFlagsNativeObjects : kCaptureFlagsNativeAllocations,
                        noNativeAllocations
                            ? (noNativeObjects ? kCaptureFlagsNativeAllocations : kCaptureFlagsManagedObjects)
                            : kCaptureFlagsManagedObjects);
                case 1:
                    if (noNativeObjects)
                        return kCaptureFlagsNativeObjects;
                    else if (noNativeAllocations)
                        return kCaptureFlagsNativeAllocations;
                    else if (noManagedObjects)
                        return kCaptureFlagsManagedObjects;
                    else
                        return string.Empty; // 不应该到达这里，但返回空字符串而不是抛异常
                case 0:
                    return string.Empty; // 没有缺失的标志
                default:
                    return string.Empty; // 不支持的情况，返回空字符串
            }
        }
    }
}

