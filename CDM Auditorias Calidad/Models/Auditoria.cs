namespace CDM_Auditorias_Calidad.Models;

/// <summary>
/// Una fila de la tabla «Auditorias» del Power BI: una auditoría de una llamada o chat.
/// </summary>
/// <param name="Respuesta">Nota de calidad entre 0 y 1 (0,85 = 85 %). Puede faltar.</param>
/// <param name="Base">Origen: <c>WEB</c> (formularios de WhatsApp, Jazztel y Orange) o <c>ICEBERG</c>.</param>
public sealed record Auditoria(
    DateOnly Fecha,
    long? Legajo,
    string? Sector,
    string? Super,
    string? Team,
    string? Agente,
    string? IdLlamada,
    string? CorreoAuditor,
    string? NombreAuditor,
    string? CargoAuditor,
    double? Respuesta,
    string? Base);

/// <summary>
/// Un agente en nómina un día (<c>Consultas/Nomina.sql</c>), para la tarjeta «Total agentes».
/// </summary>
/// <param name="Cargo">Cargo en NominaAntiguedad (<c>car_descrip</c>): «Agente», «Team Leader»…</param>
public sealed record RegistroNomina(DateOnly Fecha, long Legajo, string? Sector, string? Super, string? Team, string? Cargo);

/// <summary>
/// Todas las auditorías leídas en una ejecución de la consulta, y la nómina de esas fechas.
/// </summary>
public sealed class InstantaneaAuditorias
{
    public InstantaneaAuditorias(
        IReadOnlyList<Auditoria> filas,
        DateTime cargadoEn,
        TimeSpan duracion,
        IReadOnlyList<RegistroNomina>? nomina = null,
        string? errorNomina = null,
        IReadOnlyList<AuditoriaT0>? t0 = null,
        string? errorT0 = null,
        IReadOnlyDictionary<DateOnly, int>? planesPorDia = null)
    {
        Filas = filas;
        CargadoEn = cargadoEn;
        Duracion = duracion;
        Nomina = nomina;
        ErrorNomina = errorNomina;
        T0 = t0;
        ErrorT0 = errorT0;
        PlanesPorDia = planesPorDia;
        if (filas.Count > 0)
        {
            PrimeraFecha = filas.Min(f => f.Fecha);
            UltimaFecha = filas.Max(f => f.Fecha);
        }
    }

    public IReadOnlyList<Auditoria> Filas { get; }

    /// <summary>La nómina del rango del calendario; null si no se pudo leer (la tarjeta sale sin dato).</summary>
    public IReadOnlyList<RegistroNomina>? Nomina { get; }

    public string? ErrorNomina { get; }

    /// <summary>Las auditorías ICEBERG con Tolerancia 0 y su alerta y plan (<c>Consultas/AuditoriasT0.sql</c>); null si no se pudo leer.</summary>
    public IReadOnlyList<AuditoriaT0>? T0 { get; }

    public string? ErrorT0 { get; }

    /// <summary>Planes creados cada día en <c>Legal.PlanAccion</c> en la ventana (los días sin ninguno no están): para ver sus huecos.</summary>
    public IReadOnlyDictionary<DateOnly, int>? PlanesPorDia { get; }
    public DateTime CargadoEn { get; }
    public TimeSpan Duracion { get; }

    /// <summary>
    /// Rango de la tabla «Calendario» del Power BI:
    /// <c>CALENDAR(MIN(Auditorias[Fecha]), MAX(Auditorias[Fecha]))</c>, sobre todas las filas.
    /// </summary>
    public DateOnly? PrimeraFecha { get; }
    public DateOnly? UltimaFecha { get; }
}
