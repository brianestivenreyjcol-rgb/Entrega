using CDM_Auditorias_Calidad.Models;
using CDM_Auditorias_Calidad.Servicios.Tablero;

namespace CDM_Auditorias_Calidad.Tests;

public class CalculadoraT0Tests
{
    private static DateOnly F(int m, int d) => new(2026, m, d);

    private static Auditoria A(DateOnly fecha, string auditor, string base_ = "ICEBERG")
        => new(fecha, 100, "CO Técnico MasMovil", "Super 1", "Team 1", "Agente", null, "1", auditor, "Técnico de Calidad", 0.8, base_);

    /// <summary>Una fila de Reportar_T0 gestionada con <paramref name="accion"/> (null = pendiente).</summary>
    private static AlertaT0 R(long id, DateOnly fecha, string auditor, string? accion = "Plan de acción", long? idAccion = 500, double horas = 24)
        => new(id, fecha, fecha.ToDateTime(new TimeOnly(9, 0)), fecha, null, 100, "CO Técnico MasMovil", "Super 1", "Team 1", "Agente",
            "llamada-" + id, "Desatención", "Moderado", null, 1, auditor, "Técnico de Calidad",
            accion is null ? "Pendiente" : "Gestionado", accion is null ? null : fecha.ToDateTime(new TimeOnly(9, 0)).AddHours(horas),
            accion ?? "Sin acción", accion is null ? null : idAccion, null, "TL", "Team Leader", false, 1, null, null);

    private static AuditoriaT0 T(DateOnly fecha, string auditor, AlertaT0? reporte = null, string plantilla = "ESTILO YG/MM")
        => new(fecha, 100, "CO Técnico MasMovil", "Super 1", "Team 1", "Agente", fecha.DayNumber * 10 + auditor.Length, "llamada",
            1, auditor, "Técnico de Calidad", plantilla, "Desatención", 0.5, reporte?.Id, reporte is null ? null : "Llamada")
        { Reporte = reporte };

    /// <summary>
    /// Ana: 10 auditorías (8 ICEBERG), 5 con T0 y las 5 en Reportar_T0 (3 con plan, una con registro de falta, una pendiente).
    /// Luis: 4 auditorías, 2 con T0, una sin reportar. Reportar_T0 tiene además una fila de Luis que no sale de ICEBERG.
    /// </summary>
    private static InstantaneaAuditorias Datos()
    {
        var auditorias = Enumerable.Range(1, 10).Select(i => A(F(9, i), "Ana", i <= 8 ? "ICEBERG" : "WEB"))
            .Concat(Enumerable.Range(1, 4).Select(i => A(F(9, i), "Luis"))).ToList();
        var r1 = R(1, F(9, 1), "Ana");
        var r2 = R(2, F(9, 2), "Ana", horas: 48);
        var r3 = R(3, F(9, 3), "Ana", idAccion: 0, horas: 72);
        var r4 = R(4, F(9, 4), "Ana", "Registro de Falta", 77, horas: 12);
        var r5 = R(5, F(9, 5), "Ana", accion: null);
        var r6 = R(6, F(9, 6), "Luis");
        var r7 = R(7, F(9, 7), "Luis", "Agente dado de baja", 0);
        return new InstantaneaAuditorias(auditorias, DateTime.Now, TimeSpan.Zero, null, null,
            [T(F(9, 1), "Ana", r1), T(F(9, 2), "Ana", r2), T(F(9, 3), "Ana", r3), T(F(9, 4), "Ana", r4), T(F(9, 5), "Ana", r5),
             T(F(9, 6), "Luis", r6), T(F(9, 8), "Luis", plantilla: "RETENCION YG")],
            [r1, r2, r3, r4, r5, r6, r7]);
    }

    [Fact]
    public void Por_auditor_cuenta_auditorias_T0_y_las_que_estan_en_Reportar_T0()
    {
        var t = CalculadoraT0.Calcular(Datos(), new FiltrosTablero()).T0!;
        var ana = t.PorAuditor.Single(f => f.Nombre == "Ana");
        Assert.Equal((10, 5, 5, 0, 3), (ana.Auditorias, ana.ConT0, ana.Reportadas, ana.SinReportar, ana.ConPlan));
        Assert.Equal(1.0, ana.PctReportadas);
        var luis = t.PorAuditor.Single(f => f.Nombre == "Luis");
        Assert.Equal((4, 2, 1, 1, 2), (luis.Auditorias, luis.ConT0, luis.Reportadas, luis.SinReportar, luis.ReportesPropios));
        Assert.Equal("Ana", t.PorAuditor[0].Nombre);
    }

    [Fact]
    public void Clasifica_cada_T0_por_su_fila_de_Reportar_T0()
    {
        var m = CalculadoraT0.Calcular(Datos(), new FiltrosTablero());
        var t = m.T0!;
        Assert.Equal((7, 6, 1, 4), (t.Total, t.Reportadas, t.SinReportar, t.ConPlan));
        var cuenta = t.Situaciones.ToDictionary(s => s.Situacion, s => s.Cantidad);
        Assert.Equal(4, cuenta[SituacionT0.ReportadaConPlan]);
        Assert.Equal(1, cuenta[SituacionT0.ReportadaSinPlan]);
        Assert.Equal(1, cuenta[SituacionT0.ReportadaPendiente]);
        Assert.Equal(1, cuenta[SituacionT0.SinReportar]);
        Assert.Equal("Registro de falta 1", t.Situaciones.Single(s => s.Situacion == SituacionT0.ReportadaSinPlan).Detalle);
        Assert.Equal("1 sin número de plan", t.Situaciones.Single(s => s.Situacion == SituacionT0.ReportadaConPlan).Detalle);
        // La fila 7 de Reportar_T0 no sale de una auditoría T0 de ICEBERG.
        Assert.Equal(1, t.ReportesSinAuditoria);
        // Mediana de gestión de las 5 reportadas y gestionadas: 12, 24, 24, 48 y 72 horas -> 1 día.
        Assert.Equal(1.0, t.DiasGestion!.Value, 3);
        Assert.Equal("14", m.Tarjetas[0].Valor);
    }

    [Fact]
    public void El_detalle_empieza_por_las_sin_reportar()
    {
        var t = CalculadoraT0.Calcular(Datos(), new FiltrosTablero()).T0!;
        Assert.Equal(SituacionT0.SinReportar, t.Detalle[0].Situacion);
        Assert.Equal(SituacionT0.ReportadaPendiente, t.Detalle[1].Situacion);
        Assert.Equal(SituacionT0.ReportadaConPlan, t.Detalle[^1].Situacion);
        Assert.Equal("RETENCION YG", t.PorPlantilla[0].Nombre);
    }

    [Fact]
    public void Filtra_por_auditor_y_situacion_y_las_opciones_se_cuentan_con_los_demas_filtros()
    {
        var f = new FiltrosTablero();
        f.Auditor.Add("Luis");
        var m = CalculadoraT0.Calcular(Datos(), f);
        Assert.Equal(2, m.T0!.Total);
        Assert.Equal(4, int.Parse(m.Tarjetas[0].Valor, Infraestructura.Formato.Es));
        Assert.Equal(7, m.Grupos.Single(g => g.Campo == "auditor").Opciones.Sum(o => o.Cantidad));

        var s = new FiltrosTablero();
        s.Situacion.Add("Sin reportar");
        Assert.Single(CalculadoraT0.Detalle(Datos(), s));
    }

    [Fact]
    public void Sin_T0_lo_dice_y_no_rompe()
    {
        var datos = new InstantaneaAuditorias(Datos().Filas, DateTime.Now, TimeSpan.Zero, null, null, null, null, "sin permiso en Reporting.pdc");
        var m = CalculadoraT0.Calcular(datos, new FiltrosTablero());
        Assert.Equal(0, m.T0!.Total);
        Assert.Equal("sin permiso en Reporting.pdc", m.T0.Error);
        Assert.Equal(2, m.T0.PorAuditor.Count);
    }

    [Theory]
    [InlineData(null, "—")]
    [InlineData(0.01, "menos de 1 h")]
    [InlineData(1.0, "1 día")]
    [InlineData(4.07, "4,1 días")]
    public void Escribe_los_dias(double? dias, string esperado) => Assert.Equal(esperado, TextosT0.Dias(dias));

    [Theory]
    [InlineData("Desatención", "Desatención")]
    [InlineData("El no seguimiento del procedimiento afecta a la resolución de la llamada o de la gestión", "El no seguimiento del procedimiento afecta a la resolución…")]
    public void Corta_la_infraccion_por_una_palabra(string texto, string esperado)
        => Assert.Equal(esperado, TextosT0.Corto(texto));
}
