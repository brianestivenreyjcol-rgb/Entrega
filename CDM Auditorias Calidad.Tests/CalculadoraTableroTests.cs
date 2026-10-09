using CDM_Auditorias_Calidad.Models;
using CDM_Auditorias_Calidad.Servicios.Tablero;

namespace CDM_Auditorias_Calidad.Tests;

public class CalculadoraTableroTests
{
    private static DateOnly F(int m, int d) => new(2026, m, d);

    private static Auditoria A(DateOnly fecha, long legajo, string sector, string auditor, string cargo, double? nota, string base_ = "WEB")
        => new(fecha, legajo, sector, "Super 1", "Team 1", "Agente " + legajo, null, null, auditor, cargo, nota, base_);

    /// <summary>
    /// Agosto: 4 auditorías; septiembre: 6 (2 en la semana del 28); 1 de octubre: 1.
    /// </summary>
    private static InstantaneaAuditorias Datos() => new(
    [
        A(F(8, 1), 1, "Jazztel", "Ana", "Técnico de Calidad", 0.40),
        A(F(8, 21), 2, "Jazztel", "Ana", "Técnico de Calidad", 0.60),
        A(F(8, 24), 3, "Orange", "Luis", "Team Leader", 0.20),
        A(F(9, 1), 1, "Orange", "Luis", "Team Leader", null),
        A(F(9, 2), 4, "Jazztel", "Ana", "Técnico de Calidad", 0.80),
        A(F(9, 21), 5, "Jazztel", "Marta", "Formador", 1.00, "ICEBERG"),
        A(F(9, 22), 5, "Orange", "Marta", "Formador", 0.50, "ICEBERG"),
        A(F(9, 28), 6, "Orange", "Luis", "Team Leader", 0.30),
        A(F(9, 29), 7, "Jazztel", "Ana", "Técnico de Calidad", 0.70),
        A(F(9, 30), 7, "Jazztel", "Ana", "Técnico de Calidad PP", 0.90),
        A(F(10, 1), 8, "", "Ana", "Técnico de Calidad", 0.50),
    ], DateTime.Now, TimeSpan.Zero, Nomina());

    private static RegistroNomina N(DateOnly fecha, long legajo, string sector, string cargo)
        => new(fecha, legajo, sector, "Super 1", "Team 1", cargo);

    /// <summary>
    /// 15/09: legajos 1 a 9 agentes (impares en Jazztel, pares en Orange), 20 Team Leader (no
    /// cuenta), 21 en capacitación y 22 aprendiz (en minúsculas: se compara sin mayúsculas).
    /// 10/08: legajo 30, agente en Orange.
    /// </summary>
    private static List<RegistroNomina> Nomina()
    {
        var l = Enumerable.Range(1, 9).Select(i => N(F(9, 15), i, i % 2 == 1 ? "Jazztel" : "Orange", "Agente")).ToList();
        l.Add(N(F(9, 15), 20, "Jazztel", "Team Leader"));
        l.Add(N(F(9, 15), 21, "Jazztel", "Agente en Capacitacion"));
        l.Add(N(F(9, 15), 22, "Orange", "aprendiz sena etapa productiva"));
        l.Add(N(F(8, 10), 30, "Orange", "Agente"));
        return l;
    }

    private static readonly PaginaTablero Formacion =
        PaginaTablero.Formacion(["Formador", "Formador PP", "Técnico de Calidad", "Técnico de Calidad PP"]);

    private static TableroModelo Calcular(FiltrosTablero f, PaginaTablero? p = null)
        => CalculadoraTablero.Calcular(Datos(), p ?? PaginaTablero.General, f, 0.5);

    [Fact]
    public void Sin_filtros_usa_todo_el_calendario()
    {
        var m = Calcular(new FiltrosTablero());
        Assert.Equal(F(8, 1), m.Desde);
        Assert.Equal(F(10, 1), m.Hasta);
        Assert.Equal(11, m.TotalFiltrado);
        Assert.Equal("11", m.Tarjetas[0].Valor);
        Assert.Equal(Vista.Semana, m.Vista);
    }

    [Fact]
    public void Semana_y_mes_salen_de_la_ultima_fecha_elegida()
    {
        // Última fecha 01/10 (jueves): semana 28/09–01/10 = 4; anterior 21/09–24/09 = 2.
        var m = Calcular(new FiltrosTablero());
        Assert.Equal("4", m.Tarjetas[1].Valor);
        Assert.Equal(1.0, m.Tarjetas[1].Variacion!.Value, 6);

        // Mes: 01/10 = 1; anterior: 01/09 = 1.
        Assert.Equal("1", m.Tarjetas[2].Valor);
        Assert.Equal(0.0, m.Tarjetas[2].Variacion!.Value, 6);
    }

    [Fact]
    public void Con_el_rango_completo_no_hay_periodo_anterior()
    {
        // 01/08–01/10 se compararía con 01/05–01/07, y no hay datos antes del 01/08: sin
        // variación (el PBI comparaba con 01/08–01/09, solapado y más corto).
        var m = Calcular(new FiltrosTablero());
        Assert.Null(m.Tarjetas[0].Variacion);
        Assert.Equal("Sin período anterior con datos", m.Tarjetas[0].TextoVariacion);
        Assert.Null(m.Tarjetas[3].Variacion);
        Assert.Contains("no hay datos antes del 01/08/2026", m.Tarjetas[0].Ayuda);
    }

    [Fact]
    public void Un_mes_se_compara_con_el_mes_anterior_entero()
    {
        // Septiembre (7) frente a agosto entero (3).
        var m = Calcular(new FiltrosTablero { Mes = ["2026-09"] });
        Assert.Equal((7 - 3) / 3.0, m.Tarjetas[0].Variacion!.Value, 6);
        Assert.Contains("01/08/2026 – 31/08/2026", m.Tarjetas[0].Ayuda);
    }

    [Fact]
    public void Un_mes_en_curso_se_compara_con_las_mismas_fechas_del_anterior()
    {
        // Octubre solo tiene datos hasta el 01/10: 01/10 (1) frente a 01/09 (1).
        var m = Calcular(new FiltrosTablero { Mes = ["2026-10"] });
        Assert.Equal(0.0, m.Tarjetas[0].Variacion!.Value, 6);
        Assert.Contains("frente a 01/09/2026 (el mismo tiempo", m.Tarjetas[0].Ayuda);
    }

    [Fact]
    public void Un_rango_de_dias_se_compara_con_los_mismos_dias_justo_antes()
    {
        // 02/09–22/09 (21 días: 3 auditorías) frente a 12/08–01/09 (21 días: 21/08, 24/08 y 01/09).
        var m = Calcular(new FiltrosTablero { Desde = "2026-09-02", Hasta = "2026-09-22" });
        Assert.Equal(0.0, m.Tarjetas[0].Variacion!.Value, 6);
        Assert.Contains("12/08/2026 – 01/09/2026", m.Tarjetas[0].Ayuda);
    }

    [Fact]
    public void Nota_media_ignora_las_vacias()
    {
        var m = Calcular(new FiltrosTablero { Desde = "2026-09-01", Hasta = "2026-09-30" });
        // Septiembre: notas 0,8 1 0,5 0,3 0,7 0,9 (una vacía) → 0,7.
        Assert.Equal("70,00 %", m.Tarjetas[3].Valor);
        // Agosto (mismo rango un mes antes): 0,4 0,6 0,2 → 0,4 → +30 pp.
        Assert.Equal(30.0, m.Tarjetas[3].Variacion!.Value, 6);
    }

    [Fact]
    public void Total_agentes_son_los_de_nomina_y_se_comparan_con_los_auditados()
    {
        var m = Calcular(new FiltrosTablero { Desde = "2026-09-01", Hasta = "2026-09-30" });
        var t = m.Tarjetas[4];
        Assert.Equal("Total agentes", t.Titulo);
        // Septiembre: 1–9, 21 y 22 (el Team Leader no) = 11; auditados en septiembre: 1, 4, 5, 6, 7.
        Assert.Equal("11", t.Valor);
        Assert.Equal(5 / 11.0, t.Progreso!.Value, 6);
        Assert.Equal("5 auditados · 45,45 %", t.TextoVariacion);
    }

    [Fact]
    public void Total_agentes_usa_las_fechas_y_los_filtros_de_sector_super_y_team()
    {
        // Todo el calendario y sector Orange: 2, 4, 6, 8, 22 (septiembre) y 30 (agosto).
        Assert.Equal("6", Calcular(new FiltrosTablero { Sector = ["Orange"] }).Tarjetas[4].Valor);
        // El filtro de auditor no cambia la nómina, solo los auditados.
        var conAuditor = Calcular(new FiltrosTablero { Desde = "2026-09-01", Hasta = "2026-09-30", Auditor = ["Marta"] }).Tarjetas[4];
        Assert.Equal("11", conAuditor.Valor);
        Assert.Equal(1 / 11.0, conAuditor.Progreso!.Value, 6);
    }

    [Fact]
    public void Sin_nomina_la_tarjeta_sale_sin_dato()
    {
        var sinNomina = new InstantaneaAuditorias(Datos().Filas, DateTime.Now, TimeSpan.Zero, null, "tiempo agotado");
        var t = CalculadoraTablero.Calcular(sinNomina, PaginaTablero.General, new FiltrosTablero(), 0.5).Tarjetas[4];
        Assert.Equal("—", t.Valor);
        Assert.Null(t.Progreso);
        Assert.Contains("tiempo agotado", t.Ayuda);
    }

    [Fact]
    public void Sin_periodo_anterior_no_inventa_variacion()
    {
        // Agosto se compararía con julio, y no hay datos de julio.
        var m = Calcular(new FiltrosTablero { Desde = "2026-08-01", Hasta = "2026-08-31" });
        Assert.Null(m.Tarjetas[0].Variacion);
        Assert.Null(m.Tarjetas[3].Variacion);
        Assert.Equal("Sin período anterior con datos", m.Tarjetas[3].TextoVariacion);
    }

    [Fact]
    public void Formacion_solo_tiene_los_cargos_de_su_filtro_de_pagina()
    {
        var m = Calcular(new FiltrosTablero(), Formacion);
        Assert.Equal(8, m.TotalFiltrado);
        Assert.Equal(Vista.Mes, m.Vista);
        Assert.DoesNotContain(m.Grupos.Single(g => g.Campo == "cargo").Opciones, o => o.Valor == "Team Leader");
    }

    [Fact]
    public void Los_filtros_se_filtran_entre_si_y_las_marcadas_siempre_se_ven()
    {
        var m = Calcular(new FiltrosTablero { Sector = ["Orange"], Auditor = ["Ana"] });
        Assert.Equal(0, m.TotalFiltrado);
        var auditores = m.Grupos.Single(g => g.Campo == "auditor").Opciones;
        // Con sector Orange solo hay Luis y Marta, pero Ana está marcada y se ve con 0.
        Assert.Equal(new[] { "Ana", "Luis", "Marta" }, auditores.Select(o => o.Valor));
        Assert.Equal(0, auditores.Single(o => o.Valor == "Ana").Cantidad);
        Assert.True(auditores.Single(o => o.Valor == "Ana").Marcada);
    }

    [Fact]
    public void Los_vacios_se_filtran_como_en_blanco()
    {
        var m = Calcular(new FiltrosTablero { Sector = [CalculadoraTablero.EnBlanco] });
        Assert.Equal(1, m.TotalFiltrado);
        Assert.Equal(CalculadoraTablero.EnBlanco, m.Sectores.Single().Sector);
    }

    [Fact]
    public void El_top_agrupa_por_auditor_y_cargo()
    {
        var m = Calcular(new FiltrosTablero());
        Assert.Equal("Ana", m.TopAuditores[0].Auditor);
        Assert.Equal(5, m.TopAuditores[0].Cantidad);
        Assert.Contains(m.TopAuditores, f => f.Auditor == "Ana" && f.Cargo == "Técnico de Calidad PP" && f.Cantidad == 1);
    }

    [Fact]
    public void Las_vistas_agrupan_por_dia_semana_y_mes()
    {
        var mes = Calcular(new FiltrosTablero { Vista = "mes" });
        Assert.Equal(new[] { "ago", "sept", "oct" }, mes.Evolucion.Select(p => p.Etiqueta));
        Assert.Equal(new[] { 3, 7, 1 }, mes.Evolucion.Select(p => p.Cantidad));

        var semana = Calcular(new FiltrosTablero { Vista = "semana" });
        Assert.Equal("40", semana.Evolucion[^1].Etiqueta);
        Assert.Equal(4, semana.Evolucion[^1].Cantidad);

        var dia = Calcular(new FiltrosTablero { Vista = "dia", Desde = "2026-09-28" });
        Assert.Equal(new[] { "28/09", "29/09", "30/09", "01/10" }, dia.Evolucion.Select(p => p.Etiqueta));
    }

    [Fact]
    public void El_filtro_de_mes_no_cambia_semana_ni_mes()
    {
        // Como en DAX: las medidas de semana/mes ponen sus fechas y anulan el filtro de Mes.
        var m = Calcular(new FiltrosTablero { Mes = ["2026-09"] });
        Assert.Equal(7, m.TotalFiltrado);
        // Última fecha elegida: 30/09 → semana 28–30/09 = 3; mes 01–30/09 = 7.
        Assert.Equal("3", m.Tarjetas[1].Valor);
        Assert.Equal("7", m.Tarjetas[2].Valor);
    }

    [Fact]
    public void Fechas_al_reves_se_intercambian()
    {
        var m = Calcular(new FiltrosTablero { Desde = "2026-09-30", Hasta = "2026-09-01" });
        Assert.Equal(F(9, 1), m.Desde);
        Assert.Equal(7, m.TotalFiltrado);
    }

    [Fact]
    public void El_detalle_tiene_los_mismos_filtros()
    {
        var f = new FiltrosTablero { Base = ["ICEBERG"] };
        var detalle = CalculadoraTablero.Detalle(Datos(), PaginaTablero.General, f);
        Assert.Equal(Calcular(f).TotalFiltrado, detalle.Count);
        Assert.Equal(F(9, 22), detalle[0].Fecha);
    }

    [Fact]
    public void La_url_lleva_los_filtros_y_alternar_quita_o_pone()
    {
        var f = new FiltrosTablero { Sector = ["CO Atención Jazztel"], Vista = "dia" };
        Assert.Equal("?sector=CO%20Atenci%C3%B3n%20Jazztel&vista=dia", f.Consulta());
        Assert.Empty(f.Alternar("sector", "CO Atención Jazztel").Sector);
        Assert.Equal(2, f.Alternar("sector", "Otro").Sector.Count);
        Assert.Single(f.Sector);
    }
}
