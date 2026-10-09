namespace CDM_Auditorias_Calidad.Models;

/// <summary>
/// Una auditoría de calidad de ICEBERG con afectación «Tolerancia 0» y lo que se encontró de ella
/// (<c>Consultas/AuditoriasT0.sql</c>): su alerta T0 en <c>Legal.Alertas_T0</c> y el plan de acción en
/// <c>Legal.PlanAccion</c>. La regla de negocio: cada auditoría con T0 debe tener un plan de acción cargado.
/// </summary>
/// <param name="Infraccion">El texto de la columna «Tolerancia 0» de la plantilla.</param>
/// <param name="Cruce">Cómo se encontró la alerta: «Llamada» (mismo ID de llamada) o «Agente, auditor y fecha».</param>
/// <param name="Accion">La acción que dice la alerta: «Plan de acción», «Registro de Falta», «Agente dado de baja»…</param>
/// <param name="IdAccion">El número que se escribió en la alerta (si es un plan, el <c>IdPlanDeAccion</c>).</param>
/// <param name="IdPlan">El plan de la alerta tal como está en <c>Legal.PlanAccion</c>; null si no está.</param>
/// <param name="IdPlanCalidad">
/// Comparación: el primer plan con motivo «Acción de Calidad» del agente creado entre el día de la
/// auditoría y 15 días después, sin pasar por la alerta.
/// </param>
public sealed record AuditoriaT0(
    DateOnly Fecha,
    long? Legajo,
    string? Sector,
    string? Super,
    string? Team,
    string? Agente,
    long IdGestion,
    string? IdLlamada,
    string? LegajoAuditor,
    string? NombreAuditor,
    string? CargoAuditor,
    string? Plantilla,
    string? Infraccion,
    double? Nota,
    long? IdAlerta,
    string? Cruce,
    DateTime? FechaReporteT0,
    string? Gravedad,
    string? EstadoAlerta,
    string? Accion,
    long? IdAccion,
    DateTime? FechaGestionado,
    string? Gestionador,
    string? CargoGestionador,
    long? IdPlan,
    DateTime? FechaPlan,
    string? EstadoPlan,
    string? MotivoPlan,
    long? IdPlanCalidad,
    DateTime? FechaPlanCalidad)
{
    /// <summary>La alerta dice que se cargó un plan de acción.</summary>
    public bool AlertaConPlan => Accion is { } a && a.Trim().StartsWith("Plan de acci", StringComparison.OrdinalIgnoreCase);

    public SituacionT0 Situacion =>
        IdAlerta is null ? SituacionT0.SinAlerta
        : string.Equals(EstadoAlerta?.Trim(), "Pendiente", StringComparison.OrdinalIgnoreCase) ? SituacionT0.AlertaPendiente
        : AlertaConPlan ? (IdPlan is not null ? SituacionT0.PlanVerificado : SituacionT0.PlanSinVerificar)
        : SituacionT0.OtraAccion;

    /// <summary>Cumple la regla: la alerta T0 tiene un plan de acción (esté o no en <c>Legal.PlanAccion</c>).</summary>
    public bool ConPlan => Situacion is SituacionT0.PlanVerificado or SituacionT0.PlanSinVerificar;
}

/// <summary>En qué punto está una auditoría T0 frente a la regla «debe tener un plan de acción».</summary>
public enum SituacionT0
{
    /// <summary>La alerta dice «Plan de acción» y ese plan está en <c>Legal.PlanAccion</c>.</summary>
    PlanVerificado,

    /// <summary>La alerta dice «Plan de acción» pero su número no está en <c>Legal.PlanAccion</c> (o es 0).</summary>
    PlanSinVerificar,

    /// <summary>La alerta se gestionó con otra acción: registro de falta, agente dado de baja, sin acción…</summary>
    OtraAccion,

    /// <summary>La alerta existe pero sigue pendiente de gestionar.</summary>
    AlertaPendiente,

    /// <summary>No se encontró alerta T0 para la auditoría.</summary>
    SinAlerta,
}

public static class TextosT0
{
    public static string Texto(SituacionT0 s) => s switch
    {
        SituacionT0.PlanVerificado => "Plan verificado",
        SituacionT0.PlanSinVerificar => "Plan sin verificar",
        SituacionT0.OtraAccion => "Otra acción",
        SituacionT0.AlertaPendiente => "Alerta pendiente",
        _ => "Sin alerta T0",
    };

    public static string Explicacion(SituacionT0 s) => s switch
    {
        SituacionT0.PlanVerificado => "la alerta T0 tiene plan de acción y el plan está en PlanAccion",
        SituacionT0.PlanSinVerificar => "la alerta T0 dice «Plan de acción», pero ese número no está en PlanAccion",
        SituacionT0.OtraAccion => "la alerta T0 se gestionó sin plan: registro de falta, agente dado de baja…",
        SituacionT0.AlertaPendiente => "la alerta T0 está sin gestionar",
        _ => "no hay alerta T0 para la auditoría, así que tampoco plan",
    };

    /// <summary>Pastilla de la situación en las tablas (semáforo pastel).</summary>
    public static string Chip(SituacionT0 s) => s switch
    {
        SituacionT0.PlanVerificado => "chip-bueno",
        SituacionT0.PlanSinVerificar => "",
        SituacionT0.SinAlerta => "chip-critico",
        _ => "chip-atencion",
    };

    /// <summary>Qué se hizo con la alerta, en corto, para la tabla de detalle.</summary>
    public static string Accion(AuditoriaT0 a) => a.Situacion switch
    {
        SituacionT0.PlanVerificado => $"Plan {a.IdPlan} · {a.MotivoPlan ?? "sin motivo"} · {a.EstadoPlan ?? "sin estado"}",
        SituacionT0.PlanSinVerificar => a.IdAccion is > 0 ? $"Plan {a.IdAccion}, no está en PlanAccion" : "Plan sin número",
        SituacionT0.OtraAccion => string.IsNullOrWhiteSpace(a.Accion) ? "Sin acción" : a.Accion.Trim(),
        SituacionT0.AlertaPendiente => $"Alerta {a.IdAlerta} sin gestionar",
        _ => "—",
    };

    /// <summary>Un texto largo (la infracción) cortado por una palabra; el entero va en la ficha.</summary>
    public static string Corto(string? texto, int maximo = 60)
    {
        if (string.IsNullOrWhiteSpace(texto)) return "—";
        texto = texto.Trim();
        if (texto.Length <= maximo) return texto;
        var corte = texto.LastIndexOf(' ', maximo);
        return texto[..(corte > maximo / 2 ? corte : maximo)].TrimEnd(' ', ',', '(', '/') + "…";
    }

    /// <summary>Color de la barra de la situación (--relleno).</summary>
    public static string Tono(SituacionT0 s) => s switch
    {
        SituacionT0.PlanVerificado => "tono-bueno",
        SituacionT0.PlanSinVerificar => "tono-neutro",
        SituacionT0.SinAlerta => "tono-critico",
        _ => "tono-atencion",
    };
}

/// <summary>Un tramo de días sin ningún plan creado en <c>Legal.PlanAccion</c> (la tabla tiene huecos).</summary>
public sealed record HuecoPlanes(DateOnly Desde, DateOnly Hasta)
{
    public int Dias => Hasta.DayNumber - Desde.DayNumber + 1;
}

/// <summary>Lo que pinta la pestaña «T0 y planes de acción» (además de tarjetas y filtros, que van en <see cref="TableroModelo"/>).</summary>
public sealed class InformeT0
{
    public int Total { get; init; }
    public int ConAlerta { get; init; }
    public int ConPlan { get; init; }
    public int Verificados { get; init; }

    public int SinPlan => Total - ConPlan;

    public IReadOnlyList<FilaSituacionT0> Situaciones { get; init; } = [];
    public IReadOnlyList<FilaCumplimientoT0> PorSector { get; init; } = [];
    public IReadOnlyList<FilaCumplimientoT0> PorTeam { get; init; } = [];

    /// <summary>Motivo de los planes verificados (el que se eligió al cargarlo en PlanAccion).</summary>
    public IReadOnlyList<(string Motivo, int Cantidad)> Motivos { get; init; } = [];

    /// <summary>Plan según la alerta frente a plan «Acción de Calidad» del agente (0–15 días).</summary>
    public int AmbosPlanes { get; init; }
    public int SoloAlerta { get; init; }
    public int SoloCalidad { get; init; }
    public int NingunPlan { get; init; }

    /// <summary>Las auditorías de la tabla de detalle (primero las que no tienen plan) y cuántas hay en total.</summary>
    public IReadOnlyList<AuditoriaT0> Detalle { get; init; } = [];
    public int DetalleTotal { get; init; }

    /// <summary>Por periodo: auditorías T0 (volumen) y % con plan de acción.</summary>
    public IReadOnlyList<PuntoSerie> Evolucion { get; init; } = [];

    public IReadOnlyList<HuecoPlanes> Huecos { get; init; } = [];

    /// <summary>Error al leer las alertas T0 (el resto del informe de Auditorías sigue).</summary>
    public string? Error { get; init; }
}

/// <param name="Detalle">Desglose corto (en «Otra acción», qué acciones).</param>
public sealed record FilaSituacionT0(SituacionT0 Situacion, int Cantidad, string? Detalle);

/// <param name="Sub">Dato corto al lado del nombre (el super de un team).</param>
public sealed record FilaCumplimientoT0(string Nombre, string? Sub, int Total, int ConAlerta, int ConPlan, int Verificados, bool Marcado)
{
    public int SinPlan => Total - ConPlan;
    public double? PctPlan => Total > 0 ? (double)ConPlan / Total : null;
}
