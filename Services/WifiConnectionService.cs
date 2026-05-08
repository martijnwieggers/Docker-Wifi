using Docker_Wifi.Exceptions;
using Docker_Wifi.Helpers;
using Docker_Wifi.Models;

namespace Docker_Wifi.Services;

public interface IWifiConnectionService
{
    Task<WifiConnectionResult> ConnectAsync(WifiConnectionRequest request, CancellationToken cancellationToken = default);
    Task<WifiConnectionResult> DisconnectAsync(CancellationToken cancellationToken = default);
    Task<string?> GetCurrentConnectionAsync(CancellationToken cancellationToken = default);
    Task<string?> GetWlan0IpAddressAsync(CancellationToken cancellationToken = default);
}

public sealed class WifiConnectionService : IWifiConnectionService
{
    private readonly IWifiShellService _shellService;
    private readonly ILogger<WifiConnectionService> _logger;
    private const string WlanInterface = "wlan0";

    public WifiConnectionService(IWifiShellService shellService, ILogger<WifiConnectionService> logger)
    {
        _shellService = shellService;
        _logger = logger;
    }

    public async Task<WifiConnectionResult> ConnectAsync(
        WifiConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to connect to WiFi network: {SSID}", request.SSID);

        if (string.IsNullOrWhiteSpace(request.SSID))
        {
            return new WifiConnectionResult
            {
                Success = false,
                Message = "SSID cannot be empty",
                ErrorCode = "INVALID_SSID"
            };
        }

        try
        {
            var escapedSsid = CommandLineHelper.EscapeArgument(request.SSID);
            var escapedPassword = CommandLineHelper.EscapeArgument(request.Password);

            string arguments;
            if (string.IsNullOrEmpty(request.Password))
            {
                // Open network
                arguments = $"device wifi connect {escapedSsid} ifname {WlanInterface}";
                _logger.LogDebug("Connecting to open network");
            }
            else
            {
                // Secured network
                arguments = $"device wifi connect {escapedSsid} password {escapedPassword} ifname {WlanInterface}";
                _logger.LogDebug("Connecting to secured network");
            }

            var result = await _shellService.ExecuteCommandAsync(
                "nmcli",
                arguments,
                TimeSpan.FromSeconds(30),
                cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.LogInformation("Successfully connected to WiFi network: {SSID}", request.SSID);

                // Verify connection
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                var currentConnection = await GetCurrentConnectionAsync(cancellationToken).ConfigureAwait(false);

                if (currentConnection?.Equals(request.SSID, StringComparison.OrdinalIgnoreCase) == true)
                {
                    return new WifiConnectionResult
                    {
                        Success = true,
                        Message = $"Successfully connected to {request.SSID}"
                    };
                }
                else
                {
                    _logger.LogWarning("Connected but verification failed. Expected: {Expected}, Got: {Actual}",
                        request.SSID, currentConnection);

                    return new WifiConnectionResult
                    {
                        Success = true,
                        Message = $"Connected to {request.SSID} but verification uncertain",
                        RequiresReconnect = false
                    };
                }
            }
            else
            {
                var errorMessage = ParseConnectionError(result.StdErr);
                _logger.LogWarning("Failed to connect to {SSID}: {Error}", request.SSID, errorMessage);

                return new WifiConnectionResult
                {
                    Success = false,
                    Message = errorMessage,
                    ErrorCode = DetermineErrorCode(result.StdErr)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while connecting to WiFi network: {SSID}", request.SSID);

            return new WifiConnectionResult
            {
                Success = false,
                Message = $"Connection failed: {ex.Message}",
                ErrorCode = "EXCEPTION"
            };
        }
    }

    public async Task<WifiConnectionResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Disconnecting from WiFi network on {Interface}", WlanInterface);

        try
        {
            var result = await _shellService.ExecuteCommandAsync(
                "nmcli",
                $"device disconnect {WlanInterface}",
                TimeSpan.FromSeconds(15),
                cancellationToken).ConfigureAwait(false);

            if (result.Success)
            {
                _logger.LogInformation("Successfully disconnected from WiFi");

                return new WifiConnectionResult
                {
                    Success = true,
                    Message = "Successfully disconnected"
                };
            }
            else
            {
                return new WifiConnectionResult
                {
                    Success = false,
                    Message = $"Failed to disconnect: {result.StdErr}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception while disconnecting from WiFi");

            return new WifiConnectionResult
            {
                Success = false,
                Message = $"Disconnect failed: {ex.Message}",
                ErrorCode = "EXCEPTION"
            };
        }
    }

    public async Task<string?> GetCurrentConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _shellService.ExecuteCommandAsync(
                "nmcli",
                $"-t -f GENERAL.CONNECTION device show {WlanInterface}",
                TimeSpan.FromSeconds(10),
                cancellationToken).ConfigureAwait(false);

            if (!result.Success)
            {
                return null;
            }

            var lines = result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("GENERAL.CONNECTION:", StringComparison.OrdinalIgnoreCase))
                {
                    var connection = line.Substring("GENERAL.CONNECTION:".Length).Trim();

                    if (string.IsNullOrEmpty(connection) || connection == "--")
                    {
                        return null;
                    }

                    return connection;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get current connection");
            return null;
        }
    }

    public async Task<string?> GetWlan0IpAddressAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _shellService.ExecuteCommandAsync(
                "nmcli",
                $"-t -f IP4.ADDRESS device show {WlanInterface}",
                TimeSpan.FromSeconds(10),
                cancellationToken).ConfigureAwait(false);

            if (!result.Success)
                return null;

            foreach (var line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.StartsWith("IP4.ADDRESS", StringComparison.OrdinalIgnoreCase))
                {
                    var address = line[(line.IndexOf(':') + 1)..].Trim();
                    if (string.IsNullOrEmpty(address) || address == "--")
                        return null;
                    var slash = address.IndexOf('/');
                    return slash >= 0 ? address[..slash] : address;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get wlan0 IP address");
            return null;
        }
    }

    private static string ParseConnectionError(string stderr)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return "Connection failed";
        }

        if (stderr.Contains("Secrets were required", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("wrong password", StringComparison.OrdinalIgnoreCase))
        {
            return "Invalid password";
        }

        if (stderr.Contains("No network with SSID", StringComparison.OrdinalIgnoreCase))
        {
            return "Network not found";
        }

        if (stderr.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "Connection timeout";
        }

        return stderr.Length > 100 ? stderr.Substring(0, 100) + "..." : stderr;
    }

    private static string DetermineErrorCode(string stderr)
    {
        if (stderr.Contains("Secrets were required", StringComparison.OrdinalIgnoreCase) ||
            stderr.Contains("wrong password", StringComparison.OrdinalIgnoreCase))
        {
            return "INVALID_PASSWORD";
        }

        if (stderr.Contains("No network with SSID", StringComparison.OrdinalIgnoreCase))
        {
            return "NETWORK_NOT_FOUND";
        }

        if (stderr.Contains("timeout", StringComparison.OrdinalIgnoreCase))
        {
            return "TIMEOUT";
        }

        return "UNKNOWN";
    }
}

public sealed class MockWifiConnectionService : IWifiConnectionService
{
    private readonly ILogger<MockWifiConnectionService> _logger;
    private string? _currentConnection = "Home_Network_5G";

    public MockWifiConnectionService(ILogger<MockWifiConnectionService> logger)
    {
        _logger = logger;
    }

    public async Task<WifiConnectionResult> ConnectAsync(
        WifiConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock: Connecting to {SSID}", request.SSID);

        await Task.Delay(2000, cancellationToken).ConfigureAwait(false);

        // Simulate occasional failures
        if (request.SSID.Contains("Secure", StringComparison.OrdinalIgnoreCase) && 
            request.Password.Length < 8)
        {
            return new WifiConnectionResult
            {
                Success = false,
                Message = "Invalid password",
                ErrorCode = "INVALID_PASSWORD"
            };
        }

        _currentConnection = request.SSID;

        return new WifiConnectionResult
        {
            Success = true,
            Message = $"Successfully connected to {request.SSID}"
        };
    }

    public async Task<WifiConnectionResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock: Disconnecting from WiFi");

        await Task.Delay(1000, cancellationToken).ConfigureAwait(false);

        _currentConnection = null;

        return new WifiConnectionResult
        {
            Success = true,
            Message = "Successfully disconnected"
        };
    }

    public Task<string?> GetCurrentConnectionAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_currentConnection);
    }

    public Task<string?> GetWlan0IpAddressAsync(CancellationToken cancellationToken = default)
    {
        var ip = _currentConnection != null ? "192.168.1.42" : null;
        return Task.FromResult<string?>(ip);
    }
}
