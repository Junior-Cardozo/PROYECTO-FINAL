using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace MonitorServidores;

/// <summary>
/// Login Kit Web de TikTok.
///
/// TikTok solo acepta un Redirect URI con HTTPS (en desarrollo, el túnel ngrok),
/// pero la sesión del resto del sistema vive en http://localhost. Por eso el
/// acceso se hace en tres pasos:
///   1. GET /login/tiktok?returnUrl=...   → manda al usuario a TikTok. El destino
///      final viaja dentro del parámetro "state", firmado con Data Protection.
///   2. GET /signin-tiktok (URL del túnel) → TikTok regresa con el código; se
///      valida el state, se obtiene el perfil y se emite un ticket firmado que
///      vence en 1 minuto.
///   3. GET /signin-tiktok/completar (API_URL) → canjea el ticket, crea la cookie
///      de sesión en localhost y regresa al frontend.
/// Así la cookie queda en el mismo sitio que usan el frontend y el Portal de
/// Soporte (SSO) y no depende de la cookie del dominio del túnel.
/// </summary>
public static class TikTokEndpoints
{
    private const string PropositoState = "MonitorServidores.TikTok.State.v1";
    private const string PropositoTicket = "MonitorServidores.TikTok.Ticket.v1";

    private record EstadoTikTok(string Retorno);
    private record TicketTikTok(string OpenId, string Nombre, string Retorno);

    public static void MapTikTokEndpoints(this WebApplication app)
    {
        // 1. Inicio del flujo OAuth
        app.MapGet("/login/tiktok", IResult (
            IConfiguration config, IDataProtectionProvider proteccion, string? returnUrl) =>
        {
            var key = config["Authentication:TikTok:ClientKey"];
            var redirectUri = config["TIKTOK_REDIRECT_URI"];

            if (string.IsNullOrWhiteSpace(key) ||
                !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/signin-tiktok")
            {
                return Results.Problem("TikTok no está configurado correctamente.", statusCode: 503);
            }

            var retorno = !string.IsNullOrWhiteSpace(returnUrl) && RetornoPermitido(config, returnUrl)
                ? returnUrl
                : "/";

            var state = Limitado(proteccion, PropositoState).Protect(
                JsonSerializer.Serialize(new EstadoTikTok(retorno)),
                TimeSpan.FromMinutes(5));

            var autorizacion = "https://www.tiktok.com/v2/auth/authorize/" +
                "?client_key=" + Uri.EscapeDataString(key) +
                "&response_type=code" +
                "&scope=user.info.basic" +
                "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                "&state=" + Uri.EscapeDataString(state);

            return Results.Redirect(autorizacion);
        });

        // 2. TikTok devuelve el código
        app.MapGet("/signin-tiktok", async Task<IResult> (
            HttpContext ctx, IConfiguration config,
            IHttpClientFactory fabrica, IDataProtectionProvider proteccion) =>
        {
            EstadoTikTok? estado;
            try
            {
                var recibido = ctx.Request.Query["state"].ToString();
                estado = JsonSerializer.Deserialize<EstadoTikTok>(
                    Limitado(proteccion, PropositoState).Unprotect(recibido));
            }
            catch
            {
                estado = null;
            }

            if (estado is null)
                return Results.BadRequest("Estado de inicio de sesión inválido o vencido.");

            if (!string.IsNullOrEmpty(ctx.Request.Query["error"].ToString()))
                return Results.BadRequest("TikTok no autorizó el inicio de sesión.");

            var code = ctx.Request.Query["code"].ToString();
            var key = config["Authentication:TikTok:ClientKey"];
            var secret = config["Authentication:TikTok:ClientSecret"];
            var redirectUri = config["TIKTOK_REDIRECT_URI"];

            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(key) ||
                string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(redirectUri))
                return Results.BadRequest("Faltan datos para completar el inicio de sesión.");

            var cliente = fabrica.CreateClient("TikTok");

            using var formulario = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_key"] = key,
                ["client_secret"] = secret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            });

            using var respuestaToken = await cliente.PostAsync(
                "https://open.tiktokapis.com/v2/oauth/token/", formulario);

            // TikTok puede devolver un JSON de error en vez de access_token/open_id.
            // No escribimos en logs ni mostramos el cuerpo completo porque podría
            // contener tokens o información sensible.
            JsonDocument documentoToken;
            try
            {
                documentoToken = JsonDocument.Parse(
                    await respuestaToken.Content.ReadAsStringAsync());
            }
            catch (JsonException)
            {
                app.Logger.LogWarning("TikTok OAuth devolvió contenido no JSON. HTTP {Estado}",
                    (int)respuestaToken.StatusCode);
                return Results.Problem(
                    "TikTok devolvió una respuesta inesperada al solicitar el token.",
                    statusCode: 502);
            }

            using var tokens = documentoToken;
            var raizToken = tokens.RootElement;
            if (raizToken.ValueKind != JsonValueKind.Object)
                return Results.Problem("Respuesta OAuth de TikTok no válida.", statusCode: 502);

            // Capturamos el tipo de error, pero NO el client_secret, code ni access_token.
            if (raizToken.TryGetProperty("error", out var errorTikTok))
            {
                var codigoError = errorTikTok.ValueKind == JsonValueKind.String
                    ? errorTikTok.GetString() : null;
                if (!string.IsNullOrWhiteSpace(codigoError))
                {
                    app.Logger.LogWarning(
                        "TikTok OAuth rechazó el intercambio: {Error}, HTTP {Estado}",
                        codigoError, (int)respuestaToken.StatusCode);
                    return Results.Problem($"Error OAuth de TikTok: {codigoError}",
                        statusCode: 502);
                }
            }

            if (!respuestaToken.IsSuccessStatusCode)
            {
                app.Logger.LogWarning("TikTok OAuth HTTP {Estado} sin código de error reconocible",
                    (int)respuestaToken.StatusCode);
                return Results.Problem("TikTok rechazó el intercambio del código OAuth.",
                    statusCode: 502);
            }

            if (!raizToken.TryGetProperty("access_token", out var accesoJson) ||
                accesoJson.ValueKind != JsonValueKind.String ||
                !raizToken.TryGetProperty("open_id", out var openIdJson) ||
                openIdJson.ValueKind != JsonValueKind.String)
            {
                app.Logger.LogWarning(
                    "TikTok OAuth respondió HTTP {Estado}, pero no incluyó access_token y open_id",
                    (int)respuestaToken.StatusCode);
                return Results.Problem("TikTok no devolvió la identidad esperada.",
                    statusCode: 502);
            }

            var token = accesoJson.GetString();
            var openId = openIdJson.GetString();
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(openId))
                return Results.Problem("TikTok devolvió credenciales incompletas.", statusCode: 502);

            using var peticionPerfil = new HttpRequestMessage(HttpMethod.Get,
                "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name");
            peticionPerfil.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var respuestaPerfil = await cliente.SendAsync(peticionPerfil);
            if (!respuestaPerfil.IsSuccessStatusCode)
                return Results.Problem("No fue posible obtener el perfil de TikTok.", statusCode: 502);

            using var perfil = JsonDocument.Parse(await respuestaPerfil.Content.ReadAsStringAsync());
            if (!perfil.RootElement.TryGetProperty("data", out var datos) ||
                !datos.TryGetProperty("user", out var usuario) ||
                !usuario.TryGetProperty("open_id", out var idPerfilJson) ||
                idPerfilJson.GetString() != openId)
                return Results.Problem("La identidad de TikTok no coincide.", statusCode: 502);

            var nombre = usuario.TryGetProperty("display_name", out var nombreJson)
                ? nombreJson.GetString()
                : null;

            // Los tokens de TikTok no se guardan ni se envían al navegador.
            var ticket = Limitado(proteccion, PropositoTicket).Protect(
                JsonSerializer.Serialize(new TicketTikTok(
                    openId,
                    string.IsNullOrWhiteSpace(nombre) ? "Usuario de TikTok" : nombre,
                    estado.Retorno)),
                TimeSpan.FromMinutes(1));

            // La sesión se crea en la dirección pública de la API (localhost).
            var api = (config["Api:PublicUrl"] ?? "http://localhost:8080").TrimEnd('/');
            return Results.Redirect(
                $"{api}/signin-tiktok/completar?ticket={Uri.EscapeDataString(ticket)}");
        });

        // 3. Canje del ticket y creación de la sesión local
        app.MapGet("/signin-tiktok/completar", async Task<IResult> (
            HttpContext ctx, IConfiguration config,
            IDataProtectionProvider proteccion, string? ticket) =>
        {
            TicketTikTok? datos;
            try
            {
                datos = JsonSerializer.Deserialize<TicketTikTok>(
                    Limitado(proteccion, PropositoTicket).Unprotect(ticket ?? string.Empty));
            }
            catch
            {
                datos = null;
            }

            if (datos is null)
                return Results.BadRequest("El ticket de inicio de sesión es inválido o venció.");

            var identidad = new ClaimsIdentity(
                new[]
                {
                    // Id estable del usuario: queda como "TikTok:<open_id>"
                    new Claim(ClaimTypes.NameIdentifier, datos.OpenId),
                    new Claim(ClaimTypes.Name, datos.Nombre),
                    new Claim("proveedor", "TikTok")
                },
                CookieAuthenticationDefaults.AuthenticationScheme);

            await ctx.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identidad),
                new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                });

            var destino = RetornoPermitido(config, datos.Retorno) ? datos.Retorno : "/";
            return Results.Redirect(destino);
        });
    }

    private static ITimeLimitedDataProtector Limitado(IDataProtectionProvider proveedor, string proposito) =>
        proveedor.CreateProtector(proposito).ToTimeLimitedDataProtector();

    /// <summary>Solo rutas locales o los orígenes de Cors:AllowedOrigins (evita redirecciones abiertas).</summary>
    private static bool RetornoPermitido(IConfiguration config, string url)
    {
        if (url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\"))
            return true;

        var permitidos = (config["Cors:AllowedOrigins"] ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(o => o.TrimEnd('/'));

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && permitidos.Contains(uri.GetLeftPart(UriPartial.Authority), StringComparer.OrdinalIgnoreCase);
    }
}