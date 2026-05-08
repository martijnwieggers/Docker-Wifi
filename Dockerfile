# Multi-stage build for Raspberry Pi ARM64

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY ["Docker-Wifi.csproj", "./"]
RUN dotnet restore "Docker-Wifi.csproj"

# Copy source code and build
COPY . .
RUN dotnet build "Docker-Wifi.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "Docker-Wifi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install required network utilities
RUN apt-get update && \
    DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends \
    network-manager \
    wireless-tools \
    iw \
    iproute2 \
    net-tools \
    util-linux \
    && rm -rf /var/lib/apt/lists/*

# Copy published application
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port
EXPOSE 8080

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/ || exit 1

# Entry point
ENTRYPOINT ["dotnet", "Docker-Wifi.dll"]
