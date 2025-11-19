namespace UnityPerfProfilerWPF.Unity;

/// <summary>
/// Unity Profiler Message IDs - exact port from UnityPerfProfiler
/// </summary>
internal enum MessageID
{
    kProfileDataMessage = 32,
    kProfileStartupInformation,
    kObjectMemoryProfileSnapshot = 40,
    kObjectMemoryProfileDataMessage,
    kMemorySnapshotRequest,
    kMemorySnapshotDataMessage,
    kProfilerQueryInstrumentableFunctions = 50,
    kProfilerQueryFunctionCallees,
    kProfilerFunctionsDataMessage,
    kProfilerBeginInstrumentFunction,
    kProfilerEndInstrumentFunction,
    kProfilerSetAutoInstrumentedAssemblies,
    kObjectLuaProfileDataMessage = 99,
    kLogMessage,
    kCleanLogMessage,
    kFileTransferMessage = 200,
    kCaptureHeaphshotMessage = 202,
    kFrameDebuggerEditorToPlayer,
    kFrameDebuggerPlayerToEditor,
    kPingAliveMessage = 300,
    kApplicationQuitMessage,
    kLastMessageID
}