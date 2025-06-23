# Use the official .NET 9 SDK (Alpine) image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src

# Copy the rest of the source code
COPY . .

# Restore the dependencies
RUN dotnet restore SlimBus.Api/SlimBus.Api.csproj

# Build the app
RUN dotnet publish SlimBus.Api/SlimBus.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Use the official ASP.NET 9 runtime (Alpine) image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app
COPY --from=build /app/publish .

# Expose port 80
EXPOSE 80

# Set the entrypoint
ENTRYPOINT ["dotnet", "SlimBus.Api.dll"]
