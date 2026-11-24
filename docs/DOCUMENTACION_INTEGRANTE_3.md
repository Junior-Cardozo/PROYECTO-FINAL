Documentación de tablas y reportes — Integrante 3 yasmin diaz
Proyecto: Monitoreo de Servidores
Tecnologías: ASP.NET Core .NET 10, Entity Framework Core, PostgreSQL, Docker y Scalar.
Responsabilidad: reportes autorizados, consultas SQL, endpoints protegidos y documentación de tablas.

1. Objetivo
   Documentar las cuatro tablas que utiliza el módulo de reportes y las tres consultas implementadas en API/Controllers/ReportesController.cs. El módulo reutiliza las tablas existentes y no requiere nuevas migraciones.
2. Diccionario de datos
   Los nombres de columnas respetan el modelo del proyecto. En PostgreSQL deben incluirse entre comillas dobles cuando se consultan mediante SQL directo.
   2.1. servidores
   Almacena los servidores y su clasificación.
   Columna Tipo PostgreSQL Restricciones Descripción
   Id integer PK, autogenerado, no nulo Identificador único.
   Nombre varchar(100) No nulo Nombre del servidor.
   DireccionIp varchar(255) No nulo Dirección IP registrada.
   Descripcion varchar(300) Puede ser nulo Información complementaria.
   Activo boolean No nulo Indicador lógico de actividad.
   FechaRegistro timestamp with time zone No nulo Fecha de registro.
   TipoServidorId integer FK, puede ser nulo Referencia a tipos_servidor.Id.
   SistemaOperativoId integer FK, puede ser nulo Referencia a sistemas_operativos.Id.

La clase C# inicializa Activo = true y FechaRegistro = DateTime.UtcNow.
2.2. monitoreos
Registra las comprobaciones de disponibilidad de los servidores.
Columna Tipo PostgreSQL Restricciones Descripción
Id integer PK, autogenerado, no nulo Identificador del monitoreo.
ServidorId integer FK, no nulo Referencia a servidores.Id.
FechaRegistro timestamp with time zone No nulo Fecha de la comprobación.
Disponible boolean No nulo Indica disponibilidad.
TiempoRespuestaMs integer Puede ser nulo; [Range(0, int.MaxValue)] en el modelo Tiempo de respuesta en milisegundos.
Observaciones varchar(300) Puede ser nulo Comentarios de la comprobación.
Activo boolean No nulo Indicador lógico de actividad.

El modelo inicializa FechaRegistro = DateTime.UtcNow y Activo = true. La validación de rango pertenece a la clase C#; la migración revisada no crea una restricción SQL CHECK para este campo.
2.3. sistemas_operativos
Catálogo de sistemas operativos que pueden utilizar los servidores.
Columna Tipo PostgreSQL Restricciones Descripción
Id integer PK, autogenerado, no nulo Identificador del sistema operativo.
Nombre varchar(100) No nulo Nombre del sistema operativo.
Version varchar(50) Puede ser nulo Versión.
Descripcion varchar(300) Puede ser nulo Descripción.
Activo boolean No nulo Indicador lógico de actividad.

El modelo inicializa Activo = true.
2.4. tipos_servidor
Catálogo de tipos de servidor.
Columna Tipo PostgreSQL Restricciones Descripción
Id integer PK, autogenerado, no nulo Identificador del tipo.
Nombre varchar(100) No nulo Nombre del tipo de servidor.
Descripcion varchar(300) Puede ser nulo Descripción.
Activo boolean No nulo Indicador lógico de actividad.

El modelo inicializa Activo = true. 3. Relaciones y reglas
sistemas_operativos (1) ──── (0..N) servidores
tipos_servidor (1) ──── (0..N) servidores
servidores (1) ──── (0..N) monitoreos

- servidores.SistemaOperativoId y servidores.TipoServidorId admiten valores nulos: la clasificación es opcional.
- monitoreos.ServidorId no admite nulos: cada monitoreo pertenece a un servidor.
- Las tres relaciones tienen DeleteBehavior.Restrict: no se puede eliminar el registro principal si quedan registros dependientes.
- La migración crea índices para las claves foráneas SistemaOperativoId, TipoServidorId y ServidorId.

4. Reportes autorizados
   El controlador ReportesController tiene [ApiController], [Route("api/reportes")] y [Authorize]. Los tres métodos son GET, solo consultan información, utilizan MonitorDbContext y responden con JSON.
   4.1. Servidores por sistema operativo
   Ruta: GET /api/reportes/servidores-por-sistema
   Función: agrupar servidores por SistemaOperativoId y contar los registros de cada grupo.

SQL equivalente a la consulta LINQ:
SELECT s."SistemaOperativoId",
COUNT(\*) AS "CantidadServidores"
FROM public.servidores AS s
GROUP BY s."SistemaOperativoId";
Respuesta: sistemaOperativoId, cantidadServidores. Un servidor sin sistema operativo puede aparecer en un grupo cuyo ID es null.
4.2. Monitoreos por servidor
Ruta: GET /api/reportes/monitoreos-por-servidor
Función: listar cada servidor con su nombre y la cantidad de monitoreos asociados, incluso cuando la cantidad sea cero.

SQL equivalente a la consulta LINQ:
SELECT s."Id" AS "ServidorId",
s."Nombre" AS "NombreServidor",
COUNT(m."Id") AS "CantidadMonitoreos"
FROM public.servidores AS s
LEFT JOIN public.monitoreos AS m
ON m."ServidorId" = s."Id"
GROUP BY s."Id", s."Nombre";
Respuesta: servidorId, nombreServidor, cantidadMonitoreos.
4.3. Servidores por tipo
Ruta: GET /api/reportes/servidores-por-tipo
Función: agrupar servidores por TipoServidorId y contar los registros de cada grupo.

SQL equivalente a la consulta LINQ:
SELECT s."TipoServidorId",
COUNT(\*) AS "CantidadServidores"
FROM public.servidores AS s
GROUP BY s."TipoServidorId";
Respuesta: tipoServidorId, cantidadServidores. Un servidor sin tipo asignado puede aparecer en un grupo con ID null.

Alcance de las consultas: actualmente los tres reportes cuentan registros sin filtrar por Activo ni Disponible. Los reportes por tipo y por sistema operativo devuelven identificadores, no nombres de catálogo. 5. Verificación en Scalar
Prueba Resultado esperado
Solicitud sin sesión autenticada 401 Unauthorized
GET /api/reportes/servidores-por-sistema con sesión 200 OK
GET /api/reportes/monitoreos-por-servidor con sesión 200 OK
GET /api/reportes/servidores-por-tipo con sesión 200 OK

Se comprobó en Scalar que los endpoints responden 200 OK con la sesión autenticada. Las respuestas [] observadas indican ausencia de servidores en ese momento y no prueban todavía la exactitud de los conteos con datos poblados.

Para una prueba funcional más completa: registrar un sistema operativo y un tipo; crear dos servidores asociados; añadir dos monitoreos a uno solo; verificar que los conteos por sistema y tipo sean 2 y que los conteos de monitoreos por servidor sean 2 y 0. Guardar capturas de estas pruebas. 6. Consultas de inspección
SELECT _ FROM public.servidores;
SELECT _ FROM public.monitoreos;
SELECT _ FROM public.sistemas_operativos;
SELECT _ FROM public.tipos_servidor; 7. Conclusión
La parte del Integrante 3 utiliza cuatro tablas existentes para ofrecer tres reportes de lectura protegidos por autenticación, construidos con Entity Framework Core y PostgreSQL. No se modificaron las entidades ni las migraciones para implementar estos reportes.

Fuentes revisadas en la rama feature/reportes-autorizados-yasmin: API/Models/Servidor.cs, Monitoreo.cs, SistemaOperativo.cs, TipoServidor.cs; API/Data/MonitorDbContext.cs; API/Migrations/20261121220406_InicialTablasYasmin.cs; API/Controllers/ReportesController.cs.
