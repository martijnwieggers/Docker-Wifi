# Docker-Wifi — Projectstatus

Datum: 2026-05-08  
Branch: `main`  
Laatste commit: zie `git log --oneline`

---

## Wat is dit?

Blazor Server (.NET 8) webapplicatie die draait in een Docker-container op een Raspberry Pi. Biedt een webinterface voor:

- WiFi-netwerken scannen en verbinding maken via `wlan0`
- Verbonden clients monitoren op `wlan1` (Access Point, alleen read-only)
- De Raspberry Pi netjes afsluiten

Bereikbaar op poort 80 (`http://<pi-ip>/`).

---

## Architectuur

```
Program.cs
├── Services (DI)
│   ├── WifiStateContainer          — gedeelde singleton-state (immutable records + volatile)
│   ├── WifiShellService            — shell-commando's uitvoeren (ProcessStartInfo.ArgumentList)
│   ├── WifiScannerService          — nmcli scan + iw dev wlan0 link
│   ├── WifiConnectionService       — nmcli connect (personal + enterprise)
│   └── AccessPointClientService    — iw/arp clients wlan1
├── Background
│   └── WifiMonitoringBackgroundService — scan + clients elke ~10-15s
├── Components/Pages
│   ├── Home.razor                  — dashboard + shutdown-knop
│   ├── WifiScan.razor              — netwerkenlijst (tabel) + verbinden
│   └── WifiClients.razor           — AP-clientenoverzicht
├── Components/Layout
│   ├── NavMenu.razor               — navigatiemenu (Home, WiFi Networks, Connected Clients)
│   └── MainLayout.razor            — paginaschil
└── Components
    └── ConnectionDialog.razor      — verbindingsdialoog (personal + enterprise)
```

**Development** (Windows): mock-implementaties actief (geen echte nmcli calls)  
**Production** (Linux/Pi): echte shell-commando's via `LinuxDetectionHelper.IsLinux`

---

## Opgeloste problemen (chronologisch)

### 1. UI hing bij navigatie naar netwerken/clients
**Oorzaak**: `NotifyStateChanged()` werd aangeroepen terwijl `SemaphoreSlim` nog vergrendeld was. Callback-handlers probeerden dezelfde lock te acquiren → deadlock.  
**Fix**: `State` getter lock-free gemaakt (`volatile` referentie = atomair in .NET). `NotifyStateChanged()` aangeroepen ná de `finally`-block.  
**Bestand**: `Services/WifiStateContainer.cs`

### 2. Verbonden netwerk niet gedetecteerd (blank na verbinding)
**Oorzaak**: nmcli terse-mode gebruikt `:` als veldscheidingsteken. Beveiligingsvelden zoals `WPA2:WPA3` bevatten ook dubbele punten, waardoor kolom-indices verschoven.  
**Fix**: `iw dev wlan0 link` gebruikt voor betrouwbare SSID-detectie (onafhankelijk van nmcli-parsing).  
**Bestand**: `Services/WifiScannerService.cs` — methode `GetConnectedSsidAsync`

### 3. Verbinding mislukt door argument-escaping
**Oorzaak**: `ProcessStartInfo.Arguments` interpreteert shell-quoting niet; SSID's met spaties werden fout doorgegeven.  
**Fix**: Overgestapt op `ProcessStartInfo.ArgumentList` via `IEnumerable<string>` interface.  
**Bestand**: `Services/WifiShellService.cs`

### 4. Enterprise (eduroam) ondersteuning
**Toevoeging**: WPA2-Enterprise netwerken (PEAP/MSCHAPv2) worden herkend via `RSN-FLAGS` in nmcli-scan output. Aparte verbindingsflow met `nmcli connection add` + `--wait 0 connection up` + polling op `GENERAL.STATE` (max 60s in stappen van 5s).  
**Bestanden**: `Models/WifiNetwork.cs`, `Models/WifiConnectionRequest.cs`, `Services/WifiConnectionService.cs`, `Services/WifiScannerService.cs`, `Components/ConnectionDialog.razor`

### 5. Enterprise dialoog hing (verbinding werd wel gemaakt)
**Oorzaak**: `nmcli connection up` blokkeert 60-90 seconden voor eduroam. Timeout verstoorde de UI maar NetworkManager verbond alsnog.  
**Fix**: `--wait 0` flag zodat nmcli direct terugkeert; applicatie pollt daarna zelf de device-state.  
**Bestand**: `Services/WifiConnectionService.cs` — `ConnectEnterpriseAsync` + `PollForConnectionAsync`

### 6. Counter- en Weather-pagina's verwijderd
**Wijziging**: `Counter.razor` en `Weather.razor` verwijderd; uit de standaard Blazor-template, niet relevant voor de applicatie. `NavMenu.razor` bijgewerkt: alleen Home, WiFi Networks (`bi-broadcast`) en Connected Clients (`bi-people-fill`) staan nog in het menu.  
**Bestanden**: `Components/Pages/Counter.razor` (verwijderd), `Components/Pages/Weather.razor` (verwijderd), `Components/Layout/NavMenu.razor`

### 7. Shutdown werkt niet vanuit container
**Oorzaak 1**: `shutdown -h now` kon systemd niet bereiken — container heeft geen systemd als PID 1 en D-Bus is read-only gemount.  
**Fix 1**: `nsenter -t 1 -m -u -i -n -p -- shutdown -h now` om host-namespaces te betreden. `util-linux` toegevoegd aan Dockerfile (bevat `nsenter`).

**Oorzaak 2**: Zonder `pid: host` is PID 1 binnen de container het dotnet-process, niet de Pi's systemd. `nsenter -t 1` betrad daardoor de verkeerde namespaces.  
**Fix 2**: `pid: host` toegevoegd aan `docker-compose.yaml`. Nu verwijst PID 1 naar de Pi's systemd.  
**Bestanden**: `Dockerfile`, `Components/Pages/Home.razor`, `docker-compose.yaml`

---

## Docker-configuratie

```yaml
# docker-compose.yaml — relevante instellingen
network_mode: host      # toegang tot wlan0/wlan1
privileged: true        # netwerk-management permissies
pid: host               # PID 1 = host-init (nodig voor shutdown via nsenter)
restart: always         # herstart na reboot of crash

volumes:
  - /var/run/dbus:/var/run/dbus:ro
  - /etc/NetworkManager:/etc/NetworkManager:ro
```

---

## Deployen op de Pi

```bash
# Eerste keer of na wijzigingen aan docker-compose.yaml:
docker compose down && docker compose up -d --build

# Alleen image updaten (code-wijzigingen):
docker compose up -d --build
```

---

## Bekende beperkingen / openstaande punten

- CA-certificaat wordt niet meegeleverd voor enterprise-verbindingen; NetworkManager gebruikt systeem-CA's als fallback of verbindt zonder verificatie
- Wlan1 (AP) is volledig read-only; wordt nooit gewijzigd door de applicatie
- Shutdown-knop stuurt `shutdown -h now` via nsenter; er is geen bevestiging in de UI nadat het commando is uitgevoerd (Pi is dan immers offline)
