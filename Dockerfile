# syntax=docker/dockerfile:1

# ---------- build ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first so the layer is cached when only source files change.
COPY global.json Directory.Build.props Novaforge.slnx ./
COPY src/Novaforge.Web/Novaforge.Web.csproj src/Novaforge.Web/
COPY tests/Novaforge.Tests/Novaforge.Tests.csproj tests/Novaforge.Tests/
RUN dotnet restore Novaforge.slnx

COPY . .
RUN dotnet publish src/Novaforge.Web/Novaforge.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DataProtection__KeysPath=/app/data/keys

COPY --from=build /app/publish .
RUN mkdir -p /app/data/keys /app/app_data && chown -R $APP_UID /app/data /app/app_data

# Non-root user provided by the official .NET images.
USER $APP_UID
EXPOSE 8080

# Liveness/readiness: point your platform's HTTP health check at GET /health
# (the slim aspnet image has no curl, so no Docker HEALTHCHECK here).

ENTRYPOINT ["dotnet", "Novaforge.Web.dll"]
