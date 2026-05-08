using Docker_Wifi.Services;

namespace Docker_Wifi.Background;

public sealed class WifiMonitoringBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WifiMonitoringBackgroundService> _logger;

    private static readonly TimeSpan ClientsRefreshInterval = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan NetworksRefreshInterval = TimeSpan.FromSeconds(15);

    public WifiMonitoringBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<WifiMonitoringBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("WiFi Monitoring Background Service started");

        // Wait a bit before starting to allow app to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

        // Start both monitoring tasks
        var clientsTask = MonitorClientsAsync(stoppingToken);
        var networksTask = MonitorNetworksAsync(stoppingToken);

        try
        {
            await Task.WhenAll(clientsTask, networksTask).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("WiFi Monitoring Background Service stopping");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WiFi Monitoring Background Service failed");
            throw;
        }
    }

    private async Task MonitorClientsAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting AP clients monitoring (interval: {Interval}s)", 
            ClientsRefreshInterval.TotalSeconds);

        using var timer = new PeriodicTimer(ClientsRefreshInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshClientsAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error refreshing AP clients");
            }

            try
            {
                await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("AP clients monitoring stopped");
    }

    private async Task MonitorNetworksAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting WiFi networks monitoring (interval: {Interval}s)", 
            NetworksRefreshInterval.TotalSeconds);

        using var timer = new PeriodicTimer(NetworksRefreshInterval);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RefreshNetworksAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error refreshing WiFi networks");
            }

            try
            {
                await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("WiFi networks monitoring stopped");
    }

    private async Task RefreshClientsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var clientService = scope.ServiceProvider.GetRequiredService<IAccessPointClientService>();
        var stateContainer = scope.ServiceProvider.GetRequiredService<WifiStateContainer>();

        var clients = await clientService.GetConnectedClientsAsync(cancellationToken).ConfigureAwait(false);
        await stateContainer.UpdateClientsAsync(clients).ConfigureAwait(false);

        _logger.LogDebug("Refreshed AP clients: {Count} clients connected", clients.Count);
    }

    private async Task RefreshNetworksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var scannerService = scope.ServiceProvider.GetRequiredService<IWifiScannerService>();
        var connectionService = scope.ServiceProvider.GetRequiredService<IWifiConnectionService>();
        var stateContainer = scope.ServiceProvider.GetRequiredService<WifiStateContainer>();

        try
        {
            await stateContainer.SetScanningAsync(true).ConfigureAwait(false);

            var networks = await scannerService.ScanNetworksAsync(cancellationToken).ConfigureAwait(false);
            var ipAddress = await connectionService.GetWlan0IpAddressAsync(cancellationToken).ConfigureAwait(false);

            await stateContainer.UpdateNetworksAsync(networks, false).ConfigureAwait(false);
            await stateContainer.UpdateWlan0IpAsync(ipAddress).ConfigureAwait(false);

            _logger.LogDebug("Refreshed WiFi networks: {Count} networks found, wlan0 IP: {IP}",
                networks.Count, ipAddress ?? "none");
        }
        finally
        {
            await stateContainer.SetScanningAsync(false).ConfigureAwait(false);
        }
    }
}
