# Quick Start Guide - WiFi Manager

## Raspberry Pi Deployment

### Prerequisites
```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
curl -fsSL https://get.docker.com -o get-docker.sh
sudo sh get-docker.sh
sudo usermod -aG docker $USER

# Install Docker Compose
sudo apt install -y docker-compose-plugin

# Verify installation
docker --version
docker compose version
```

### Deploy Application

1. **Clone repository or copy files to Raspberry Pi**
```bash
cd /home/pi
mkdir wifi-manager
cd wifi-manager
# Copy all project files here
```

2. **Make deploy script executable**
```bash
chmod +x deploy.sh
```

3. **Deploy**
```bash
./deploy.sh
```

4. **Access Application**
```
http://<raspberry-pi-ip>:8080
```

### Verify Network Interfaces

```bash
# Check wlan0 (should be managed by NetworkManager)
nmcli device status

# Check wlan1 (should show your AP)
iw dev wlan1 info

# Verify both interfaces are up
ip link show wlan0
ip link show wlan1
```

## Windows Development

### Prerequisites
- .NET 8 SDK
- Visual Studio 2022 or VS Code

### Run Locally

1. **Clone repository**
```bash
git clone <repository-url>
cd Docker-Wifi
```

2. **Run application**
```bash
dotnet run
```

3. **Access application**
```
https://localhost:5001
```

**Note:** On Windows, the application uses mock services for development.

## Docker Commands

### View Logs
```bash
docker logs wifi-manager -f
```

### Restart Container
```bash
docker compose restart
```

### Stop Container
```bash
docker compose down
```

### Rebuild Container
```bash
docker compose down
docker compose build --no-cache
docker compose up -d
```

### Check Container Status
```bash
docker ps
docker stats wifi-manager
```

## Troubleshooting

### Container won't start

1. Check logs:
```bash
docker logs wifi-manager
```

2. Verify network interfaces:
```bash
nmcli device status
ip link show
```

3. Check NetworkManager:
```bash
systemctl status NetworkManager
```

### Can't scan networks

1. Verify wlan0 is managed:
```bash
nmcli device show wlan0
```

2. Check if wlan0 is UP:
```bash
ip link show wlan0
```

3. Test nmcli manually:
```bash
nmcli device wifi list ifname wlan0
```

### Can't see connected clients

1. Check wlan1 interface:
```bash
iw dev wlan1 info
iw dev wlan1 station dump
```

2. Verify AP is running:
```bash
nmcli connection show
```

## Configuration Tips

### Change refresh intervals

Edit `Background/WifiMonitoringBackgroundService.cs`:
```csharp
private static readonly TimeSpan ClientsRefreshInterval = TimeSpan.FromSeconds(10);
private static readonly TimeSpan NetworksRefreshInterval = TimeSpan.FromSeconds(15);
```

### Change listening port

Edit `docker-compose.yaml`:
```yaml
environment:
  - ASPNETCORE_URLS=http://+:8080  # Change 8080 to desired port
```

Or edit `appsettings.json`:
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://*:8080"
      }
    }
  }
}
```

### Enable HTTPS

1. Generate certificate
2. Mount certificate in docker-compose.yaml
3. Update Kestrel configuration

## Performance Tuning

### Raspberry Pi 4/5 Recommended Settings

```yaml
deploy:
  resources:
    limits:
      cpus: '2.0'
      memory: 512M
    reservations:
      cpus: '0.5'
      memory: 256M
```

### Older Raspberry Pi Models

```yaml
deploy:
  resources:
    limits:
      cpus: '1.0'
      memory: 384M
    reservations:
      cpus: '0.25'
      memory: 192M
```

## Security Considerations

### Production Checklist

- [ ] Change default ports if needed
- [ ] Set up firewall rules
- [ ] Enable HTTPS
- [ ] Review log levels (disable Trace/Debug)
- [ ] Implement authentication if needed
- [ ] Regular updates of base images
- [ ] Monitor container resources

## Monitoring

### Check Application Health

```bash
curl http://localhost:8080/
```

### Container Resource Usage

```bash
docker stats wifi-manager
```

### System Resource Usage

```bash
htop
free -h
df -h
```

## Backup & Recovery

### Backup Configuration

```bash
# Backup docker-compose.yaml and appsettings
tar -czf wifi-manager-config.tar.gz \
  docker-compose.yaml \
  appsettings.json \
  appsettings.Production.json
```

### Restore from Backup

```bash
tar -xzf wifi-manager-config.tar.gz
./deploy.sh
```

## Updates

### Update Application

1. Pull latest code
2. Rebuild container
```bash
./deploy.sh
```

### Update Base Images

```bash
docker compose pull
docker compose up -d
```

## Support

For issues, check:
1. Container logs
2. System logs: `journalctl -xe`
3. NetworkManager logs: `journalctl -u NetworkManager`
4. GitHub Issues
