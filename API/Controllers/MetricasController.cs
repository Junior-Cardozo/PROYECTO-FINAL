using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MonitorServidores.Security;
using System.Globalization;
using System.Text.Json;

namespace MonitorServidores.Controllers;

/// <summary>
/// Métricas del servidor obtenidas de Prometheus (que a su vez las recoge de
/// Node Exporter). El navegador nunca habla con Prometheus: solo con esta API,
/// que valida el rol en el backend.
///   GET /api/metricas/servidor    valores actuales (CPU, RAM, disco, uptime, disponibilidad)
///   GET /api/metricas/historial   series de CPU y RAM de los últimos minutos
///   GET /api/metricas/objetivos   estado de los objetivos que scrapea Prometheus
/// Permisos: rol Administrador o AnalistaServidores.
/// </summary>
[ApiController]
[Route("api/metricas")]
[Authorize(Policy = Politicas.Metricas)]
public class MetricasController : ControllerBase
{
    // Consultas PromQL
    private const string CpuPorcentaje =
        "100 - (avg(rate(node_cpu_seconds_total{mode=\"idle\"}[1m])) * 100)";
    private const string RamPorcentaje =
        "(1 - sum(node_memory_MemAvailable_bytes) / sum(node_memory_MemTotal_bytes)) * 100";
    private const string RamTotal = "sum(node_memory_MemTotal_bytes)";
    private const string RamDisponible = "sum(node_memory_MemAvailable_bytes)";
    private const string DiscoTotal = "max(node_filesystem_size_bytes{mountpoint=\"/\"})";
    private const string DiscoDisponible = "max(node_filesystem_avail_bytes{mountpoint=\"/\"})";
    private const string Uptime = "time() - max(node_boot_time_seconds)";
    private const string Carga1m = "max(node_load1)";
    private const string Nucleos = "count(count by (cpu) (node_cpu_seconds_total{mode=\"idle\"}))";
    private const string NodeActivo = "max(up{job=\"node-exporter\"})";

    private readonly IHttpClientFactory _fabrica;

    public MetricasController(IHttpClientFactory fabrica)
    {
        _fabrica = fabrica;
    }

    [HttpGet("servidor")]
    public async Task<IActionResult> Servidor(CancellationToken ct)
    {
        try
        {
            var c = _fabrica.CreateClient("prometheus");

            var activo = await ValorAsync(c, NodeActivo, ct);
            var cpu = await ValorAsync(c, CpuPorcentaje, ct);
            var ramPct = await ValorAsync(c, RamPorcentaje, ct);
            var ramTotal = await ValorAsync(c, RamTotal, ct);
            var ramLibre = await ValorAsync(c, RamDisponible, ct);
            var discoTotal = await ValorAsync(c, DiscoTotal, ct);
            var discoLibre = await ValorAsync(c, DiscoDisponible, ct);
            var uptime = await ValorAsync(c, Uptime, ct);
            var carga = await ValorAsync(c, Carga1m, ct);
            var nucleos = await ValorAsync(c, Nucleos, ct);

            double? discoPct = discoTotal is > 0 && discoLibre is not null
                ? (1 - discoLibre / discoTotal) * 100
                : null;

            return Ok(new
            {
                actualizadoUtc = DateTime.UtcNow,
                disponible = activo is null ? (bool?)null : activo == 1,
                cpuPorcentaje = Redondear(cpu),
                ramPorcentaje = Redondear(ramPct),
                ramTotalBytes = ramTotal,
                ramUsadaBytes = ramTotal is not null && ramLibre is not null ? ramTotal - ramLibre : null,
                discoPorcentaje = Redondear(discoPct),
                discoTotalBytes = discoTotal,
                discoUsadoBytes = discoTotal is not null && discoLibre is not null ? discoTotal - discoLibre : null,
                uptimeSegundos = uptime,
                cargaPromedio1m = Redondear(carga),
                nucleosCpu = nucleos
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return PrometheusNoDisponible();
        }
    }

    [HttpGet("historial")]
    public async Task<IActionResult> Historial([FromQuery] int minutos = 15, CancellationToken ct = default)
    {
        minutos = Math.Clamp(minutos, 5, 180);
        var paso = Math.Max(15, minutos * 60 / 60);

        try
        {
            var c = _fabrica.CreateClient("prometheus");

            return Ok(new
            {
                minutos,
                pasoSegundos = paso,
                cpu = await SerieAsync(c, CpuPorcentaje, minutos, paso, ct),
                ram = await SerieAsync(c, RamPorcentaje, minutos, paso, ct)
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return PrometheusNoDisponible();
        }
    }

    [HttpGet("objetivos")]
    public async Task<IActionResult> Objetivos(CancellationToken ct)
    {
        try
        {
            var c = _fabrica.CreateClient("prometheus");
            using var respuesta = await c.GetAsync("/api/v1/targets", ct);
            respuesta.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));

            var objetivos = doc.RootElement
                .GetProperty("data")
                .GetProperty("activeTargets")
                .EnumerateArray()
                .Select(t => new
                {
                    job = t.GetProperty("labels").GetProperty("job").GetString(),
                    url = t.GetProperty("scrapeUrl").GetString(),
                    estado = t.GetProperty("health").GetString()
                })
                .ToList();

            return Ok(objetivos);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return PrometheusNoDisponible();
        }
    }

    // ---------------- Utilidades ----------------

    private ObjectResult PrometheusNoDisponible() =>
        StatusCode(503, new { mensaje = "Prometheus no está disponible." });

    private static double? Redondear(double? valor) =>
        valor.HasValue ? Math.Round(valor.Value, 1) : null;

    private static async Task<double?> ValorAsync(HttpClient cliente, string consulta, CancellationToken ct)
    {
        using var respuesta = await cliente.GetAsync(
            $"/api/v1/query?query={Uri.EscapeDataString(consulta)}", ct);
        respuesta.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        var resultado = doc.RootElement.GetProperty("data").GetProperty("result");

        if (resultado.GetArrayLength() == 0)
            return null;

        var texto = resultado[0].GetProperty("value")[1].GetString();

        return double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor)
               && double.IsFinite(valor)
            ? valor
            : null;
    }

    private static async Task<List<object>> SerieAsync(
        HttpClient cliente, string consulta, int minutos, int paso, CancellationToken ct)
    {
        var fin = DateTimeOffset.UtcNow;
        var inicio = fin.AddMinutes(-minutos);

        var url = "/api/v1/query_range"
            + $"?query={Uri.EscapeDataString(consulta)}"
            + $"&start={inicio.ToUnixTimeSeconds()}"
            + $"&end={fin.ToUnixTimeSeconds()}"
            + $"&step={paso}";

        using var respuesta = await cliente.GetAsync(url, ct);
        respuesta.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync(ct));
        var resultado = doc.RootElement.GetProperty("data").GetProperty("result");

        var puntos = new List<object>();

        if (resultado.GetArrayLength() == 0)
            return puntos;

        foreach (var par in resultado[0].GetProperty("values").EnumerateArray())
        {
            var marca = par[0].GetDouble();
            var texto = par[1].GetString();

            if (double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var valor)
                && double.IsFinite(valor))
            {
                puntos.Add(new
                {
                    t = DateTimeOffset.FromUnixTimeSeconds((long)marca),
                    v = Math.Round(valor, 1)
                });
            }
        }

        return puntos;
    }
}
