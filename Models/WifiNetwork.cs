namespace Docker_Wifi.Models;

public sealed class WifiNetwork
{
    public required string SSID { get; init; }
    public int SignalStrength { get; init; }
    public required string Security { get; init; }
    public int Channel { get; init; }
    public bool IsConnected { get; init; }
}
