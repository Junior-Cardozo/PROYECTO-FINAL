using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonitorServidores.Security;

namespace MonitorServidores.Controllers;

/// <summary>
/// Catálogo de reportes: autor, tablas, propósito, endpoint y permisos.
/// El campo "permitido" indica si el usuario actual puede consultarlo
/// (solo informativo para la interfaz; la validación real la hace cada
/// endpoint en el backend).
/// </summary>
[ApiController]
[Route("api/reportes")]
[Authorize]
public class ReportesCatalogoController : ControllerBase
{
    private readonly IAuthorizationService _autorizacion;

    public ReportesCatalogoController(IAuthorizationService autorizacion)
    {
        _autorizacion = autorizacion;
    }

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var definiciones = new[]
        {
            new
            {
                Clave = "servidores",
                Titulo = "Disponibilidad y rendimiento de servidores",
                Autor = "Yasmin",
                Tablas = new[] { "servidores", "tipos_servidor", "sistemas_operativos", "monitoreos" },
                Proposito = "Disponibilidad, tiempo de respuesta y último monitoreo por servidor.",
                Endpoint = "/api/reportes/servidores",
                Permisos = new[] { Roles.Administrador, Roles.AnalistaServidores },
                Politica = Politicas.ReporteServidores
            },
            new
            {
                Clave = "incidentes",
                Titulo = "Resumen de incidentes",
                Autor = "Junior Cardozo",
                Tablas = new[] { "Incidentes", "TiposIncidente", "PrioridadesIncidente", "EstadosIncidente", "servidores" },
                Proposito = "Incidentes por estado, prioridad, tipo y servidor, más los recientes.",
                Endpoint = "/api/reportes/incidentes",
                Permisos = new[] { Roles.Administrador, Roles.AnalistaIncidentes },
                Politica = Politicas.ReporteIncidentes
            },
            new
            {
                Clave = "alertas",
                Titulo = "Alertas y notificaciones",
                Autor = "Diego",
                Tablas = new[] { "alertas", "tipos_alerta", "notificaciones", "historial_alertas", "servidores" },
                Proposito = "Alertas activas, estado de notificaciones y últimos movimientos.",
                Endpoint = "/api/reportes/alertas",
                Permisos = new[] { Roles.Administrador, Roles.AnalistaAlertas },
                Politica = Politicas.ReporteAlertas
            }
        };

        var resultado = new List<object>();

        foreach (var d in definiciones)
        {
            var evaluacion = await _autorizacion.AuthorizeAsync(User, null, d.Politica);

            resultado.Add(new
            {
                d.Clave,
                d.Titulo,
                d.Autor,
                d.Tablas,
                d.Proposito,
                d.Endpoint,
                d.Permisos,
                Permitido = evaluacion.Succeeded
            });
        }

        return Ok(resultado);
    }
}
