#!/usr/bin/env bash
# SOLO para cambios futuros del modelo (la migración de usuarios y roles ya viene incluida).
# Genera una migración nueva de EF Core
# usando un contenedor con el SDK de .NET: no necesitas instalar .NET.
# Uso:  ./scripts/generar-migracion.sh [NombreMigracion]
set -euo pipefail

cd "$(dirname "$0")/../API"

docker run --rm \
  --user "$(id -u):$(id -g)" \
  -e HOME=/tmp \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  -e ConnectionStrings__Postgres="Host=localhost;Database=x;Username=x;Password=x" \
  -e Database__ApplyMigrations=false \
  -v "$PWD":/src -w /src \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  sh -c "dotnet tool install --tool-path /tmp/tools dotnet-ef --version 10.* >/dev/null && /tmp/tools/dotnet-ef migrations add ${1:-AgregarUsuariosYRoles} --project MonitorServidores.csproj"

echo
echo "Migración creada en API/Migrations. Ahora ejecuta:"
echo "  docker compose -f despliegue.yml up -d --build"
