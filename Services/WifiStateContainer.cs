using Docker_Wifi.Models;

namespace Docker_Wifi.Services;

public sealed class WifiStateContainer
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private WifiState _state = new();

    public event Action? OnStateChanged;

    public WifiState State
    {
        get
        {
            _lock.Wait();
            try
            {
                return _state;
            }
            finally
            {
                _lock.Release();
            }
        }
    }

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

            NotifyStateChanged();
        }
        finally
        {
            _lock.Release();
        }
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

            NotifyStateChanged();
        }
        finally
        {
            _lock.Release();
        }
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

            NotifyStateChanged();
        }
        finally
        {
            _lock.Release();
        }
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

            NotifyStateChanged();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void NotifyStateChanged()
    {
        OnStateChanged?.Invoke();
    }
}
