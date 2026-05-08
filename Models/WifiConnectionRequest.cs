namespace Docker_Wifi.Models;

public sealed class WifiConnectionRequest
{
    public required string SSID { get; init; }
    public string Password { get; init; } = string.Empty;
    public bool IsEnterprise { get; init; }
    public string Username { get; init; } = string.Empty;
    public string AnonymousIdentity { get; init; } = string.Empty;
}
