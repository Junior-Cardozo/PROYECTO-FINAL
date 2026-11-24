using Microsoft.EntityFrameworkCore;
using MonitorServidores.Models;
using MonitorServidores.Security;

namespace MonitorServidores.Data;

public static class DatosIniciales
{
    /// <summary>Crea los roles del sistema si no existen (idempotente).</summary>
    public static void SembrarRoles(MonitorDbContext db)
    {
        var existentes = db.Roles.Select(r => r.Nombre).ToHashSet();

        foreach (var (nombre, descripcion) in Roles.Definiciones)
        {
            if (!existentes.Contains(nombre))
                db.Roles.Add(new Rol { Nombre = nombre, Descripcion = descripcion });
        }

        db.SaveChanges();
    }

    /// <summary>
    /// Datos de ejemplo para que los reportes se vean con información.
    /// Solo se cargan si la tabla de servidores está vacía y Database:SeedDemo=true.
    /// </summary>
    public static void SembrarDemo(MonitorDbContext db)
    {
        if (db.Servidores.Any())
            return;

        var aleatorio = new Random(42);
        var ahora = DateTime.UtcNow;

        // ---- Servidores ----
        var tipoWeb = new TipoServidor { Nombre = "Servidor web", Descripcion = "Aplicaciones web y APIs" };
        var tipoBd = new TipoServidor { Nombre = "Base de datos", Descripcion = "Servidores de datos" };
        var tipoArchivos = new TipoServidor { Nombre = "Archivos", Descripcion = "Almacenamiento y respaldos" };

        var ubuntu = new SistemaOperativo { Nombre = "Ubuntu Server", Version = "24.04 LTS" };
        var debian = new SistemaOperativo { Nombre = "Debian", Version = "12" };
        var windows = new SistemaOperativo { Nombre = "Windows Server", Version = "2022" };

        var web01 = new Servidor { Nombre = "srv-web-01", DireccionIp = "192.168.1.10", Descripcion = "Frontend y API", TipoServidor = tipoWeb, SistemaOperativo = ubuntu };
        var bd01 = new Servidor { Nombre = "srv-bd-01", DireccionIp = "192.168.1.20", Descripcion = "PostgreSQL principal", TipoServidor = tipoBd, SistemaOperativo = debian };
        var arch01 = new Servidor { Nombre = "srv-archivos-01", DireccionIp = "192.168.1.30", Descripcion = "Respaldos", TipoServidor = tipoArchivos, SistemaOperativo = windows };

        var servidores = new[] { web01, bd01, arch01 };
        var probabilidadCaida = new[] { 0.05, 0.02, 0.25 };

        var monitoreos = new List<Monitoreo>();
        for (var i = 0; i < servidores.Length; i++)
        {
            for (var k = 0; k < 24; k++)
            {
                var disponible = aleatorio.NextDouble() > probabilidadCaida[i];
                monitoreos.Add(new Monitoreo
                {
                    Servidor = servidores[i],
                    FechaRegistro = ahora.AddHours(-k),
                    Disponible = disponible,
                    TiempoRespuestaMs = disponible ? aleatorio.Next(20, 250) : null,
                    Observaciones = disponible ? "Respuesta correcta" : "Sin respuesta"
                });
            }
        }

        // ---- Incidentes ----
        var abierto = new EstadoIncidente { Nombre = "Abierto", Descripcion = "Pendiente de atención" };
        var proceso = new EstadoIncidente { Nombre = "En proceso", Descripcion = "En atención" };
        var resuelto = new EstadoIncidente { Nombre = "Resuelto", Descripcion = "Solucionado" };

        var alta = new PrioridadIncidente { Nombre = "Alta", Descripcion = "Atender de inmediato" };
        var media = new PrioridadIncidente { Nombre = "Media", Descripcion = "Atender hoy" };
        var baja = new PrioridadIncidente { Nombre = "Baja", Descripcion = "Atender cuando sea posible" };

        var caida = new TipoIncidente { Nombre = "Caída de servicio", Descripcion = "Servicio sin respuesta" };
        var rendimiento = new TipoIncidente { Nombre = "Rendimiento", Descripcion = "Lentitud o uso excesivo de recursos" };
        var seguridad = new TipoIncidente { Nombre = "Seguridad", Descripcion = "Accesos o eventos sospechosos" };

        Incidente Inc(string titulo, string desc, Servidor s, TipoIncidente t, PrioridadIncidente p, EstadoIncidente e, int horas) =>
            new()
            {
                Titulo = titulo, Descripcion = desc, Servidor = s, TipoIncidente = t,
                PrioridadIncidente = p, EstadoIncidente = e, FechaRegistro = ahora.AddHours(-horas)
            };

        var incidentes = new[]
        {
            Inc("API sin respuesta", "La API dejó de responder peticiones.", web01, caida, alta, resuelto, 30),
            Inc("CPU al 95%", "Uso de CPU sostenido durante 20 minutos.", web01, rendimiento, media, proceso, 6),
            Inc("Disco casi lleno", "El volumen de datos supera el 90%.", bd01, rendimiento, alta, abierto, 3),
            Inc("Intentos de acceso fallidos", "Múltiples intentos de login SSH.", bd01, seguridad, alta, abierto, 12),
            Inc("Respaldo no completado", "El respaldo nocturno terminó con error.", arch01, caida, media, proceso, 20),
            Inc("Latencia elevada", "Tiempos de respuesta superiores a 2 s.", arch01, rendimiento, baja, resuelto, 48)
        };

        // ---- Alertas ----
        var tipoCpu = new TipoAlerta { Nombre = "CPU alta", Descripcion = "Uso de CPU por encima del umbral" };
        var tipoMem = new TipoAlerta { Nombre = "Memoria baja", Descripcion = "Poca memoria disponible" };
        var tipoDisco = new TipoAlerta { Nombre = "Disco lleno", Descripcion = "Poco espacio libre" };
        var tipoCaida = new TipoAlerta { Nombre = "Servidor caído", Descripcion = "El servidor no responde" };

        var alertas = new[]
        {
            new Alerta { Titulo = "CPU alta en srv-web-01", Descripcion = "CPU > 90% por 10 min", Servidor = web01, TipoAlerta = tipoCpu },
            new Alerta { Titulo = "Memoria baja en srv-bd-01", Descripcion = "RAM libre < 10%", Servidor = bd01, TipoAlerta = tipoMem },
            new Alerta { Titulo = "Disco lleno en srv-bd-01", Descripcion = "Espacio libre < 10%", Servidor = bd01, TipoAlerta = tipoDisco },
            new Alerta { Titulo = "srv-archivos-01 sin respuesta", Descripcion = "3 chequeos consecutivos fallidos", Servidor = arch01, TipoAlerta = tipoCaida },
            new Alerta { Titulo = "CPU alta en srv-archivos-01", Descripcion = "CPU > 85%", Servidor = arch01, TipoAlerta = tipoCpu }
        };

        var canales = new[] { "Correo", "Slack", "Discord" };
        var notificaciones = new List<Notificacion>();
        var historial = new List<HistorialAlerta>();

        for (var i = 0; i < alertas.Length; i++)
        {
            notificaciones.Add(new Notificacion
            {
                Alerta = alertas[i],
                Mensaje = $"Nueva alerta: {alertas[i].Titulo}",
                Canal = canales[i % canales.Length],
                Enviada = i % 3 != 0,
                FechaRegistro = ahora.AddHours(-i - 1)
            });

            historial.Add(new HistorialAlerta
            {
                Alerta = alertas[i],
                Accion = "Alerta creada",
                Detalle = alertas[i].Descripcion,
                FechaRegistro = ahora.AddHours(-i - 1)
            });
        }

        historial.Add(new HistorialAlerta
        {
            Alerta = alertas[0],
            Accion = "Alerta reconocida",
            Detalle = "Atendida por el equipo de guardia",
            FechaRegistro = ahora.AddMinutes(-30)
        });

        db.AddRange(monitoreos);
        db.AddRange(incidentes);
        db.AddRange(alertas);
        db.AddRange(notificaciones);
        db.AddRange(historial);

        db.SaveChanges();
    }
}
