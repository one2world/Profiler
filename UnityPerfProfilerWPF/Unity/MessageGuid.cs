using System;
using System.Collections.Generic;

namespace UnityPerfProfilerWPF.Unity;

/// <summary>
/// Unity Message GUIDs - exact port from UnityPerfProfiler
/// </summary>
internal static class MessageGuid
{
    static MessageGuid()
    {
        Guid2Id.Add(new Guid(kProfileStartupInformation), MessageID.kProfileStartupInformation);
        Guid2Id.Add(new Guid(kProfilerSetAutoInstrumentedAssemblies), MessageID.kProfilerSetAutoInstrumentedAssemblies);
        Guid2Id.Add(new Guid(kObjectMemoryProfileSnapshot), MessageID.kObjectMemoryProfileSnapshot);
        Guid2Id.Add(new Guid(kProfileDataMessage), MessageID.kProfileDataMessage);
        Guid2Id.Add(new Guid(kObjectLuaProfileDataMessage), MessageID.kObjectLuaProfileDataMessage);
        Guid2Id.Add(new Guid(kObjectMemoryProfileDataMessage), MessageID.kObjectMemoryProfileDataMessage);
        Guid2Id.Add(new Guid(kMemorySnapshotRequest), MessageID.kMemorySnapshotRequest);
        Guid2Id.Add(new Guid(kMemorySnapshotDataMessage), MessageID.kMemorySnapshotDataMessage);
        Guid2Id.Add(new Guid(kMemorySnapshotDataMessageAbove2019), MessageID.kMemorySnapshotDataMessage);
        Guid2Id.Add(new Guid(kProfilerQueryInstrumentableFunctions), MessageID.kProfilerQueryInstrumentableFunctions);
        Guid2Id.Add(new Guid(kProfilerQueryFunctionCallees), MessageID.kProfilerQueryFunctionCallees);
        Guid2Id.Add(new Guid(kProfilerFunctionsDataMessage), MessageID.kProfilerFunctionsDataMessage);
        Guid2Id.Add(new Guid(kProfilerBeginInstrumentFunction), MessageID.kProfilerBeginInstrumentFunction);
        Guid2Id.Add(new Guid(kProfilerEndInstrumentFunction), MessageID.kProfilerEndInstrumentFunction);
        Guid2Id.Add(new Guid(kLogMessage), MessageID.kLogMessage);
        Guid2Id.Add(new Guid(kCleanLogMessage), MessageID.kCleanLogMessage);
        Guid2Id.Add(new Guid(kFileTransferMessage), MessageID.kFileTransferMessage);
        Guid2Id.Add(new Guid(kFrameDebuggerEditorToPlayer), MessageID.kFrameDebuggerEditorToPlayer);
        Guid2Id.Add(new Guid(kFrameDebuggerPlayerToEditor), MessageID.kFrameDebuggerPlayerToEditor);
        Guid2Id.Add(new Guid(kPingAliveMessage), MessageID.kPingAliveMessage);
        Guid2Id.Add(new Guid(kApplicationQuitMessage), MessageID.kApplicationQuitMessage);
        
        Id2Guid.Add(MessageID.kProfileStartupInformation, kProfileStartupInformation);
        Id2Guid.Add(MessageID.kProfilerSetAutoInstrumentedAssemblies, kProfilerSetAutoInstrumentedAssemblies);
        Id2Guid.Add(MessageID.kObjectMemoryProfileSnapshot, kObjectMemoryProfileSnapshot);
        Id2Guid.Add(MessageID.kMemorySnapshotRequest, kMemorySnapshotRequest);
        Id2Guid.Add(MessageID.kProfileDataMessage, kProfileDataMessage);
    }

    public static MessageID GetIdByGuid(byte[] guid)
    {
        Guid g = new(guid);
        if (Guid2Id.ContainsKey(g))
        {
            return Guid2Id[g];
        }
        return MessageID.kLastMessageID;
    }

    public static byte[]? GetGuidByID(MessageID id)
    {
        if (Id2Guid.ContainsKey(id))
        {
            return Id2Guid[id];
        }
        return null;
    }

    // Unity Message GUIDs - exact from Unity protocol
    public static readonly byte[] kProfileStartupInformation = BinaryUtils.UnityGUID2Bytes("2257466d0e0e47da89826cf04e68135c");
    public static readonly byte[] kProfilerSetAutoInstrumentedAssemblies = BinaryUtils.UnityGUID2Bytes("6cfdfe5ac10d4b79bfe27e8abe06915f");
    public static readonly byte[] kObjectMemoryProfileSnapshot = BinaryUtils.UnityGUID2Bytes("14473694eb0a4963870aaab63efb7507");
    public static readonly byte[] kProfileDataMessage = BinaryUtils.UnityGUID2Bytes("c58d77184f4b4b59b3fffc6f800ae10e");
    public static readonly byte[] kObjectMemoryProfileDataMessage = BinaryUtils.UnityGUID2Bytes("8584ee18ea264718873cd92b109a0761");
    public static readonly byte[] kMemorySnapshotRequest = BinaryUtils.UnityGUID2Bytes("4b9386de901a4ae2bb70084edca4c7f4");
    public static readonly byte[] kMemorySnapshotDataMessage = BinaryUtils.UnityGUID2Bytes("f3a50e1a63f9400f914d9407cd8094fb");
    public static readonly byte[] kMemorySnapshotDataMessageAbove2019 = BinaryUtils.UnityGUID2Bytes("edcadf462a69df81547ff4f30a52530c");
    public static readonly byte[] kMessageSnapshotDataBegin = BinaryUtils.UnityGUID2Bytes("c626f85c1d3492369e59250f015f8129");
    public static readonly byte[] kMessageSnapshotDataEnd = BinaryUtils.UnityGUID2Bytes("80491b35c7645ea780edbe7c1d834138");
    public static readonly byte[] kObjectLuaProfileDataMessage = BinaryUtils.UnityGUID2Bytes("4d7008c6e970447a8c34f3be95f74d66");
    public static readonly byte[] kProfilerQueryInstrumentableFunctions = BinaryUtils.UnityGUID2Bytes("302b3998e168478eb8713b086c7693a9");
    public static readonly byte[] kProfilerQueryFunctionCallees = BinaryUtils.UnityGUID2Bytes("d8f38a5539cc4b608792c273efe6a969");
    public static readonly byte[] kProfilerFunctionsDataMessage = BinaryUtils.UnityGUID2Bytes("e2acb618e8c8465a901eb7b6f667cc41");
    public static readonly byte[] kProfilerBeginInstrumentFunction = BinaryUtils.UnityGUID2Bytes("027723bb8a12495aa4803c27d10c86b8");
    public static readonly byte[] kProfilerEndInstrumentFunction = BinaryUtils.UnityGUID2Bytes("1db84608522147b8bc57e34cd4d036b1");
    public static readonly byte[] kProfilerSetMemoryRecordMode = BinaryUtils.UnityGUID2Bytes("c48d097f8fea463494b8f08b0b55d05a");
    public static readonly byte[] kProfilerSetAudioCaptureFlags = BinaryUtils.UnityGUID2Bytes("1e792ecb5c9f4a8381d0d03528b6ae7b");
    public static readonly byte[] kLogMessage = BinaryUtils.UnityGUID2Bytes("394ada038ba04f26b0011a6cdeb05a62");
    public static readonly byte[] kCleanLogMessage = BinaryUtils.UnityGUID2Bytes("3ded2ddacdf246d8a3f601741741e7a9");
    public static readonly byte[] kFileTransferMessage = BinaryUtils.UnityGUID2Bytes("c2a22f5d7091478ab4d6c163a7573c35");
    public static readonly byte[] kFrameDebuggerEditorToPlayer = BinaryUtils.UnityGUID2Bytes("035c0cae2e03494894aabe3955d4bf43");
    public static readonly byte[] kFrameDebuggerPlayerToEditor = BinaryUtils.UnityGUID2Bytes("8f448ceb744d42ba80a854f56e43b77e");
    public static readonly byte[] kPingAliveMessage = BinaryUtils.UnityGUID2Bytes("fe9b18127f6045c68db230d993d2a210");
    public static readonly byte[] kApplicationQuitMessage = BinaryUtils.UnityGUID2Bytes("38a5d246506546dfaedb6653f6e22b33");

    private static readonly Dictionary<Guid, MessageID> Guid2Id = new();
    private static readonly Dictionary<MessageID, byte[]> Id2Guid = new();
}