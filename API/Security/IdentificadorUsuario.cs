using System.Security.Claims;

namespace MonitorServidores.Security;

public static class IdentificadorUsuario
{
    public static (string Proveedor, string IdExterno) Partes(ClaimsPrincipal usuario)
    {
        var proveedor = usuario.FindFirst("proveedor")?.Value ?? "desconocido";
        var id = usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value
              ?? usuario.FindFirst("sub")?.Value
              ?? usuario.Identity?.Name
              ?? "sin-id";

        return (proveedor, id);
    }

    /// <summary>Identificador estable del usuario: "proveedor:id".</summary>
    public static string Obtener(ClaimsPrincipal usuario)
    {
        var (proveedor, id) = Partes(usuario);
        return $"{proveedor}:{id}";
    }
}
