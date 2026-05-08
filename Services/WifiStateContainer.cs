using Docker_Wifi.Models;

namespace Docker_Wifi.Services;

public sealed class WifiStateContainer
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile WifiState _state = new();

    public event Action? OnStateChanged;

    // WifiState is an immutable record; reference reads are atomic in .NET,
    // so no lock is needed here — avoids deadlock when callbacks read State
    // while an Update method holds the lock.
    public WifiState State => _state;

    public async Task UpdateNetworksAsync(List<WifiNetwork> networks, bool isScanning = false)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _state = _state with
            {
                Networks = networks,
                LastUpdated = DateTime.UtcNow,
                IsScanning = isScanning
            };
        }
        finally
        {
            _lock.Release();
        }
        NotifyStateChanged();
    }

    public async Task UpdateClientsAsync(List<WifiClient> clients)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _state = _state with
            {
                Clients = clients,
                LastUpdated = DateTime.UtcNow
            };
        }
        finally
        {
            _lock.Release();
        }
        NotifyStateChanged();
    }

    public async Task SetScanningAsync(bool isScanning)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _state = _state with
            {
                IsScanning = isScanning
            };
        }
        finally
        {
            _lock.Release();
        }
        NotifyStateChanged();
    }

    public async Task SetConnectingAsync(bool isConnecting)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _state = _state with
            {
                IsConnecting = isConnecting
            };
        }
        finally
        {
            _lock.Release();
        }
        NotifyStateChanged();
    }

    public async Task UpdateWlan0IpAsync(string? ipAddress)
    {
        await _lock.WaitAsync().ConfigureAwait(false);
        try
        {
            _state = _state with { Wlan0IpAddress = ipAddress };
        }
        finally
        {
            _lock.Release();
        }
        NotifyStateChanged();
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
