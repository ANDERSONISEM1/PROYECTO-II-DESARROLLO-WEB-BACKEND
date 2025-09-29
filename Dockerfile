# =========================
# Backend .NET 8 - Build
# =========================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia solo el csproj/sln y restaura (mejor cacheo)
COPY *.sln ./
COPY ./*.csproj ./
RUN dotnet restore

# Copia el resto del código y publica
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# =========================
# Backend .NET 8 - Runtime
# =========================
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Instalar curl para healthcheck (la imagen no lo trae)
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl ca-certificates bash \
 && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .

# Escuchar en todas las IPs (no localhost)
ENV ASPNETCORE_URLS=http://0.0.0.0:5080

# Healthcheck robusto
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
  CMD bash -lc 'exec 3<>/dev/tcp/127.0.0.1/5080 || exit 1; \
                curl -fsS http://127.0.0.1:5080/healthz || \
                curl -fsS http://127.0.0.1:5080/ || exit 1'

EXPOSE 5080

# Ejecutable correcto
ENTRYPOINT ["dotnet","api.dll"]
