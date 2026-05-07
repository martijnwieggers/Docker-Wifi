namespace Docker_Wifi.Models;

public sealed class WifiClient
{
    public required string MacAddress { get; init; }
    public string? IpAddress { get; init; }
    public string? Hostname { get; init; }
    public int? SignalDbm { get; init; }
    public string? TxBitrate { get; init; }
    public string? RxBitrate { get; init; }
    public TimeSpan? ConnectedDuration { get; init; }
}
