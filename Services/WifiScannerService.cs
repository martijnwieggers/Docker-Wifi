using Docker_Wifi.Exceptions;
using Docker_Wifi.Helpers;
using Docker_Wifi.Models;

namespace Docker_Wifi.Services;

public interface IWifiScannerService
{
    Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken cancellationToken = default);
}

public sealed class WifiScannerService : IWifiScannerService
{
    private readonly IWifiShellService _shellService;
    private readonly ILogger<WifiScannerService> _logger;
    private const string WlanInterface = "wlan0";

    public WifiScannerService(IWifiShellService shellService, ILogger<WifiScannerService> logger)
    {
        _shellService = shellService;
        _logger = logger;
    }

    public async Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Scanning WiFi networks on {Interface}", WlanInterface);

        try
        {
            // Request rescan first to get fresh data
            await _shellService.ExecuteCommandAsync(
                "nmcli",
                $"device wifi rescan ifname {WlanInterface}",
                TimeSpan.FromSeconds(10),
                cancellationToken).ConfigureAwait(false);

            // Small delay to allow scan to complete
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

            // Get the scan results
            var result = await _shellService.ExecuteCommandAsync(
                "nmcli",
                $"-t -f SSID,SIGNAL,SECURITY,CHAN,IN-USE device wifi list ifname {WlanInterface}",
                TimeSpan.FromSeconds(15),
                cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                throw new WifiScanException($"nmcli scan failed: {result.StdErr}");
            }

            var networks = ParseScanOutput(result.StdOut);

            _logger.LogInformation("Found {Count} WiFi networks on {Interface}", networks.Count, WlanInterface);

            return networks;
        }
        catch (Exception ex) when (ex is not WifiScanException)
        {
            _logger.LogError(ex, "Failed to scan WiFi networks");
            throw new WifiScanException("Failed to scan WiFi networks", ex);
        }
    }

    private List<WifiNetwork> ParseScanOutput(string output)
    {
        var networks = new List<WifiNetwork>();
        var seenSsids = new HashSet<string>();

        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            try
            {
                var parts = line.Split(':', StringSplitOptions.None);

                if (parts.Length < 5)
                {
                    continue;
                }

                var ssid = parts[0].Trim();

                // Skip empty SSIDs (hidden networks)
                if (string.IsNullOrWhiteSpace(ssid))
                {
                    continue;
                }

                // Skip duplicates (take first occurrence which is usually strongest)
                if (seenSsids.Contains(ssid))
                {
                    continue;
                }

                if (!int.TryParse(parts[1].Trim(), out var signal))
                {
                    signal = 0;
                }

                var security = parts[2].Trim();
                if (string.IsNullOrEmpty(security))
                {
                    security = "Open";
                }

                if (!int.TryParse(parts[3].Trim(), out var channel))
                {
                    channel = 0;
                }

                var isConnected = parts[4].Trim() == "*";

                networks.Add(new WifiNetwork
                {
                    SSID = ssid,
                    SignalStrength = signal,
                    Security = security,
                    Channel = channel,
                    IsConnected = isConnected
                });

                seenSsids.Add(ssid);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse WiFi network line: {Line}", line);
            }
        }

        // Sort by signal strength descending
        return networks.OrderByDescending(n => n.SignalStrength).ToList();
    }
}

public sealed class MockWifiScannerService : IWifiScannerService
{
    private readonly ILogger<MockWifiScannerService> _logger;
    private readonly Random _random = new();

    public MockWifiScannerService(ILogger<MockWifiScannerService> logger)
    {
        _logger = logger;
    }

    public Task<List<WifiNetwork>> ScanNetworksAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock: Scanning WiFi networks");

        var networks = new List<WifiNetwork>
        {
            new() { SSID = "Home_Network_5G", SignalStrength = 85 + _random.Next(-5, 5), Security = "WPA2", Channel = 36, IsConnected = true },
            new() { SSID = "Home_Network_2.4G", SignalStrength = 72 + _random.Next(-5, 5), Security = "WPA2", Channel = 6, IsConnected = false },
            new() { SSID = "Neighbor_WiFi", SignalStrength = 45 + _random.Next(-5, 5), Security = "WPA2/WPA3", Channel = 11, IsConnected = false },
            new() { SSID = "Guest_Network", SignalStrength = 62 + _random.Next(-5, 5), Security = "Open", Channel = 1, IsConnected = false },
            new() { SSID = "Office_Secure", SignalStrength = 38 + _random.Next(-5, 5), Security = "WPA3", Channel = 48, IsConnected = false }
        };

        return Task.FromResult(networks.OrderByDescending(n => n.SignalStrength).ToList());
    }
}
