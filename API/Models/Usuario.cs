using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MonitorServidores.Models;

/// <summary>
/// Usuario del sistema. Se crea automáticamente la primera vez que alguien
/// inicia sesión con cualquiera de los proveedores (sin roles = sin permisos).
/// </summary>
[Table("usuarios")]
public class Usuario
{
    public int Id { get; set; }

    [Required]
    [MaxLength(50)]
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>Id del usuario en el proveedor externo.</summary>
    [Required]
    [MaxLength(200)]
    public string ProveedorId { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Nombre { get; set; }

    [MaxLength(200)]
    public string? Email { get; set; }

    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public DateTime UltimoAcceso { get; set; } = DateTime.UtcNow;

    public bool Activo { get; set; } = true;

    public ICollection<Rol> Roles { get; set; } = new List<Rol>();
}
