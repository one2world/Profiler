namespace Unity.MemoryProfiler.Editor.UIContentData
{
    internal static class TextContent
    {
        public const string InvalidObjectPleaseReportABugMessage = "please report a bug via the Window Menu Bar option 'Help > Report a Bug'. Please attach this snapshot, info on how to find this object in the snapshot, and a project to reproduce this with.";
        public const string InvalidObjectPleaseReportABugMessageShort = "Failed to read Object, please report a bug via 'Help > Report a Bug'.";
        public const string LeakedManagedShellHint = "Managed shell retained after native object was collected";

        public const string InstanceIdPingingOnlyWorksInNewerUnityVersions = "Pinging objects based on their Instance ID does not work in this Unity version. To enable that functionality, please update your Unity installation.";
        public const string InstanceIdPingingOnlyWorksInSameSessionMessage = "Pinging objects only works for snapshots taken in the current editor session, as it relies on instance IDs. Current Editor Session ID:{0}, Snapshot Session ID: {1}";

        public const string SearchInSceneButton = "Search Scene";
        public const string SearchInProjectButton = "Search Project";
        public const string SearchButtonCantSearch = "Search";
    }
}

