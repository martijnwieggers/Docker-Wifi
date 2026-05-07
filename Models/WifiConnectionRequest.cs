namespace Docker_Wifi.Models;

public sealed class WifiConnectionRequest
{
    public required string SSID { get; init; }
    public required string Password { get; init; }
}
