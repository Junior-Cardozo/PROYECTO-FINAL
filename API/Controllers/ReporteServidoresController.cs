using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitorServidores.Data;
using MonitorServidores.Security;

namespace MonitorServidores.Controllers;

/// <summary>
/// REPORTE: Disponibilidad y rendimiento de servidores
/// Autor:    Yasmin (módulo de servidores)
/// Tablas:   servidores, tipos_servidor, sistemas_operativos, monitoreos
/// Propósito: mostrar por servidor su tipo, sistema operativo, porcentaje de
///            disponibilidad, tiempo de respuesta promedio y último monitoreo.
/// Endpoint: GET /api/reportes/servidores
/// Permisos: sesión iniciada + rol Administrador o AnalistaServidores.
/// </summary>
[ApiController]
[Route("api/reportes/servidores")]
[Authorize(Policy = Politicas.ReporteServidores)]
public class ReporteServidoresController : ControllerBase
{
    private readonly MonitorDbContext _context;

    public ReporteServidoresController(MonitorDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var filas = await _context.Servidores
            .AsNoTracking()
            .Where(s => s.Activo)
            .OrderBy(s => s.Nombre)
            .Select(s => new
            {
                s.Id,
                s.Nombre,
                s.DireccionIp,
                Tipo = s.TipoServidor != null ? s.TipoServidor.Nombre : "Sin tipo",
                SistemaOperativo = s.SistemaOperativo != null
                    ? s.SistemaOperativo.Nombre : "Sin sistema operativo",
                VersionSistema = s.SistemaOperativo != null
                    ? s.SistemaOperativo.Version : null,
                TotalMonitoreos = s.Monitoreos.Count(m => m.Activo),
                MonitoreosDisponibles = s.Monitoreos
                    .Count(m => m.Activo && m.Disponible),
                TiempoRespuestaPromedioMs = s.Monitoreos
                    .Where(m => m.Activo && m.TiempoRespuestaMs != null)
                    .Average(m => (double?)m.TiempoRespuestaMs),
                UltimoMonitoreo = s.Monitoreos
                    .Where(m => m.Activo)
                    .OrderByDescending(m => m.FechaRegistro)
                    .Select(m => (DateTime?)m.FechaRegistro)
                    .FirstOrDefault(),
                UltimoEstadoDisponible = s.Monitoreos
                    .Where(m => m.Activo)
                    .OrderByDescending(m => m.FechaRegistro)
                    .Select(m => (bool?)m.Disponible)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var servidores = filas.Select(f => new
        {
            f.Id,
            f.Nombre,
            f.DireccionIp,
            f.Tipo,
            f.SistemaOperativo,
            f.VersionSistema,
            f.TotalMonitoreos,
            f.MonitoreosDisponibles,
            PorcentajeDisponibilidad = f.TotalMonitoreos == 0
                ? (double?)null
                : Math.Round(100.0 * f.MonitoreosDisponibles / f.TotalMonitoreos, 2),
            TiempoRespuestaPromedioMs = f.TiempoRespuestaPromedioMs.HasValue
                ? Math.Round(f.TiempoRespuestaPromedioMs.Value, 1)
                : (double?)null,
            f.UltimoMonitoreo,
            f.UltimoEstadoDisponible
        }).ToList();

        return Ok(new
        {
            reporte = "Disponibilidad y rendimiento de servidores",
            generadoUtc = DateTime.UtcNow,
            resumen = new
            {
                totalServidores = servidores.Count,
                servidoresDisponibles = servidores.Count(s => s.UltimoEstadoDisponible == true),
                servidoresCaidos = servidores.Count(s => s.UltimoEstadoDisponible == false),
                sinMonitoreo = servidores.Count(s => s.UltimoEstadoDisponible == null)
            },
            servidores
        });
    }
}
