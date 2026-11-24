using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitorServidores.Data;
using MonitorServidores.Security;

namespace MonitorServidores.Controllers;

/// <summary>
/// REPORTE: Alertas y notificaciones
/// Autor:    Diego (módulo de alertas)
/// Tablas:   alertas, tipos_alerta, notificaciones, historial_alertas, servidores
/// Propósito: ver alertas activas por tipo y servidor, el estado de envío de
///            notificaciones y los últimos movimientos del historial.
/// Endpoint: GET /api/reportes/alertas
/// Permisos: sesión iniciada + rol Administrador o AnalistaAlertas.
/// </summary>
[ApiController]
[Route("api/reportes/alertas")]
[Authorize(Policy = Politicas.ReporteAlertas)]
public class ReporteAlertasController : ControllerBase
{
    private readonly MonitorDbContext _context;

    public ReporteAlertasController(MonitorDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var alertas = _context.Alertas
            .AsNoTracking()
            .Where(a => a.Activo);

        var totalAlertas = await alertas.CountAsync();

        var porTipo = await alertas
            .GroupBy(a => a.TipoAlerta.Nombre)
            .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();

        var porServidor = await alertas
            .GroupBy(a => a.Servidor.Nombre)
            .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();

        var notificaciones = _context.Notificaciones
            .AsNoTracking()
            .Where(n => n.Activo);

        var totalNotificaciones = await notificaciones.CountAsync();
        var enviadas = await notificaciones.CountAsync(n => n.Enviada);

        var porCanal = await notificaciones
            .GroupBy(n => n.Canal)
            .Select(g => new
            {
                Nombre = g.Key,
                Cantidad = g.Count(),
                Enviadas = g.Count(n => n.Enviada)
            })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();

        var ultimosMovimientos = await _context.HistorialAlertas
            .AsNoTracking()
            .Where(h => h.Activo)
            .OrderByDescending(h => h.FechaRegistro)
            .Take(10)
            .Select(h => new
            {
                h.Id,
                Alerta = h.Alerta.Titulo,
                Servidor = h.Alerta.Servidor.Nombre,
                h.Accion,
                h.Detalle,
                h.FechaRegistro
            })
            .ToListAsync();

        return Ok(new
        {
            reporte = "Alertas y notificaciones",
            generadoUtc = DateTime.UtcNow,
            totalAlertas,
            porTipo,
            porServidor,
            notificaciones = new
            {
                total = totalNotificaciones,
                enviadas,
                pendientes = totalNotificaciones - enviadas,
                porCanal
            },
            ultimosMovimientos
        });
    }
}
