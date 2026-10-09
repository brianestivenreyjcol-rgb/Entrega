using CDM_Auditorias_Calidad.Models;
using CDM_Auditorias_Calidad.Servicios.Tablero;

namespace CDM_Auditorias_Calidad.Tests;

public class CalculadoraT0Tests
{
    private static DateOnly F(int m, int d) => new(2026, m, d);

    /// <summary>Una auditoría T0: sin alerta si <paramref name="alerta"/> es null.</summary>
    private static AuditoriaT0 T(DateOnly fecha, string team, long? alerta = null, string? estado = "Gestionado", string? accion = null,
        long? idAccion = null, long? plan = null, string? motivo = null, long? planCalidad = null, string sector = "CO Técnico MasMovil")
        => new(fecha, 100, sector, "Super 1", team, "Agente", fecha.DayNumber, "llamada", "1", "Auditor 1", "Técnico de Calidad",
            "ESTILO YG/MM", "Desatención", 0.5, alerta, alerta is null ? null : "Llamada", null, null, alerta is null ? null : estado,
            accion, idAccion, null, null, null, plan, null, plan is null ? null : "Abierto", motivo, planCalidad, null);

    /// <summary>
    /// Septiembre: 6 auditorías T0 del team A y B: plan verificado (Tolerancia Cero), plan sin verificar, registro de falta,
    /// alerta pendiente, sin alerta (con plan «Acción de Calidad» del agente) y sin alerta. Octubre: una con plan verificado.
    /// </summary>
    private static InstantaneaAuditorias Datos(IReadOnlyDictionary<DateOnly, int>? planes = null) => new(
        [new Auditoria(F(9, 1), 1, "CO Técnico MasMovil", "Super 1", "A", "Agente", null, "1", "Auditor 1", "Técnico de Calidad", 0.8, "ICEBERG"),
         new Auditoria(F(10, 3), 1, "CO Técnico MasMovil", "Super 1", "A", "Agente", null, "1", "Auditor 1", "Técnico de Calidad", 0.8, "ICEBERG")],
        DateTime.Now, TimeSpan.Zero, null, null,
        [
            T(F(9, 2), "A", 1, accion: "Plan de acción", idAccion: 900, plan: 900, motivo: "Tolerancia Cero"),
            T(F(9, 3), "A", 2, accion: "Plan de acción", idAccion: 901),
            T(F(9, 4), "A", 3, accion: "Registro de Falta", idAccion: 5),
            T(F(9, 5), "B", 4, estado: "Pendiente"),
            T(F(9, 6), "B", planCalidad: 77),
            T(F(9, 7), "B"),
            T(F(10, 2), "B", 5, accion: "Plan de acción", idAccion: 902, plan: 902, motivo: "Acción de Calidad", planCalidad: 902, sector: "CO Fide OUT"),
        ],
        null, planes);

    [Fact]
    public void Clasifica_cada_auditoria_frente_al_plan()
    {
        var t = CalculadoraT0.Calcular(Datos(), new FiltrosTablero()).T0!;
        Assert.Equal(7, t.Total);
        Assert.Equal(5, t.ConAlerta);
        Assert.Equal(3, t.ConPlan);
        Assert.Equal(2, t.Verificados);
        Assert.Equal(4, t.SinPlan);
        var cuenta = t.Situaciones.ToDictionary(s => s.Situacion, s => s.Cantidad);
        Assert.Equal(2, cuenta[SituacionT0.PlanVerificado]);
        Assert.Equal(1, cuenta[SituacionT0.PlanSinVerificar]);
        Assert.Equal(1, cuenta[SituacionT0.OtraAccion]);
        Assert.Equal(1, cuenta[SituacionT0.AlertaPendiente]);
        Assert.Equal(2, cuenta[SituacionT0.SinAlerta]);
        Assert.Equal("Registro de Falta 1", t.Situaciones.Single(s => s.Situacion == SituacionT0.OtraAccion).Detalle);
    }

    [Fact]
    public void Compara_el_plan_de_la_alerta_con_accion_de_calidad()
    {
        var t = CalculadoraT0.Calcular(Datos(), new FiltrosTablero()).T0!;
        Assert.Equal(1, t.AmbosPlanes);
        Assert.Equal(2, t.SoloAlerta);
        Assert.Equal(1, t.SoloCalidad);
        Assert.Equal(3, t.NingunPlan);
        Assert.Equal(new[] { ("Acción de Calidad", 1), ("Tolerancia Cero", 1) }, t.Motivos);
    }

    [Fact]
    public void Por_team_primero_el_que_tiene_mas_sin_plan_y_el_detalle_empieza_por_los_que_incumplen()
    {
        var t = CalculadoraT0.Calcular(Datos(), new FiltrosTablero()).T0!;
        Assert.Equal(new[] { "B", "A" }, t.PorTeam.Select(f => f.Nombre));
        Assert.Equal(3, t.PorTeam[0].SinPlan);
        Assert.Equal(SituacionT0.SinAlerta, t.Detalle[0].Situacion);
        Assert.Equal(F(9, 7), t.Detalle[0].Fecha);
        Assert.Equal(SituacionT0.PlanVerificado, t.Detalle[^1].Situacion);
    }

    [Fact]
    public void Filtra_por_situacion_y_por_fechas_y_las_opciones_se_cuentan_con_los_demas_filtros()
    {
        var f = new FiltrosTablero { Desde = "2026-09-01", Hasta = "2026-09-30" };
        f.Situacion.Add("Sin alerta T0");
        var m = CalculadoraT0.Calcular(Datos(), f);
        Assert.Equal(2, m.T0!.Total);
        Assert.All(m.T0.Detalle, a => Assert.Equal(SituacionT0.SinAlerta, a.Situacion));
        var situacion = m.Grupos.Single(g => g.Campo == "situacion");
        Assert.Equal(6, situacion.Opciones.Sum(o => o.Cantidad));
        Assert.True(situacion.Opciones.Single(o => o.Valor == "Sin alerta T0").Marcada);
        Assert.Equal(2, CalculadoraT0.Detalle(Datos(), f).Count);
    }

    [Fact]
    public void Avisa_de_los_huecos_de_PlanAccion_de_tres_dias_o_mas()
    {
        var planes = new Dictionary<DateOnly, int> { [F(9, 1)] = 5, [F(9, 2)] = 3, [F(9, 3)] = 1, [F(9, 8)] = 4, [F(9, 9)] = 2 };
        // Del 4 al 7 sin planes (4 días); el 10 y el 11 tampoco, pero el 11 es ayer y solo son 2 días.
        var huecos = CalculadoraT0.Huecos(planes, F(9, 12));
        Assert.Equal(new[] { new HuecoPlanes(F(9, 4), F(9, 7)) }, huecos);
        Assert.Equal(4, huecos[0].Dias);
        // Hasta ayer: si la tabla dejó de cargarse, el hueco llega al final.
        Assert.Equal(new HuecoPlanes(F(9, 10), F(9, 14)), CalculadoraT0.Huecos(planes, F(9, 15))[^1]);
    }

    [Fact]
    public void Sin_alertas_T0_lo_dice_y_no_rompe()
    {
        var datos = new InstantaneaAuditorias(Datos().Filas, DateTime.Now, TimeSpan.Zero, null, null, null, "sin permiso en RecursosHumanos");
        var m = CalculadoraT0.Calcular(datos, new FiltrosTablero());
        Assert.Equal(0, m.T0!.Total);
        Assert.Equal("sin permiso en RecursosHumanos", m.T0.Error);
    }

    [Theory]
    [InlineData("Desatención", "Desatención")]
    [InlineData("El no seguimiento del procedimiento afecta a la resolución de la llamada o de la gestión", "El no seguimiento del procedimiento afecta a la resolución…")]
    public void Corta_la_infraccion_por_una_palabra(string texto, string esperado)
        => Assert.Equal(esperado, TextosT0.Corto(texto));
}
