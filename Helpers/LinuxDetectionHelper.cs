namespace Docker_Wifi.Helpers;

public static class LinuxDetectionHelper
{
    private static readonly Lazy<bool> _isLinux = new(() => OperatingSystem.IsLinux());
    private static readonly Lazy<bool> _isWindows = new(() => OperatingSystem.IsWindows());

    public static bool IsLinux => _isLinux.Value;
    public static bool IsWindows => _isWindows.Value;
    public static bool IsDockerContainer => File.Exists("/.dockerenv");

    public static string GetPlatformInfo()
    {
        var platform = IsLinux ? "Linux" : IsWindows ? "Windows" : "Unknown";
        var container = IsDockerContainer ? " (Docker)" : "";
        return $"{platform}{container}";
    }
}
