# Registro de Pull Requests

Flujo (según CONTRIBUTING.md): rama por aporte → PR a `main` → revisa el líder → merge.
La PR del líder la revisa otro integrante. **Cada PR debe ser abierta por quien realmente hizo el aporte.**

## Tabla punto ↔ PR ↔ responsable (completar con los números reales)

| Punto | Rama sugerida | Archivos | Autor | Revisor | PR |
|---|---|---|---|---|---|
| 1. Frontend login/logout | `feature/junior-frontend` | `frontend/public/*`, `Program.cs` (login/logout/CORS) | Junior | Yasmin | #__ |
| 2a. Reporte servidores | `feature/yasmin-reporte-servidores` | `ReporteServidoresController.cs` | Yasmin | Junior | #__ |
| 2b. Reporte incidentes | `feature/junior-reporte-incidentes` | `ReporteIncidentesController.cs`, `Security/*` | Junior | Diego | #__ |
| 2c. Reporte alertas | `feature/diego-reporte-alertas` | `ReporteAlertasController.cs` | Diego | Junior | #__ |
| Extra. Usuarios y roles en BD + panel admin | `feature/junior-usuarios-roles` | `Models/Usuario.cs`, `Models/Rol.cs`, `Security/*`, `UsuariosController.cs`, migración | Junior | Yasmin | #__ |
| Extra. Prometheus + Node Exporter + métricas | `feature/yasmin-prometheus` | `prometheus/prometheus.yml`, `MetricasController.cs`, servicios en `despliegue.yml` | Yasmin | Junior | #__ |
| 3a. Dockerfile Yasmin | `feature/yasmin-dockerfile` | `API/Dockerfile.Yasmin` | Yasmin | Junior | #__ |
| 3b. Dockerfile Diego | `feature/diego-dockerfile` | `API/Dockerfile.Diego` | Diego | Junior | #__ |
| 3c. Dockerfile final | `feature/junior-dockerfile` | `API/Dockerfile` | Junior | Yasmin | #__ |
| 4. Compose dev backend | `feature/junior-compose-dev` | `despliegue.yml`, `.env.example` | Junior | Diego | #__ |
| 5. Frontend en Compose | `feature/junior-compose-frontend` | `frontend/Dockerfile`, `nginx.conf` | Junior | Yasmin | #__ |
| 6. Imagen Docker Hub | `docs/junior-dockerhub` | `README.md` (enlace/versión) | Junior | Diego | #__ |
| 7. Compose release | `feature/junior-compose-release` | `docker-compose.release.yml`, `scripts/` | Junior | Yasmin | #__ |

## Plantilla de descripción de PR

```
## Punto que resuelve
(Punto N de la tarea: ...)

## Autor
@usuario

## Cambios
- ...

## Cómo se comprobó
- Comandos ejecutados y resultado esperado/obtenido
- Capturas (si aplica)
```

## Ejemplo – PR punto 2a (Yasmin)

```
## Punto que resuelve
2. Reporte autorizado por integrante

## Autor
@yasmin

## Cambios
- Nuevo GET /api/reportes/servidores (tablas: servidores, tipos_servidor, sistemas_operativos, monitoreos)
- Autorización: rol Administrador o AnalistaServidores

## Cómo se comprobó
- Sin sesión: 401
- Usuario autenticado sin rol: 403
- Usuario con rol: 200 con % de disponibilidad por servidor
```
