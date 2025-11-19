using UnityPerfProfilerWPF.Utils;

namespace UnityPerfProfilerWPF.Network;

public class MessageManagerConfig
{
    public string ServerHost { get; set; } = "";
    public int ServerPort { get; set; }
    public MessageMeter? Meter { get; set; }
    public bool BusyMode { get; set; } = false;
    public TransportType TransportType { get; set; } = TransportType.Tcp;
}

public enum TransportType
{
    Tcp,
    Kcp
}
