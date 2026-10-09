using CDM_Auditorias_Calidad.Servicios.Tablero;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>Las funciones de fechas de DAX (ver docs/contexto/power-bi.md, 2.3).</summary>
public class PeriodosTests
{
    private static DateOnly F(int a, int m, int d) => new(a, m, d);

    [Theory]
    [InlineData(2026, 1, 1, 1)]   // jueves: semana 1
    [InlineData(2026, 1, 4, 1)]   // domingo: sigue en la 1
    [InlineData(2026, 1, 5, 2)]   // lunes: empieza la 2
    [InlineData(2026, 9, 28, 40)] // última semana del PBI de la captura
    [InlineData(2026, 12, 31, 53)]
    public void Semana_es_WEEKNUM_tipo_2(int a, int m, int d, int esperada)
        => Assert.Equal(esperada, Periodos.Semana(F(a, m, d)));

    [Fact]
    public void Lunes_de_la_semana()
    {
        Assert.Equal(F(2026, 9, 28), Periodos.Lunes(F(2026, 10, 1)));
        Assert.Equal(F(2026, 9, 28), Periodos.Lunes(F(2026, 9, 28)));
        Assert.Equal(F(2026, 9, 28), Periodos.Lunes(F(2026, 10, 4)));
    }

    [Fact]
    public void Un_mes_entero_pasa_al_mes_anterior_entero()
    {
        var septiembre = Dias(F(2026, 9, 1), F(2026, 9, 30));
        var r = Periodos.DesplazarMeses(septiembre, -1, F(2026, 1, 1), F(2026, 12, 31));
        Assert.Equal(F(2026, 8, 1), r.Min);
        Assert.Equal(F(2026, 8, 31), r.Max);
        Assert.Equal(31, r.Count);
    }

    [Fact]
    public void Un_dia_suelto_pasa_al_mismo_dia_o_al_ultimo_del_mes()
    {
        Assert.Equal(new[] { F(2026, 8, 15) }, Periodos.DesplazarMeses([F(2026, 9, 15)], -1, F(2026, 1, 1), F(2026, 12, 31)));
        Assert.Equal(new[] { F(2026, 2, 28) }, Periodos.DesplazarMeses([F(2026, 3, 31)], -1, F(2026, 1, 1), F(2026, 12, 31)));
    }

    [Fact]
    public void El_desplazamiento_se_recorta_al_calendario()
    {
        var r = Periodos.DesplazarMeses(Dias(F(2026, 8, 1), F(2026, 10, 1)), -1, F(2026, 8, 1), F(2026, 10, 1));
        Assert.Equal(F(2026, 8, 1), r.Min);
        Assert.Equal(F(2026, 9, 1), r.Max);

        Assert.Empty(Periodos.DesplazarDias([F(2026, 8, 3)], -7, F(2026, 8, 1), F(2026, 10, 1)));
    }

    [Fact]
    public void El_periodo_anterior_es_el_mismo_tiempo_justo_antes()
    {
        var ultima = F(2026, 10, 2);
        // Un mes entero → el mes anterior entero.
        var sep = Periodos.PeriodoAnterior(Dias(F(2026, 9, 1), F(2026, 9, 30)), ultima)!;
        Assert.Equal((F(2026, 8, 1), F(2026, 8, 31), 31), (sep.Min, sep.Max, sep.Count));
        // El mes en curso (hasta la última fecha con datos) → las mismas fechas del anterior.
        var oct = Periodos.PeriodoAnterior(Dias(F(2026, 10, 1), ultima), ultima)!;
        Assert.Equal((F(2026, 9, 1), F(2026, 9, 2)), (oct.Min, oct.Max));
        // Dos meses → los dos de antes.
        var dos = Periodos.PeriodoAnterior(Dias(F(2026, 8, 1), F(2026, 9, 30)), ultima)!;
        Assert.Equal((F(2026, 6, 1), F(2026, 7, 31)), (dos.Min, dos.Max));
        // Una semana → la semana de antes.
        var semana = Periodos.PeriodoAnterior(Dias(F(2026, 9, 21), F(2026, 9, 27)), ultima)!;
        Assert.Equal((F(2026, 9, 14), F(2026, 9, 20)), (semana.Min, semana.Max));
        // Nunca se solapa con el periodo elegido.
        Assert.True(semana.Max < F(2026, 9, 21));
        Assert.Null(Periodos.PeriodoAnterior([], ultima));
    }

    private static List<DateOnly> Dias(DateOnly desde, DateOnly hasta)
    {
        var l = new List<DateOnly>();
        for (var d = desde; d <= hasta; d = d.AddDays(1)) l.Add(d);
        return l;
    }
}
