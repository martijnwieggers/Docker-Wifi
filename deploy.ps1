# WiFi Manager Deployment Script for Raspberry Pi (PowerShell)
# This script is for testing deployment from Windows (will need to be run on Pi)

$ErrorActionPreference = "Stop"

Write-Host "=== WiFi Manager Deployment ===" -ForegroundColor Cyan
Write-Host ""

# Configuration
$CONTAINER_NAME = "wifi-manager"
$IMAGE_NAME = "wifi-manager:latest"

# Check if Docker is available
try {
    docker --version | Out-Null
} catch {
    Write-Host "Error: Docker is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# Stop existing container
Write-Host "Stopping existing container..." -ForegroundColor Yellow
docker compose down 2>$null

# Build new image
Write-Host "Building Docker image..." -ForegroundColor Yellow
docker compose build --no-cache

# Start container
Write-Host "Starting container..." -ForegroundColor Yellow
docker compose up -d

# Wait for container to be healthy
Write-Host "Waiting for container to be healthy..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

# Check container status
$containerRunning = docker ps | Select-String -Pattern $CONTAINER_NAME

if ($containerRunning) {
    Write-Host ""
    Write-Host "✅ Deployment successful!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Container status:" -ForegroundColor Cyan
    docker ps | Select-String -Pattern $CONTAINER_NAME
    Write-Host ""
    Write-Host "Application is running at: http://localhost:8080" -ForegroundColor Green
    Write-Host ""
    Write-Host "View logs: docker logs -f $CONTAINER_NAME" -ForegroundColor Cyan
} else {
    Write-Host ""
    Write-Host "❌ Deployment failed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Container logs:" -ForegroundColor Yellow
    docker logs $CONTAINER_NAME
    exit 1
}
