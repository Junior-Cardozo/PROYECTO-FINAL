using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using MonitorServidores.Data;
using MonitorServidores.Models;
using System.Security.Claims;

namespace MonitorServidores.Security;

/// <summary>
/// En cada petición autenticada:
///  1. Busca al usuario en la base de datos (o lo registra si es su primer acceso).
///  2. Agrega a la identidad los roles que tiene guardados en la base de datos.
/// Como se lee de PostgreSQL, un cambio de roles se aplica al instante y es
/// igual en todas las instancias del backend.
///
/// Administradores iniciales: Seguridad:AdministradoresIniciales
/// (correos o "proveedor:id") solo sirve para crear al primer administrador;
/// después los roles se gestionan desde el panel.
/// </summary>
public class RolesClaimsTransformation : IClaimsTransformation
{
    private const string MarcaCargado = "roles_cargados";

    private readonly MonitorDbContext _db;
    private readonly IConfiguration _configuracion;

    public RolesClaimsTransformation(MonitorDbContext db, IConfiguration configuracion)
    {
        _db = db;
        _configuracion = configuracion;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity { IsAuthenticated: true })
            return principal;

        if (principal.HasClaim(c => c.Type == MarcaCargado))
            return principal;

        var (proveedor, idExterno) = IdentificadorUsuario.Partes(principal);
        var nombre = principal.Identity?.Name;
        var correo = principal.FindFirst(ClaimTypes.Email)?.Value;

        var usuario = await ObtenerOCrearAsync(proveedor, idExterno, nombre, correo);

        var clon = principal.Clone();
        var identidad = clon.Identities.First();

        identidad.AddClaim(new Claim(MarcaCargado, "1"));
        identidad.AddClaim(new Claim("usuario_id", usuario.Id.ToString()));

        if (usuario.Activo)
        {
            foreach (var rol in usuario.Roles)
                identidad.AddClaim(new Claim(ClaimTypes.Role, rol.Nombre));
        }

        return clon;
    }

    private async Task<Usuario> ObtenerOCrearAsync(
        string proveedor, string idExterno, string? nombre, string? correo)
    {
        var usuario = await BuscarAsync(proveedor, idExterno);
        var esInicial = EsAdministradorInicial(proveedor, idExterno, correo);

        if (usuario is null)
        {
            usuario = new Usuario
            {
                Proveedor = proveedor,
                ProveedorId = idExterno,
                Nombre = nombre,
                Email = correo
            };

            if (esInicial)
            {
                var admin = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == Roles.Administrador);
                if (admin is not null) usuario.Roles.Add(admin);
            }

            _db.Usuarios.Add(usuario);

            try
            {
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Otra instancia lo registró al mismo tiempo: se vuelve a leer.
                _db.ChangeTracker.Clear();
                usuario = await BuscarAsync(proveedor, idExterno)
                    ?? throw new InvalidOperationException("No se pudo registrar al usuario.");
            }

            return usuario;
        }

        var cambio = false;

        if (esInicial && !usuario.Roles.Any(r => r.Nombre == Roles.Administrador))
        {
            var admin = await _db.Roles.FirstOrDefaultAsync(r => r.Nombre == Roles.Administrador);
            if (admin is not null)
            {
                usuario.Roles.Add(admin);
                cambio = true;
            }
        }

        if (DateTime.UtcNow - usuario.UltimoAcceso > TimeSpan.FromMinutes(10))
        {
            usuario.UltimoAcceso = DateTime.UtcNow;
            usuario.Nombre = nombre ?? usuario.Nombre;
            usuario.Email = correo ?? usuario.Email;
            cambio = true;
        }

        if (cambio)
            await _db.SaveChangesAsync();

        return usuario;
    }

    private Task<Usuario?> BuscarAsync(string proveedor, string idExterno) =>
        _db.Usuarios
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Proveedor == proveedor && u.ProveedorId == idExterno);

    private bool EsAdministradorInicial(string proveedor, string idExterno, string? correo)
    {
        var lista = (_configuracion["Seguridad:AdministradoresIniciales"] ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return lista.Any(x =>
            x.Equals($"{proveedor}:{idExterno}", StringComparison.OrdinalIgnoreCase)
            || (correo is not null && x.Equals(correo, StringComparison.OrdinalIgnoreCase)));
    }
}
