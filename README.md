# WiFi Management - Blazor Server Application

Production-ready Blazor Server application for managing WiFi on Raspberry Pi running Docker.

## Features

- 📡 **WiFi Network Scanning** - Scan and display available WiFi networks on wlan0
- 🔌 **WiFi Connection Management** - Connect to WiFi networks via wlan0
- 👥 **Connected Clients Monitoring** - View clients connected to wlan1 Access Point
- 🔄 **Real-time Updates** - Automatic background refresh of network status
- 🐳 **Docker Support** - Full Docker support for Raspberry Pi ARM64
- 💻 **Development Mode** - Mock implementations for Windows development

## Architecture

### Network Interfaces

- **wlan0**: WiFi client interface (scanning and connecting)
- **wlan1**: Access Point interface (read-only, monitoring clients)

⚠️ **IMPORTANT**: wlan1 is NEVER modified by this application. It only monitors connected clients.

### Technology Stack

- .NET 8
- Blazor Server
- C# 12 with nullable reference types
- Bootstrap 5
- NetworkManager (nmcli)
- Linux shell utilities (iw, ip, arp)

### Project Structure

```
Docker-Wifi/
├── Background/          # Background services
├── Components/          # Blazor components and pages
│   ├── Pages/          # Razor pages
│   └── Layout/         # Layout components
├── Exceptions/         # Custom exceptions
├── Helpers/            # Utility helpers
├── Models/             # Data models
├── Services/           # Business logic services
├── Dockerfile          # Production Docker image
└── docker-compose.yaml # Docker Compose configuration
```

## Requirements

### Production (Raspberry Pi)

- Raspberry Pi 5 (ARM64)
- Raspberry Pi OS Lite (Bookworm)
- Docker & Docker Compose
- NetworkManager installed
- wlan0 and wlan1 interfaces configured

### Development (Windows)

- .NET 8 SDK
- Visual Studio 2022 or VS Code

## Installation

### Docker Deployment (Raspberry Pi)

1. **Clone the repository**
```bash
git clone <repository-url>
cd Docker-Wifi
```

2. **Build the Docker image**
```bash
docker compose build
```

3. **Start the application**
```bash
docker compose up -d
```

4. **Access the application**
```
http://<raspberry-pi-ip>:8080
```

### Local Development (Windows)

1. **Clone the repository**
```bash
git clone <repository-url>
cd Docker-Wifi
```

2. **Run the application**
```bash
dotnet run
```

3. **Access the application**
```
https://localhost:5001
```

The application automatically uses mock services on Windows for development.

## Configuration

### appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Docker_Wifi": "Information"
    }
  },
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:8080"
      }
    }
  }
}
```

### Environment Variables (Docker)

- `ASPNETCORE_ENVIRONMENT`: Production/Development
- `ASPNETCORE_URLS`: HTTP listening URL
- `TZ`: Timezone (e.g., Europe/Amsterdam)

## Usage

### WiFi Networks Page

- View all available WiFi networks on wlan0
- See signal strength, security type, and channel
- Connect to networks with password dialog
- Real-time connection status

### Connected Clients Page

- View all clients connected to wlan1 AP
- See MAC addresses, IP addresses, hostnames
- Monitor signal strength (dBm)
- View TX/RX bitrates
- See connection duration

### Background Services

The application automatically:
- Scans WiFi networks every 15 seconds
- Refreshes connected clients every 10 seconds
- Updates the UI in real-time

## Security

### Best Practices Implemented

✅ No hardcoded credentials
✅ No shell injection vulnerabilities
✅ Safe command execution (UseShellExecute=false)
✅ Password sanitization in logs
✅ Proper argument escaping
✅ Structured logging

### Network Safety

- wlan1 is **read-only** (never modified)
- Only wlan0 is used for scanning and connecting
- No modifications to eth interfaces
- nmcli manages connection profiles securely

## Troubleshooting

### Docker Container Issues

**Check container logs:**
```bash
docker logs wifi-manager
```

**Restart container:**
```bash
docker compose restart
```

**Rebuild container:**
```bash
docker compose down
docker compose build --no-cache
docker compose up -d
```

### Permission Issues

Ensure the container has sufficient privileges:
- `network_mode: host` is required
- `privileged: true` is required for network management

### NetworkManager Issues

**Check if NetworkManager is running:**
```bash
systemctl status NetworkManager
```

**Restart NetworkManager:**
```bash
systemctl restart NetworkManager
```

### Interface Issues

**Check interface status:**
```bash
ip link show wlan0
ip link show wlan1
```

**Verify wlan0 is managed by NetworkManager:**
```bash
nmcli device status
```

## Development

### Adding Features

1. Add models in `Models/`
2. Implement services in `Services/`
3. Create Blazor pages in `Components/Pages/`
4. Register services in `Program.cs`

### Testing

**Build the project:**
```bash
dotnet build
```

**Run tests (when added):**
```bash
dotnet test
```

### Code Style

- File-scoped namespaces
- Nullable reference types enabled
- C# 12 features
- Async/await everywhere
- Structured logging
- Dependency injection

## Performance

### Resource Usage

- CPU: ~10-20% average on Raspberry Pi 5
- Memory: ~256MB typical usage
- Network: Minimal (periodic scans only)

### Optimization Tips

- Adjust scan intervals in `WifiMonitoringBackgroundService`
- Reduce logging in production
- Use connection pooling where applicable

## License

[Add your license here]

## Contributing

[Add contributing guidelines here]

## Support

For issues or questions, please open an issue on GitHub.
