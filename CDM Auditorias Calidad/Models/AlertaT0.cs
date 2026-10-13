namespace CDM_Auditorias_Calidad.Models;

/// <summary>
/// Una auditoría de calidad de ICEBERG con afectación «Tolerancia 0» (primer resultado de
/// <c>Consultas/AuditoriasT0.sql</c>) y su fila de <c>[Reporting].[pdc].[Reportar_T0]</c>, si está.
/// La regla de negocio: cada auditoría con T0 tiene que estar reportada en Reportar_T0 (y allí, con plan de acción).
/// </summary>
/// <param name="Infraccion">El texto de la columna «Tolerancia 0» de la plantilla.</param>
/// <param name="IdReporte">El <c>ID</c> de Reportar_T0 que le corresponde; null si no está reportada.</param>
/// <param name="Cruce">Cómo se encontró: «Llamada» (mismo ID de llamada) o «Agente, auditor y fecha».</param>
public sealed record AuditoriaT0(
    DateOnly Fecha,
    long? Legajo,
    string? Sector,
    string? Super,
    string? Team,
    string? Agente,
    long IdGestion,
    string? IdLlamada,
    long? LegajoAuditor,
    string? NombreAuditor,
    string? CargoAuditor,
    string? Plantilla,
    string? Infraccion,
    double? Nota,
    long? IdReporte,
    string? Cruce)
{
    /// <summary>La fila de Reportar_T0 (la pone el repositorio al leer el segundo resultado).</summary>
    public AlertaT0? Reporte { get; init; }

    public bool Reportada => Reporte is not null;

    public SituacionT0 Situacion => Reporte?.Gestion switch
    {
        null => SituacionT0.SinReportar,
        GestionT0.Pendiente => SituacionT0.ReportadaPendiente,
        GestionT0.Plan or GestionT0.PlanSinNumero => SituacionT0.ReportadaConPlan,
        _ => SituacionT0.ReportadaSinPlan,
    };
}

/// <summary>En qué punto está una auditoría con T0. El orden es el de las barras.</summary>
public enum SituacionT0
{
    /// <summary>Está en Reportar_T0 y se gestionó con «Plan de acción».</summary>
    ReportadaConPlan,

    /// <summary>Está en Reportar_T0, gestionada sin plan (registro de falta, agente dado de baja, sin acción…).</summary>
    ReportadaSinPlan,

    /// <summary>Está en Reportar_T0 pero sin gestionar.</summary>
    ReportadaPendiente,

    /// <summary>No está en Reportar_T0.</summary>
    SinReportar,
}

/// <summary>
/// Una fila de <c>[Reporting].[pdc].[Reportar_T0]</c> (segundo resultado de la consulta): una T0 reportada por
/// Calidad o por el team leader, con la acción que se tomó.
/// </summary>
/// <param name="Fecha">Día del reporte (<c>Fecha_reporte_Alerta</c>).</param>
/// <param name="FechaFalta">Día de la falta (hay fechas antiguas mal escritas: solo va al Excel).</param>
/// <param name="Accion">«Plan de acción», «Registro de Falta», «Agente dado de baja», «Sin acción»…</param>
/// <param name="IdAccion">El número escrito con la acción (en un plan, el número del plan; 0 si no se puso).</param>
public sealed record AlertaT0(
    long Id,
    DateOnly Fecha,
    DateTime Reporte,
    DateOnly? FechaFalta,
    long? Login,
    long? Legajo,
    string? Sector,
    string? Super,
    string? Team,
    string? Agente,
    string? IdLlamada,
    string? Infraccion,
    string? Gravedad,
    string? ObservacionesReporte,
    long? LegajoAuditor,
    string? NombreAuditor,
    string? CargoAuditor,
    string? Estado,
    DateTime? Gestionado,
    string? Accion,
    long? IdAccion,
    string? ObservacionesRespuesta,
    string? Gestionador,
    string? CargoGestionador,
    bool CompromisoNoCumplido,
    long? Expediente,
    string? MotivoLlamada,
    string? OportunidadMejora)
{
    public GestionT0 Gestion =>
        Igual(Estado, "Pendiente") ? GestionT0.Pendiente
        : Accion is { } a && a.Trim().StartsWith("Plan de acci", StringComparison.OrdinalIgnoreCase)
            ? (IdAccion is > 0 ? GestionT0.Plan : GestionT0.PlanSinNumero)
        : Igual(Accion, "Registro de Falta") ? GestionT0.RegistroFalta
        : Igual(Accion, "Agente dado de baja") ? GestionT0.AgenteBaja
        : GestionT0.OtraAccion;

    /// <summary>Días desde el reporte hasta la gestión; null si sigue pendiente.</summary>
    public double? DiasGestion => Gestionado is { } g && g >= Reporte ? (g - Reporte).TotalDays : null;

    private static bool Igual(string? a, string b) => string.Equals(a?.Trim(), b, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Cómo se gestionó una fila de Reportar_T0.</summary>
public enum GestionT0 { Plan, PlanSinNumero, RegistroFalta, AgenteBaja, OtraAccion, Pendiente }

public static class TextosT0
{
    public static string Texto(SituacionT0 s) => s switch
    {
        SituacionT0.ReportadaConPlan => "Reportada con plan",
        SituacionT0.ReportadaSinPlan => "Reportada sin plan",
        SituacionT0.ReportadaPendiente => "Reportada pendiente",
        _ => "Sin reportar",
    };

    public static string Explicacion(SituacionT0 s) => s switch
    {
        SituacionT0.ReportadaConPlan => "está en Reportar_T0 y se gestionó con plan de acción",
        SituacionT0.ReportadaSinPlan => "está en Reportar_T0, pero se gestionó sin plan",
        SituacionT0.ReportadaPendiente => "está en Reportar_T0 y sigue sin gestionar",
        _ => "la auditoría tiene T0 y no está en Reportar_T0",
    };

    /// <summary>Pastilla de la situación en las tablas (semáforo pastel).</summary>
    public static string Chip(SituacionT0 s) => s switch
    {
        SituacionT0.ReportadaConPlan => "chip-bueno",
        SituacionT0.SinReportar => "chip-critico",
        _ => "chip-atencion",
    };

    /// <summary>Color de la barra de la situación (--relleno).</summary>
    public static string Tono(SituacionT0 s) => s switch
    {
        SituacionT0.ReportadaConPlan => "tono-bueno",
        SituacionT0.SinReportar => "tono-critico",
        _ => "tono-atencion",
    };

    public static string Texto(GestionT0 g) => g switch
    {
        GestionT0.Plan => "Plan de acción",
        GestionT0.PlanSinNumero => "Plan sin número",
        GestionT0.RegistroFalta => "Registro de falta",
        GestionT0.AgenteBaja => "Agente dado de baja",
        GestionT0.OtraAccion => "Otra acción",
        _ => "Pendiente",
    };

    /// <summary>Qué se hizo con el reporte, en corto, para la tabla de detalle.</summary>
    public static string Gestion(AlertaT0? r) => r?.Gestion switch
    {
        null => "—",
        GestionT0.Plan => $"Plan {r.IdAccion}",
        GestionT0.PlanSinNumero => "Plan sin número",
        GestionT0.RegistroFalta => r.IdAccion is > 0 ? $"Registro de falta {r.IdAccion}" : "Registro de falta",
        GestionT0.Pendiente => "Sin gestionar",
        _ => string.IsNullOrWhiteSpace(r.Accion) ? "Sin acción" : r.Accion.Trim(),
    };

    /// <summary>Días con un decimal: «4,1 días», «1 día».</summary>
    public static string Dias(double? dias)
    {
        if (dias is not { } d) return "—";
        if (d < 0.05) return "menos de 1 h";
        return Math.Round(d, 1) == 1 ? "1 día" : d.ToString("#,##0.0", Infraestructura.Formato.Es) + " días";
    }

    /// <summary>Un texto largo (la infracción) cortado por una palabra; el entero va en la ficha.</summary>
    public static string Corto(string? texto, int maximo = 60)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "—";
        texto = texto.Trim();
        if (texto.Length <= maximo) return texto;
        var corte = texto.LastIndexOf(' ', maximo);
        return texto[..(corte > maximo / 2 ? corte : maximo)].TrimEnd(' ', ',', '(', '/') + "…";
    }
}

/// <summary>Lo que pinta la pestaña «T0 y planes de acción» (además de tarjetas y filtros, que van en <see cref="TableroModelo"/>).</summary>
public sealed class InformeT0
{
    /// <summary>Auditorías con T0.</summary>
    public int Total { get; init; }
    public int Reportadas { get; init; }

    /// <summary>Reportadas y gestionadas con plan de acción.</summary>
    public int ConPlan { get; init; }

    public int SinReportar => Total - Reportadas;

    /// <summary>Filas de Reportar_T0 de las fechas elegidas que no vienen de una auditoría T0 de ICEBERG (formularios WEB, team leaders…).</summary>
    public int ReportesSinAuditoria { get; init; }

    /// <summary>Mediana de días entre el reporte y la gestión de las T0 reportadas.</summary>
    public double? DiasGestion { get; init; }

    public IReadOnlyList<FilaSituacionT0> Situaciones { get; init; } = [];
    public IReadOnlyList<FilaAuditorT0> PorAuditor { get; init; } = [];
    public IReadOnlyList<FilaCumplimientoT0> PorSector { get; init; } = [];
    public IReadOnlyList<FilaCumplimientoT0> PorPlantilla { get; init; } = [];

    /// <summary>Las auditorías de la tabla de detalle (primero las sin reportar) y cuántas hay en total.</summary>
    public IReadOnlyList<AuditoriaT0> Detalle { get; init; } = [];
    public int DetalleTotal { get; init; }

    /// <summary>Por periodo: auditorías con T0 (volumen) y % reportadas en Reportar_T0.</summary>
    public IReadOnlyList<PuntoSerie> Evolucion { get; init; } = [];

    /// <summary>Error al leer las T0 (el resto del informe de Auditorías sigue).</summary>
    public string? Error { get; init; }
}

/// <param name="Detalle">Desglose corto (en «Reportada sin plan», con qué acción).</param>
public sealed record FilaSituacionT0(SituacionT0 Situacion, int Cantidad, string? Detalle);

/// <summary>Una fila de «Por técnico de calidad / auditor».</summary>
/// <param name="Auditorias">Todas sus auditorías con los filtros (WEB e ICEBERG), como en General.</param>
/// <param name="ConT0">Sus auditorías de ICEBERG con T0.</param>
/// <param name="Reportadas">De esas, las que están en Reportar_T0 (las reportara él u otra persona).</param>
/// <param name="ReportesPropios">Filas de Reportar_T0 con él como auditor en las fechas elegidas (incluye T0 que no salen de ICEBERG).</param>
public sealed record FilaAuditorT0(string Nombre, string? Cargo, int Auditorias, int ConT0, int Reportadas, int ConPlan, int ReportesPropios, bool Marcado)
{
    public int SinReportar => ConT0 - Reportadas;
    public double? PctT0 => Auditorias > 0 ? (double)ConT0 / Auditorias : null;
    public double? PctReportadas => ConT0 > 0 ? (double)Reportadas / ConT0 : null;
}

public sealed record FilaCumplimientoT0(string Nombre, int Total, int Reportadas, int ConPlan, bool Marcado)
{
    public int SinReportar => Total - Reportadas;
    public double? PctReportadas => Total > 0 ? (double)Reportadas / Total : null;
}
