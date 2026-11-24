using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MonitorServidores.Data;
using MonitorServidores.Security;

namespace MonitorServidores.Controllers;

public record ActualizarRolesDto(List<string>? Roles);
public record ActualizarActivoDto(bool Activo);

/// <summary>
/// Administración de usuarios y roles. Solo el rol Administrador.
///   GET  /api/usuarios               lista de usuarios con sus roles
///   GET  /api/usuarios/roles         roles disponibles
///   PUT  /api/usuarios/{id}/roles    reemplaza los roles del usuario
///   PUT  /api/usuarios/{id}/activo   activa o desactiva al usuario
/// </summary>
[ApiController]
[Route("api/usuarios")]
[Authorize(Roles = Security.Roles.Administrador)]
public class UsuariosController : ControllerBase
{
    private readonly MonitorDbContext _context;

    public UsuariosController(MonitorDbContext context)
    {
        _context = context;
    }

    private int? IdActual() =>
        int.TryParse(User.FindFirst("usuario_id")?.Value, out var id) ? id : null;

    [HttpGet]
    public async Task<IActionResult> Listar()
    {
        var usuarios = await _context.Usuarios
            .AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new
            {
                u.Id,
                u.Proveedor,
                u.ProveedorId,
                u.Nombre,
                u.Email,
                u.Activo,
                u.FechaRegistro,
                u.UltimoAcceso,
                Roles = u.Roles.Select(r => r.Nombre).OrderBy(n => n).ToList()
            })
            .ToListAsync();

        return Ok(usuarios);
    }

    [HttpGet("roles")]
    public async Task<IActionResult> ListarRoles()
    {
        var roles = await _context.Roles
            .AsNoTracking()
            .OrderBy(r => r.Id)
            .Select(r => new { r.Id, r.Nombre, r.Descripcion })
            .ToListAsync();

        return Ok(roles);
    }

    [HttpPut("{id:int}/roles")]
    public async Task<IActionResult> ActualizarRoles(int id, [FromBody] ActualizarRolesDto datos)
    {
        var usuario = await _context.Usuarios
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (usuario is null)
            return NotFound(new { mensaje = "Usuario no encontrado." });

        var pedidos = (datos.Roles ?? new List<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var roles = await _context.Roles
            .Where(r => pedidos.Contains(r.Nombre))
            .ToListAsync();

        if (roles.Count != pedidos.Count)
            return BadRequest(new { mensaje = "Alguno de los roles indicados no existe." });

        if (id == IdActual() && !roles.Any(r => r.Nombre == Security.Roles.Administrador))
        {
            return BadRequest(new
            {
                mensaje = "No puedes quitarte a ti mismo el rol de Administrador."
            });
        }

        usuario.Roles.Clear();
        foreach (var rol in roles)
            usuario.Roles.Add(rol);

        await _context.SaveChangesAsync();

        return Ok(new
        {
            usuario.Id,
            Roles = roles.Select(r => r.Nombre).OrderBy(n => n).ToList()
        });
    }

    [HttpPut("{id:int}/activo")]
    public async Task<IActionResult> ActualizarActivo(int id, [FromBody] ActualizarActivoDto datos)
    {
        var usuario = await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id);

        if (usuario is null)
            return NotFound(new { mensaje = "Usuario no encontrado." });

        if (id == IdActual() && !datos.Activo)
            return BadRequest(new { mensaje = "No puedes desactivar tu propio usuario." });

        usuario.Activo = datos.Activo;
        await _context.SaveChangesAsync();

        return Ok(new { usuario.Id, usuario.Activo });
    }
}
