using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitorServidores.Data;
using MonitorServidores.Security;

namespace MonitorServidores.Controllers;

/// <summary>
/// REPORTE: Resumen de incidentes
/// Autor:    Junior Cardozo (módulo de incidentes)
/// Tablas:   Incidentes, TiposIncidente, PrioridadesIncidente,
///           EstadosIncidente, servidores
/// Propósito: ver cuántos incidentes hay por estado, prioridad, tipo y
///            servidor, y los más recientes.
/// Endpoint: GET /api/reportes/incidentes
/// Permisos: sesión iniciada + rol Administrador o AnalistaIncidentes.
/// </summary>
[ApiController]
[Route("api/reportes/incidentes")]
[Authorize(Policy = Politicas.ReporteIncidentes)]
public class ReporteIncidentesController : ControllerBase
{
    private readonly MonitorDbContext _context;

    public ReporteIncidentesController(MonitorDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var incidentes = _context.Incidentes
            .AsNoTracking()
            .Where(i => !i.Eliminado);

        var total = await incidentes.CountAsync();

        var porEstado = await incidentes
            .GroupBy(i => i.EstadoIncidente.Nombre)
            .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();

        var porPrioridad = await incidentes
            .GroupBy(i => i.PrioridadIncidente.Nombre)
            .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();

        var porTipo = await incidentes
            .GroupBy(i => i.TipoIncidente.Nombre)
            .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();

        var porServidor = await incidentes
            .GroupBy(i => i.Servidor.Nombre)
            .Select(g => new { Nombre = g.Key, Cantidad = g.Count() })
            .OrderByDescending(x => x.Cantidad)
            .ToListAsync();

        var recientes = await incidentes
            .OrderByDescending(i => i.FechaRegistro)
            .Take(10)
            .Select(i => new
            {
                i.Id,
                i.Titulo,
                Servidor = i.Servidor.Nombre,
                Tipo = i.TipoIncidente.Nombre,
                Prioridad = i.PrioridadIncidente.Nombre,
                Estado = i.EstadoIncidente.Nombre,
                i.FechaRegistro
            })
            .ToListAsync();

        return Ok(new
        {
            reporte = "Resumen de incidentes",
            generadoUtc = DateTime.UtcNow,
            totalIncidentes = total,
            porEstado,
            porPrioridad,
            porTipo,
            porServidor,
            recientes
        });
    }
}
