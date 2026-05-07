using System.Text.RegularExpressions;
using Docker_Wifi.Helpers;
using Docker_Wifi.Models;

namespace Docker_Wifi.Services;

public interface IAccessPointClientService
{
    Task<List<WifiClient>> GetConnectedClientsAsync(CancellationToken cancellationToken = default);
}

public sealed class AccessPointClientService : IAccessPointClientService
{
    private readonly IWifiShellService _shellService;
    private readonly ILogger<AccessPointClientService> _logger;
    private const string ApInterface = "wlan1";

    public AccessPointClientService(IWifiShellService shellService, ILogger<AccessPointClientService> logger)
    {
        _shellService = shellService;
        _logger = logger;
    }

    public async Task<List<WifiClient>> GetConnectedClientsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Fetching connected clients on {Interface}", ApInterface);

        try
        {
            // Get station information from iw
            var stations = await GetStationInfoAsync(cancellationToken).ConfigureAwait(false);

            // Get IP/MAC mapping from ARP
            var arpEntries = await GetArpEntriesAsync(cancellationToken).ConfigureAwait(false);

            // Merge the data
            var clients = MergeClientData(stations, arpEntries);

            _logger.LogDebug("Found {Count} connected clients on {Interface}", clients.Count, ApInterface);

            return clients;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get connected clients");
            return new List<WifiClient>();
        }
    }

    private async Task<Dictionary<string, StationInfo>> GetStationInfoAsync(CancellationToken cancellationToken)
    {
        var stations = new Dictionary<string, StationInfo>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var result = await _shellService.ExecuteCommandAsync(
                "iw",
                $"dev {ApInterface} station dump",
                TimeSpan.FromSeconds(10),
                cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogWarning("Failed to get station dump: {Error}", result.StdErr);
                return stations;
            }

            var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            StationInfo? currentStation = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // New station entry
                if (trimmedLine.StartsWith("Station ", StringComparison.OrdinalIgnoreCase))
                {
                    var match = Regex.Match(trimmedLine, @"Station\s+([0-9a-fA-F:]{17})", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var mac = match.Groups[1].Value.ToUpperInvariant();
                        currentStation = new StationInfo { MacAddress = mac };
                        stations[mac] = currentStation;
                    }
                }
                else if (currentStation != null)
                {
                    // Parse station properties
                    if (trimmedLine.StartsWith("signal:", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(trimmedLine, @"signal:\s*(-?\d+)");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var signal))
                        {
                            currentStation.SignalDbm = signal;
                        }
                    }
                    else if (trimmedLine.StartsWith("tx bitrate:", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(trimmedLine, @"tx bitrate:\s*(.+)");
                        if (match.Success)
                        {
                            currentStation.TxBitrate = match.Groups[1].Value.Trim();
                        }
                    }
                    else if (trimmedLine.StartsWith("rx bitrate:", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(trimmedLine, @"rx bitrate:\s*(.+)");
                        if (match.Success)
                        {
                            currentStation.RxBitrate = match.Groups[1].Value.Trim();
                        }
                    }
                    else if (trimmedLine.StartsWith("connected time:", StringComparison.OrdinalIgnoreCase))
                    {
                        var match = Regex.Match(trimmedLine, @"connected time:\s*(\d+)\s*seconds");
                        if (match.Success && int.TryParse(match.Groups[1].Value, out var seconds))
                        {
                            currentStation.ConnectedSeconds = seconds;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing station dump");
        }

        return stations;
    }

    private async Task<Dictionary<string, ArpEntry>> GetArpEntriesAsync(CancellationToken cancellationToken)
    {
        var arpEntries = new Dictionary<string, ArpEntry>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Try ip neigh first (modern approach)
            var result = await _shellService.ExecuteCommandAsync(
                "ip",
                "neigh show",
                TimeSpan.FromSeconds(10),
                cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                ParseIpNeighOutput(result.StdOut, arpEntries);
            }
            else
            {
                // Fallback to arp command
                result = await _shellService.ExecuteCommandAsync(
                    "arp",
                    "-n",
                    TimeSpan.FromSeconds(10),
                    cancellationToken).ConfigureAwait(false);

                if (result.Success)
                {
                    ParseArpOutput(result.StdOut, arpEntries);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting ARP entries");
        }

        return arpEntries;
    }

    private void ParseIpNeighOutput(string output, Dictionary<string, ArpEntry> entries)
    {
        // Example: 192.168.4.2 dev wlan1 lladdr aa:bb:cc:dd:ee:ff REACHABLE
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (!line.Contains(ApInterface, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = Regex.Match(line, @"(\d+\.\d+\.\d+\.\d+)\s+.*lladdr\s+([0-9a-fA-F:]{17})");
            if (match.Success)
            {
                var ip = match.Groups[1].Value;
                var mac = match.Groups[2].Value.ToUpperInvariant();

                entries[mac] = new ArpEntry
                {
                    IpAddress = ip,
                    MacAddress = mac
                };
            }
        }
    }

    private void ParseArpOutput(string output, Dictionary<string, ArpEntry> entries)
    {
        // Example: 192.168.4.2    ether   aa:bb:cc:dd:ee:ff   C   wlan1
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            if (!line.Contains(ApInterface, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var parts = Regex.Split(line, @"\s+");
            if (parts.Length >= 3)
            {
                var ip = parts[0];
                var mac = parts[2].ToUpperInvariant();

                if (Regex.IsMatch(mac, @"^[0-9A-F:]{17}$"))
                {
                    entries[mac] = new ArpEntry
                    {
                        IpAddress = ip,
                        MacAddress = mac
                    };
                }
            }
        }
    }

    private List<WifiClient> MergeClientData(
        Dictionary<string, StationInfo> stations,
        Dictionary<string, ArpEntry> arpEntries)
    {
        var clients = new List<WifiClient>();

        foreach (var station in stations.Values)
        {
            arpEntries.TryGetValue(station.MacAddress, out var arpEntry);

            clients.Add(new WifiClient
            {
                MacAddress = station.MacAddress,
                IpAddress = arpEntry?.IpAddress,
                Hostname = arpEntry?.Hostname,
                SignalDbm = station.SignalDbm,
                TxBitrate = station.TxBitrate,
                RxBitrate = station.RxBitrate,
                ConnectedDuration = station.ConnectedSeconds.HasValue
                    ? TimeSpan.FromSeconds(station.ConnectedSeconds.Value)
                    : null
            });
        }

        return clients;
    }

    private sealed class StationInfo
    {
        public required string MacAddress { get; init; }
        public int? SignalDbm { get; set; }
        public string? TxBitrate { get; set; }
        public string? RxBitrate { get; set; }
        public int? ConnectedSeconds { get; set; }
    }

    private sealed class ArpEntry
    {
        public required string IpAddress { get; init; }
        public required string MacAddress { get; init; }
        public string? Hostname { get; set; }
    }
}

public sealed class MockAccessPointClientService : IAccessPointClientService
{
    private readonly ILogger<MockAccessPointClientService> _logger;
    private readonly Random _random = new();

    public MockAccessPointClientService(ILogger<MockAccessPointClientService> logger)
    {
        _logger = logger;
    }

    public Task<List<WifiClient>> GetConnectedClientsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Mock: Fetching connected clients");

        var clients = new List<WifiClient>
        {
            new()
            {
                MacAddress = "AA:BB:CC:DD:EE:01",
                IpAddress = "192.168.4.2",
                Hostname = "smartphone-1",
                SignalDbm = -45 + _random.Next(-10, 10),
                TxBitrate = "72.2 MBit/s",
                RxBitrate = "65.0 MBit/s",
                ConnectedDuration = TimeSpan.FromMinutes(45 + _random.Next(0, 60))
            },
            new()
            {
                MacAddress = "AA:BB:CC:DD:EE:02",
                IpAddress = "192.168.4.3",
                Hostname = "laptop-office",
                SignalDbm = -52 + _random.Next(-10, 10),
                TxBitrate = "144.4 MBit/s",
                RxBitrate = "130.0 MBit/s",
                ConnectedDuration = TimeSpan.FromHours(2 + _random.Next(0, 5))
            },
            new()
            {
                MacAddress = "AA:BB:CC:DD:EE:03",
                IpAddress = "192.168.4.4",
                Hostname = null,
                SignalDbm = -68 + _random.Next(-10, 10),
                TxBitrate = "36.0 MBit/s",
                RxBitrate = "24.0 MBit/s",
                ConnectedDuration = TimeSpan.FromMinutes(15 + _random.Next(0, 30))
            }
        };

        return Task.FromResult(clients);
    }
}
