# ==============================================
# ASP.NET Core Backend Dockerfile
# Multi-stage build for optimal image size
# ==============================================

# Stage 1: Build Stage
# Use the official .NET SDK image to build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the project file and restore dependencies
# This layer is cached unless the .csproj file changes
COPY ["MovieShop.Server.csproj", "./"]
RUN dotnet restore "MovieShop.Server.csproj"

# Copy the rest of the source code
COPY . .

# Build the application in Release mode
RUN dotnet build "MovieShop.Server.csproj" -c Release -o /app/build

# Stage 2: Publish Stage
# Publish the application (creates optimized, ready-to-run files)
FROM build AS publish
RUN dotnet publish "MovieShop.Server.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Runtime Stage
# Use the smaller ASP.NET Core runtime image (no SDK needed)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Expose ports
# Port 80 for HTTP traffic
EXPOSE 80
# Port 443 for HTTPS traffic (optional, for production with certificates)
EXPOSE 443

# Copy the published application from the publish stage
COPY --from=publish /app/publish .

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "MovieShop.Server.dll"]