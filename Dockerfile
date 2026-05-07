# ── Stage 1: Build the .NET application ──────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy solution and project files first for layer caching
COPY IsDB.Hospitality.sln ./
COPY src/IsDB.Hospitality.API/IsDB.Hospitality.API.csproj                         ./src/IsDB.Hospitality.API/
COPY src/IsDB.Hospitality.Application/IsDB.Hospitality.Application.csproj         ./src/IsDB.Hospitality.Application/
COPY src/IsDB.Hospitality.Domain/IsDB.Hospitality.Domain.csproj                   ./src/IsDB.Hospitality.Domain/
COPY src/IsDB.Hospitality.Infrastructure/IsDB.Hospitality.Infrastructure.csproj   ./src/IsDB.Hospitality.Infrastructure/

# Restore dependencies
RUN dotnet restore IsDB.Hospitality.sln

# Copy the rest of the source code
COPY src/ ./src/

# Publish the API project
RUN dotnet publish src/IsDB.Hospitality.API/IsDB.Hospitality.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime image ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy published output
COPY --from=build /app/publish .

# Railway injects PORT at runtime; default to 8080
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "IsDB.Hospitality.API.dll"]
