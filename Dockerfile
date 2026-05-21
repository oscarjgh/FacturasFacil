# ── Etapa 1: Build ────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copiar archivos de proyecto y restaurar dependencias (cache layer)
COPY src/FacturasFacil.Core/FacturasFacil.Core.csproj  src/FacturasFacil.Core/
COPY src/FacturasFacil.Api/FacturasFacil.Api.csproj    src/FacturasFacil.Api/
RUN dotnet restore src/FacturasFacil.Api/FacturasFacil.Api.csproj

# Copiar todo el código y publicar
COPY src/ src/
RUN dotnet publish src/FacturasFacil.Api/FacturasFacil.Api.csproj \
    -c Release -o /app/out --no-restore

# ── Etapa 2: Runtime (imagen mínima) ──────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# Instalar librerías nativas que necesita SQLite
RUN apt-get update && apt-get install -y --no-install-recommends \
    libsqlite3-dev \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out .

# Railway inyecta PORT como variable de entorno
ENV ASPNETCORE_URLS=http://+:${PORT:-5000}
ENV ASPNETCORE_ENVIRONMENT=Production

# Carpeta persistente para la DB y los Excels generados
VOLUME ["/data"]
ENV ConnectionStrings__Default="Data Source=/data/facturasfacil.db"
ENV Historial__CarpetaBase="/data/excels"

EXPOSE 5000
ENTRYPOINT ["dotnet", "FacturasFacil.Api.dll"]
