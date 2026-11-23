using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Endpoints de Login Kit Web de TikTok. No requiere cambiar los controladores existentes.
public static class TikTokEndpoints
{
    public static void MapTikTokEndpoints(this WebApplication app)
    {
        // 1. Inicia OAuth, generando una protección contra CSRF de un solo uso.
        app.MapGet("/login/tiktok", IResult (HttpContext ctx, IConfiguration config) =>
        {
            var key = config["Authentication:TikTok:ClientKey"];
            var redirectUri = config["TIKTOK_REDIRECT_URI"];
            if (string.IsNullOrWhiteSpace(key) ||
                !Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || uri.AbsolutePath != "/signin-tiktok")
            {
                return Results.Problem("TikTok no está configurado correctamente.", statusCode: 503);
            }

            var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            ctx.Response.Cookies.Append("TikTokOAuthState", state, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/signin-tiktok",
                MaxAge = TimeSpan.FromMinutes(5)
            });

            var authorizationUrl = "https://www.tiktok.com/v2/auth/authorize/" +
                "?client_key=" + Uri.EscapeDataString(key) +
                "&response_type=code" +
                "&scope=user.info.basic" +
                "&redirect_uri=" + Uri.EscapeDataString(redirectUri) +
                "&state=" + Uri.EscapeDataString(state);

            return Results.Redirect(authorizationUrl);
        });

        // 2. TikTok devuelve el código, validamos el estado y creamos la sesión local.
        app.MapGet("/signin-tiktok", async Task<IResult> (
            HttpContext ctx, IConfiguration config, IHttpClientFactory factory) =>
        {
            var esperado = ctx.Request.Cookies["TikTokOAuthState"];
            var recibido = ctx.Request.Query["state"].ToString();
            ctx.Response.Cookies.Delete("TikTokOAuthState", new CookieOptions
            {
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/signin-tiktok"
            });

            if (string.IsNullOrEmpty(esperado) || string.IsNullOrEmpty(recibido) ||
                !CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(esperado), Encoding.UTF8.GetBytes(recibido)))
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

            var client = factory.CreateClient("TikTok");
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_key"] = key,
                ["client_secret"] = secret,
                ["code"] = code,
                ["grant_type"] = "authorization_code",
                ["redirect_uri"] = redirectUri
            });

            using var tokenResponse = await client.PostAsync(
                "https://open.tiktokapis.com/v2/oauth/token/", form);
            if (!tokenResponse.IsSuccessStatusCode)
                return Results.Problem("TikTok rechazó el intercambio del código OAuth.", statusCode: 502);

            using var tokens = JsonDocument.Parse(await tokenResponse.Content.ReadAsStringAsync());
            if (!tokens.RootElement.TryGetProperty("access_token", out var accessJson) ||
                !tokens.RootElement.TryGetProperty("open_id", out var openIdJson))
                return Results.Problem("TikTok no devolvió la identidad esperada.", statusCode: 502);

            var token = accessJson.GetString();
            var openId = openIdJson.GetString();
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(openId))
                return Results.Problem("TikTok devolvió credenciales incompletas.", statusCode: 502);

            using var userRequest = new HttpRequestMessage(HttpMethod.Get,
                "https://open.tiktokapis.com/v2/user/info/?fields=open_id,display_name");
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var userResponse = await client.SendAsync(userRequest);
            if (!userResponse.IsSuccessStatusCode)
                return Results.Problem("No fue posible obtener el perfil de TikTok.", statusCode: 502);

            using var profile = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync());
            if (!profile.RootElement.TryGetProperty("data", out var data) ||
                !data.TryGetProperty("user", out var user) ||
                !user.TryGetProperty("open_id", out var userIdJson) ||
                userIdJson.GetString() != openId)
                return Results.Problem("La identidad de TikTok no coincide.", statusCode: 502);

            string? name = user.TryGetProperty("display_name", out var displayName)
                ? displayName.GetString() : null;
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "tiktok:" + openId),
                new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(name) ? "Usuario de TikTok" : name),
                new Claim("proveedor", "TikTok")
            };
            var identity = new ClaimsIdentity(claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await ctx.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity), new AuthenticationProperties
                {
                    IsPersistent = false,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(2)
                });

            // No almacenamos ni enviamos tokens de TikTok al navegador.
            return Results.Redirect("/");
        });
    }
}