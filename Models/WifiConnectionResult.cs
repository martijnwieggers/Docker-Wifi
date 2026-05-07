namespace Docker_Wifi.Models;

public sealed class WifiConnectionResult
{
    public bool Success { get; init; }
    public required string Message { get; init; }
    public bool RequiresReconnect { get; init; }
    public string? ErrorCode { get; init; }
}
