# Entrega – Proyecto Final (Tecnología Web 1)

## 0. Antes de empezar (una sola vez)

```bash
cp .env.example .env                     # contraseñas y client id/secret de los proveedores
docker compose -f despliegue.yml up -d --build
```

**Usuarios y roles (guardados en PostgreSQL).**
- Tablas `usuarios`, `roles` y `usuarios_roles`.
- Al iniciar sesión por primera vez, el usuario se registra solo **sin roles** (recibe `403` en los reportes).
- El primer administrador se define una vez en `.env`: `ADMINISTRADORES_INICIALES=Discord:123456789` (o un correo).
- Desde el frontend, el Administrador abre el panel **Administración de usuarios y roles** y marca los roles de cada persona; se aplica al instante.
- API (solo Administrador): `GET /api/usuarios`, `GET /api/usuarios/roles`, `PUT /api/usuarios/{id}/roles`, `PUT /api/usuarios/{id}/activo`.

| Rol | Reportes | Métricas | Administrar usuarios |
|---|---|---|---|
| Administrador | los 3 | sí | sí |
| AnalistaServidores | Servidores | sí | no |
| AnalistaIncidentes | Incidentes | no | no |
| AnalistaAlertas | Alertas | no | no |

Datos de ejemplo para los reportes: `DATOS_DEMO=true` (se cargan solo si las tablas están vacías).

---

## 1. Frontend con login y logout (10 pts)

- Carpeta `frontend/public` (HTML + CSS + JS puro, sin build).
- 6 botones (Google, Discord, Spotify, Twitch, Dropbox, Slack) → `GET {API}/login/{proveedor}?returnUrl={FRONTEND_URL}`.
  El backend completa OAuth y **regresa al frontend** (el `returnUrl` se valida contra `Cors__AllowedOrigins`).
- Muestra nombre, correo, proveedor, id y roles (`GET /auth/usuario`).
- Logout: `POST /logout`; luego el frontend consulta `/api/reportes` y comprueba que responde `401`.

## 2. Reportes autorizados (15 pts)

| Reporte | Autor | Tablas | Propósito | Endpoint | Permisos (backend) |
|---|---|---|---|---|---|
| Disponibilidad y rendimiento de servidores | Yasmin | `servidores`, `tipos_servidor`, `sistemas_operativos`, `monitoreos` | % de disponibilidad, tiempo de respuesta y último estado por servidor | `GET /api/reportes/servidores` | Administrador o AnalistaServidores |
| Resumen de incidentes | Junior Cardozo | `Incidentes`, `TiposIncidente`, `PrioridadesIncidente`, `EstadosIncidente`, `servidores` | Incidentes por estado/prioridad/tipo/servidor y recientes | `GET /api/reportes/incidentes` | Administrador o AnalistaIncidentes |
| Alertas y notificaciones | Diego | `alertas`, `tipos_alerta`, `notificaciones`, `historial_alertas`, `servidores` | Alertas por tipo/servidor, envío de notificaciones y últimos movimientos | `GET /api/reportes/alertas` | Administrador o AnalistaAlertas |

> Ajusta el nombre del autor en `ReportesCatalogoController.cs` y en los comentarios de cada controlador si no coincide con tu equipo.

Catálogo (autor, tablas, endpoint, permisos): `GET /api/reportes`.
La validación está en el backend con `[Authorize(Policy = ...)]` (ver `Program.cs` y `API/Security`).

**Demostración (consola):**
```bash
curl -i http://localhost:8080/api/reportes/servidores     # 401  sin sesión
# Con cookie de un usuario SIN rol -> 403 ; con rol -> 200
curl -i -H "Cookie: MonitorServidoresSesion=VALOR" http://localhost:8080/api/reportes/servidores
```

## Extra. Monitoreo con Prometheus

Ver `docs/PROMETHEUS.md`. En el frontend, tarjeta **Monitoreo del servidor** (CPU, RAM, disco, uptime, disponibilidad y gráficas).
Endpoints: `GET /api/metricas/servidor`, `/api/metricas/historial`, `/api/metricas/objetivos` (Administrador o AnalistaServidores).

## 3. Dockerfile por integrante (15 pts)

| Archivo | Autor | Construye |
|---|---|---|
| `API/Dockerfile` | Junior (líder) – **imagen final del equipo** | `API/MonitorServidores.csproj` |
| `API/Dockerfile.Yasmin` | Yasmin | `API/MonitorServidores.csproj` |
| `API/Dockerfile.Diego` | Diego | `API/MonitorServidores.csproj` |

Todos: build multi-etapa (SDK 10 → ASP.NET 10), puerto interno `8080`, conexión por `ConnectionStrings__Postgres`.
Prueba de cada uno:
```bash
cd API
docker build -f Dockerfile.Yasmin -t monitor-api:yasmin .
docker build -f Dockerfile.Diego  -t monitor-api:diego .
docker build -f Dockerfile        -t monitor-api:final .
```

## 4 y 5. Compose de desarrollo (backend + BD + frontend) (25 pts)

Archivo: `despliegue.yml` (build desde Dockerfile, `.env`, puertos, red `monitor_red`, volúmenes `postgres_data` y `dp_keys`).

```bash
docker compose -f despliegue.yml up -d --build
```
- Frontend: http://localhost:3000  · API: http://localhost:8080 · Scalar: http://localhost:8080/scalar/v1
- **Migraciones:** se aplican solas al iniciar la API (`db.Database.Migrate()`); no hay pasos manuales.
  Para crear una nueva: `dotnet ef migrations add Nombre` dentro de `API/`.
- Configuración del navegador: `API_URL`, `FRONTEND_URL` (URL de retorno) y `Cors__AllowedOrigins` (permiso de origen).
- Redirect URIs a registrar en cada proveedor: `http://localhost:8080/signin-{google|discord|spotify|twitch|dropbox}`;
  Slack exige **HTTPS** (`https://localhost:8443/signin-slack` → usar un túnel como ngrok/cloudflared apuntando a :8080
  y poner esa URL en `SLACK_REDIRECT_URI`).
- Verificar: login → identidad → reportes → logout (401).

## 6. Publicar en Docker Hub (5 pts)

```bash
docker login
docker build -t TU_USUARIO/monitor-api:1.0.0 ./API
docker push TU_USUARIO/monitor-api:1.0.0
# comprobar descarga
docker pull TU_USUARIO/monitor-api:1.0.0
```
Entregar: enlace `https://hub.docker.com/r/TU_USUARIO/monitor-api` y referencia `TU_USUARIO/monitor-api:1.0.0`.

## 7. Compose de release – 4 instancias (20 pts)

Archivo: `docker-compose.release.yml` (solo `image:`, sin `build:`).
Puertos `5001-5004 → 8080`, misma imagen/versión, red y volumen de BD compartidos.
**Sesión compartida:** las 4 montan el mismo volumen `dp_keys` (`/keys`) y el mismo `SetApplicationName`,
así una cookie creada por una instancia la reconocen las demás.

```bash
# en .env: DOCKERHUB_USER, IMAGE_NAME, IMAGE_TAG
docker compose -f docker-compose.release.yml up -d
./scripts/probar-release.sh                       # health + reportes sin sesión (401) en los 4 puertos
COOKIE="valor" ./scripts/probar-release.sh        # reportes con sesión en los 4 puertos
```
Para iniciar sesión en el release hay que registrar en el proveedor el callback
`http://localhost:5001/signin-<proveedor>` e ingresar por `http://localhost:5001` (página con botones);
la cookie sirve luego en 5002-5004. Evidencia: capturas del script y de `/health` (campo `instancia`).

## 8. Pull Requests (10 pts)

Ver `docs/PULL_REQUESTS.md` (plantillas listas y tabla punto ↔ PR ↔ responsable).

## SSO (Single Sign-On) – demostración con dos aplicaciones

- **App 1** (monitoreo): http://localhost:3000 · **App 2** (Portal de Soporte): http://localhost:3001
- Ambas usan la misma API y la misma cookie de sesión; el inicio de sesión lo hace un proveedor externo (Discord, Google, Twitch, Spotify, Dropbox o Slack) vía OAuth 2.0 / OpenID Connect.
- Prueba: inicia sesión en la App 1 → abre la App 2 → ya estás dentro sin volver a autenticarte. Cierra sesión en una y la otra también queda cerrada.
- Importante: abre ambas con `localhost` (no mezcles `localhost` con `127.0.0.1`), porque la cookie pertenece al nombre del sitio.

## Login con TikTok (ngrok)

TikTok solo acepta un Redirect URI con **HTTPS**, así que en desarrollo se usa un túnel ngrok hacia la API.

1. En `.env`: `TIKTOK_CLIENT_KEY`, `TIKTOK_CLIENT_SECRET` y `TIKTOK_REDIRECT_URI=https://TU-DOMINIO.ngrok-free.dev/signin-tiktok` (idéntico al registrado en TikTok for Developers).
2. Abre el túnel apuntando a la API: `ngrok http 8080` (con el dominio fijo que ya tienes).
3. Flujo: frontend → `/login/tiktok` → TikTok → `/signin-tiktok` (ngrok) → `/signin-tiktok/completar` (localhost) → frontend. La sesión se crea en `localhost`, igual que con los demás proveedores, por eso funciona también el SSO con el Portal de Soporte.
4. Cada cuenta de TikTok queda registrada como `TikTok:<open_id>` y recibe sus roles desde el panel de administración.
5. Si cambia el dominio de ngrok, actualiza `.env` **y** el Redirect URI en TikTok for Developers, y reinicia: `docker compose -f despliegue.yml up -d`.
