using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitorServidores.Data;
using MonitorServidores.Security;

namespace MonitorServidores.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize(Policy = Politicas.ReporteServidores)]
public class ReportesController : ControllerBase
{
    private readonly MonitorDbContext _context;

    public ReportesController(MonitorDbContext context)
    {
        _context = context;
    }

    // REPORTE 1: Servidores por sistema operativo
    [HttpGet("servidores-por-sistema")]
    public async Task<IActionResult> ServidoresPorSistema()
    {
        var reporte = await _context.Servidores
            .GroupBy(s => s.SistemaOperativoId)
            .Select(g => new
            {
                SistemaOperativoId = g.Key,
                CantidadServidores = g.Count()
            })
            .ToListAsync();

        return Ok(reporte);
    }

    // REPORTE 2: Monitoreos por servidor
    [HttpGet("monitoreos-por-servidor")]
    public async Task<IActionResult> MonitoreosPorServidor()
    {
        var reporte = await _context.Servidores
            .Select(s => new
            {
                ServidorId = s.Id,
                NombreServidor = s.Nombre,
                CantidadMonitoreos = _context.Monitoreos
                    .Count(m => m.ServidorId == s.Id)
            })
            .ToListAsync();

        return Ok(reporte);
    }

    // REPORTE 3: Servidores por tipo
    [HttpGet("servidores-por-tipo")]
    public async Task<IActionResult> ServidoresPorTipo()
    {
        var reporte = await _context.Servidores
            .GroupBy(s => s.TipoServidorId)
            .Select(g => new
            {
                TipoServidorId = g.Key,
                CantidadServidores = g.Count()
            })
            .ToListAsync();

        return Ok(reporte);
    }
}