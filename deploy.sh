#!/bin/bash

# WiFi Manager Deployment Script for Raspberry Pi
# This script builds and deploys the Docker container

set -e

echo "=== WiFi Manager Deployment ==="
echo ""

# Configuration
CONTAINER_NAME="wifi-manager"
IMAGE_NAME="wifi-manager:latest"

# Check if Docker is installed
if ! command -v docker &> /dev/null; then
    echo "Error: Docker is not installed"
    exit 1
fi

# Check if Docker Compose is installed
if ! command -v docker compose &> /dev/null; then
    echo "Error: Docker Compose is not installed"
    exit 1
fi

# Stop existing container
echo "Stopping existing container..."
docker compose down || true

# Build new image
echo "Building Docker image..."
docker compose build --no-cache

# Start container
echo "Starting container..."
docker compose up -d

# Wait for container to be healthy
echo "Waiting for container to be healthy..."
sleep 5

# Check container status
if docker ps | grep -q $CONTAINER_NAME; then
    echo ""
    echo "✅ Deployment successful!"
    echo ""
    echo "Container status:"
    docker ps | grep $CONTAINER_NAME
    echo ""
    echo "Application is running at: http://localhost:8080"
    echo ""
    echo "View logs: docker logs -f $CONTAINER_NAME"
else
    echo ""
    echo "❌ Deployment failed!"
    echo ""
    echo "Container logs:"
    docker logs $CONTAINER_NAME
    exit 1
fi
