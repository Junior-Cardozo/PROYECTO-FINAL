# Sistema de Monitoreo de Servidores

Proyecto académico desarrollado para la asignatura de Tecnología Web, orientado al monitoreo de servidores, administración de incidentes, autenticación de usuarios y visualización de métricas mediante una aplicación web.

## Descripción del proyecto

El sistema permite supervisar servidores mediante una plataforma web desarrollada con ASP.NET Core y PostgreSQL.

Contempla autenticación mediante proveedores externos, control de acceso a recursos protegidos, generación de reportes y recopilación de métricas utilizando Prometheus.

La aplicación utiliza Docker para facilitar la construcción, ejecución y despliegue de sus servicios.

## Tecnologías utilizadas

- C# y ASP.NET Core 10.
- Entity Framework Core.
- PostgreSQL.
- Docker y Docker Compose.
- HTML, CSS y JavaScript.
- Prometheus.
- Node Exporter.
- Git y GitHub.

## Funcionalidades principales

### 1. Autenticación y autorización

El sistema contempla los siguientes proveedores de inicio de sesión:

- Google.
- Discord.
- Spotify.
- Twitch.
- Dropbox.
- Slack.

Se implementará un mecanismo de autorización para controlar qué usuarios pueden ingresar y qué recursos pueden consultar.

La autenticación permite identificar al usuario, mientras que la autorización determina los permisos que posee dentro del sistema.

### 2. Monitoreo de servidores

Se integrará Prometheus para recopilar y almacenar métricas de los servidores.

Se utilizará Node Exporter para obtener información del sistema operativo Linux.

Las métricas contempladas incluyen:

- Uso de CPU.
- Memoria RAM.
- Espacio disponible en disco.
- Tiempo de actividad.
- Disponibilidad del servidor.

**Herramientas adicionales opcionales:**

- cAdvisor: monitoreo individual de contenedores Docker.

### 3. Reportes autorizados

Cada integrante desarrollará al menos un reporte utilizando las tablas que tiene asignadas de las tareas anteriores.

Cada reporte deberá especificar:

- Autor responsable.
- Tablas utilizadas.
- Propósito del reporte.
- Endpoint correspondiente.
- Permisos necesarios para acceder.

La autorización se validará desde el backend.

### 4. Interfaz web

Se desarrollará una interfaz que permita:

- Iniciar sesión mediante los proveedores disponibles.
- Mostrar la identidad del usuario autenticado.
- Cerrar sesión.
- Consultar los reportes autorizados.
- Visualizar métricas del monitoreo.
- Consultar el estado de los servidores.

## Funcionalidades adicionales opcionales

Se contempla la posibilidad de ampliar el sistema con:

- **SSO (Single Sign-On):** centralización de la autenticación y administración de sesiones.
- **Monitoreo (Prometheus):** [docs/PROMETHEUS.md](docs/PROMETHEUS.md) · **Entrega y pruebas paso a paso:** [docs/ENTREGA.md](docs/ENTREGA.md) · **Pull Requests:** [docs/PULL_REQUESTS.md](docs/PULL_REQUESTS.md)

**GitHub:** proveedor adicional de inicio de sesión.
- **TikTok:** integración mediante Login Kit.
- **cAdvisor:** recopilación de métricas individuales de contenedores.

Estas funcionalidades se consideran ampliaciones y no sustituyen los requisitos principales del proyecto.

## Arquitectura del sistema

| Componente | Función |
|---|---|
| Frontend | Interfaz de usuario y dashboard. |
| ASP.NET Core | API, autenticación y autorización. |
| PostgreSQL | Almacenamiento de información. |
| Prometheus | Recopilación y almacenamiento de métricas. |
| Node Exporter | Métricas del sistema operativo. |
| Docker | Ejecución de servicios en contenedores. |
| Docker Compose | Administración del entorno. |

## Estructura del proyecto

```text
PROYECTO-FINAL/
│
├── API/
│   ├── Controllers/
│   ├── Data/
│   ├── Models/
│   ├── Program.cs
│   └── Dockerfile
│
├── frontend/
│   ├── public/            (index.html, app.js, styles.css)
│   ├── Dockerfile
│   └── nginx.conf
│
├── prometheus/
│   └── prometheus.yml
│
├── .env.example
├── .gitignore
├── despliegue.yml
├── docker-compose.release.yml
├── docs/
├── scripts/
├── CONTRIBUTING.md
└── README.md
```

La estructura podrá ampliarse durante la integración de los módulos.

## Requisitos para ejecutar el proyecto

- Docker Engine o Docker Desktop.
- Docker Compose.
- Git.
- Credenciales de los proveedores OAuth utilizados.

## Instalación y configuración

Clonar el repositorio:

```bash
git clone https://github.com/Junior-Cardozo/PROYECTO-FINAL.git
```

Ingresar al directorio:

```bash
cd PROYECTO-FINAL
```

Crear el archivo de variables de entorno:

```bash
cp .env.example .env
```

Configurar las variables necesarias en `.env`.

**Importante:** el archivo `.env` contiene información privada y no debe publicarse en GitHub.

## Ejecución del entorno de desarrollo

Construir e iniciar los servicios:

```bash
docker compose -f despliegue.yml up -d --build
```

Consultar los contenedores activos:

```bash
docker ps
```

Consultar los registros del backend:

```bash
docker logs -f monitor-api
```

Con este único comando se levantan **PostgreSQL, backend y frontend**.

El frontend se encuentra en:

```text
http://localhost:3000
```

La API se encuentra disponible en:

```text
http://localhost:8080
```

La documentación de la API mediante Scalar:

```text
http://localhost:8080/scalar/v1
```

PostgreSQL utiliza el puerto externo 5433 en la configuración actual.

Para detener los servicios:

```bash
docker compose -f despliegue.yml down
```

## Despliegue de release

El proyecto contempla la publicación de una imagen del backend en Docker Hub.

El entorno de release utilizará cuatro instancias del backend, todas basadas en la misma versión de imagen.

| Instancia | Puerto externo |
|---|---|
| Backend 1 | 5001 |
| Backend 2 | 5002 |
| Backend 3 | 5003 |
| Backend 4 | 5004 |

Las cuatro instancias compartirán la configuración de conexión a PostgreSQL y la configuración necesaria para mantener las sesiones de autenticación.

El despliegue se realizará mediante un archivo Compose independiente, utilizando la imagen publicada en Docker Hub.

## Organización del equipo

| Integrante | Responsabilidades |
|---|---|
| Integrante 1 (Líder) | Autenticación de los seis proveedores, restricciones de acceso, frontend, integración general, Docker Hub y Compose de release. |
| Integrante 2 | Prometheus, Node Exporter, recopilación de métricas y visualización de información del servidor. |
| Integrante 3 | Reportes autorizados, consultas SQL, endpoints protegidos y documentación de sus tablas. |

### Responsabilidades compartidas

Todos los integrantes deberán:

- Crear su propio Dockerfile.
- Trabajar mediante ramas independientes.
- Registrar sus cambios mediante commits.
- Crear Pull Requests.
- Documentar sus aportes.
- Realizar pruebas de funcionamiento.

## Control de versiones

El proyecto utiliza Git y GitHub para mantener la trazabilidad de los aportes.

Crear una rama de trabajo:

```bash
git checkout -b feature/nombre-funcionalidad
```

Registrar los cambios:

```bash
git add .
git commit -m "feat: descripcion del cambio"
git push -u origin feature/nombre-funcionalidad
```

Posteriormente, crear una Pull Request hacia la rama principal `main`.

## Seguridad

- No publicar credenciales reales.
- Mantener `.env` excluido del repositorio.
- Utilizar `.env.example` como plantilla.
- Proteger los endpoints que requieran autenticación.
- Validar permisos desde el backend.
- Mantener una configuración de autenticación compartida entre las instancias.

## Repositorios

**GitHub:**

https://github.com/Junior-Cardozo/PROYECTO-FINAL

**Docker Hub:**

Pendiente de publicación de la imagen final.

## Estado del proyecto

Proyecto académico en desarrollo e integración.

Las funcionalidades se incorporarán progresivamente mediante ramas, commits y Pull Requests.
