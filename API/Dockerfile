# ======================================================================
# Dockerfile FINAL DEL EQUIPO (líder) - construye el backend integrado
# Proyecto: API/MonitorServidores.csproj
# Es el que se usa para generar la imagen publicada en Docker Hub.
# ======================================================================

# ---------- Etapa 1: compilación ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restaurar primero (aprovecha la caché de capas si no cambian los paquetes)
COPY MonitorServidores.csproj ./
RUN dotnet restore

COPY . .
RUN dotnet publish MonitorServidores.csproj -c Release -o /app/publish --no-restore

# ---------- Etapa 2: ejecución con los archivos compilados ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

# Carpeta para las claves de Data Protection (se monta como volumen
# compartido entre instancias). Se crea con dueño no-root para que el
# volumen nombrado herede permisos de escritura.
RUN mkdir -p /keys && chown $APP_UID:$APP_UID /keys

# Puerto interno de la API y configuración por defecto
ENV ASPNETCORE_URLS=http://+:8080 \
    DataProtection__KeysPath=/keys

EXPOSE 8080

USER $APP_UID

# La cadena de conexión se entrega en tiempo de ejecución:
#   ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=...;Username=...;Password=...
ENTRYPOINT ["dotnet", "MonitorServidores.dll"]
