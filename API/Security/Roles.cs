namespace MonitorServidores.Security;

/// <summary>
/// Nombres de los roles del sistema. Los usuarios y sus roles se guardan en
/// PostgreSQL (tablas usuarios, roles, usuarios_roles) y los administra un
/// Administrador desde el panel del frontend (api/usuarios).
/// </summary>
public static class Roles
{
    public const string Administrador = "Administrador";
    public const string AnalistaServidores = "AnalistaServidores";
    public const string AnalistaIncidentes = "AnalistaIncidentes";
    public const string AnalistaAlertas = "AnalistaAlertas";

    public static readonly (string Nombre, string Descripcion)[] Definiciones =
    {
        (Administrador, "Acceso total y administración de usuarios y roles."),
        (AnalistaServidores, "Reporte de servidores y métricas de Prometheus."),
        (AnalistaIncidentes, "Reporte de incidentes."),
        (AnalistaAlertas, "Reporte de alertas y notificaciones.")
    };

    public static IEnumerable<string> Todos => Definiciones.Select(d => d.Nombre);
}

public static class Politicas
{
    public const string ReporteServidores = "ReporteServidores";
    public const string ReporteIncidentes = "ReporteIncidentes";
    public const string ReporteAlertas = "ReporteAlertas";
    public const string Metricas = "Metricas";
}
