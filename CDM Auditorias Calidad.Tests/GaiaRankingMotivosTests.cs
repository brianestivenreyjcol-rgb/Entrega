using CDM_Auditorias_Calidad.Servicios.Gaia;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>GAIA, pestañas «Ranking y Estilo» y «Motivo de contacto» (07-10-2026).</summary>
public class GaiaRankingMotivosTests
{
    private static LlamadaGaia L(Action<LlamadaGaia>? ajuste = null)
    {
        var l = new LlamadaGaia
        {
            IdConversacion = Guid.NewGuid().ToString(), IdAgente = "A1", Agente = "Ana", Oleada = "740", Sector = "YGMM",
            Formador = "F1", Supervisor = "S1", Marca = "YOIGO", Fecha = new DateOnly(2026, 9, 7), FechaHora = new DateTime(2026, 9, 7, 10, 0, 0),
        };
        ajuste?.Invoke(l);
        return l;
    }

    [Fact]
    public void Indicadores_traen_silencio_medio_y_cuentas_de_rellamada()
    {
        var i = CalculadoraGaia.Calcular([
            L(l => { l.DuracionSegundos = 100; l.TiempoNoHablado = 20; l.Rellamada72h = 1; }),
            L(l => { l.DuracionSegundos = 200; l.TiempoNoHablado = 40; l.Rellamada72h = 0; }),
            L(l => l.DuracionSegundos = 300),
        ]);
        Assert.Equal(30, i.SilencioMedio);
        Assert.Equal(1, i.Rellamadas);
        Assert.Equal(2, i.RellamadaBase);
    }

    [Fact]
    public void Ranking_por_agente_trae_oleada_y_sector_y_por_formador_no()
    {
        var ll = new[] { L(), L(), L(l => { l.IdAgente = "B"; l.Agente = "Beto"; l.Oleada = "741"; l.Formador = "F2"; }) };
        var agentes = CalculadoraGaia.Ranking(ll, "agente");
        Assert.Equal(2, agentes.Count);
        Assert.Equal("740", agentes.Single(a => a.Texto == "Ana").Oleada);
        Assert.Equal(2, agentes.Single(a => a.Texto == "Ana").Indicadores.Llamadas);

        var formadores = CalculadoraGaia.Ranking(ll, "formador");
        Assert.Equal(["F1", "F2"], formadores.Select(f => f.Texto).Order());
        Assert.All(formadores, f => Assert.Equal("", f.Oleada));

        Assert.Contains(CalculadoraGaia.Ranking(ll, "oleada"), f => f.Texto == "Oleada 741");
    }

    [Fact]
    public void Tipologico_por_motivo_2_despliega_su_motivo_3()
    {
        var ll = new[]
        {
            L(l => { l.Motivo2 = "baja"; l.Motivo3 = "bajaVoluntaria"; }),
            L(l => { l.Motivo2 = "baja"; l.Motivo3 = "bajaVoluntaria"; }),
            L(l => { l.Motivo2 = "baja"; l.Motivo3 = "portabilidad"; }),
            L(l => l.Motivo2 = "comercial"),
            L(),
        };
        var t = CalculadoraGaia.Tipologico(ll, 2);
        Assert.Equal("baja", t[0].Clave);
        Assert.Equal(3, t[0].Indicadores.Llamadas);
        Assert.Equal(["bajaVoluntaria", "portabilidad"], t[0].Hijos.Select(h => h.Clave));
        Assert.Contains(t, n => n.Clave == "Sin asignar" && n.Hijos.Count == 0);
        // El nivel 3 no despliega.
        Assert.All(CalculadoraGaia.Tipologico(ll, 3), n => Assert.Empty(n.Hijos));
    }

    [Fact]
    public void Sentimiento_inicial_frente_a_final()
    {
        var s = CalculadoraGaia.SentimientoInicialFinal([
            L(l => { l.SentimientoInicial = "Neutro"; l.SentimientoFinal = "Positivo"; }),
            L(l => { l.SentimientoInicial = "Neutro"; l.SentimientoFinal = "Neutro"; }),
            L(l => { l.SentimientoInicial = "Negativo"; l.SentimientoFinal = "Neutro"; }),
        ]);
        var neutro = s.Single(f => f.Texto == "Neutro");
        Assert.Equal(2, neutro.Inicial);
        Assert.Equal(2, neutro.Final);
        Assert.Equal("Neutro", s[0].Texto);
        Assert.Equal(1, s.Single(f => f.Texto == "Positivo").Final);
    }

    [Fact]
    public void Rellamada_por_rol_ordena_por_rellamadas()
    {
        var ll = new[]
        {
            L(l => l.Rellamada72h = 1), L(l => l.Rellamada72h = 0),
            L(l => { l.Agente = "Beto"; l.Rellamada72h = 1; }), L(l => { l.Agente = "Beto"; l.Rellamada72h = 1; }),
            L(l => { l.Agente = "Beto"; l.Rellamada72h = null; }),
        };
        var r = CalculadoraGaia.RellamadaPorRol(ll, "agente");
        Assert.Equal("Beto", r[0].Texto);
        Assert.Equal(2, r[0].Rellamadas);
        Assert.Equal(2, r[0].Base);
        Assert.Equal(0.5, r[1].Fraccion);
        Assert.Single(CalculadoraGaia.RellamadaPorRol(ll, "supervisor"));
    }

    [Fact]
    public void Motivo_por_semana_cuenta_por_semana_iso()
    {
        var m = CalculadoraGaia.MotivoPorSemana([
            L(l => { l.Motivo3 = "oferta"; l.Fecha = new DateOnly(2026, 9, 7); }),
            L(l => { l.Motivo3 = "oferta"; l.Fecha = new DateOnly(2026, 9, 14); }),
            L(l => { l.Motivo3 = "oferta"; l.Fecha = new DateOnly(2026, 9, 15); }),
            L(l => { l.Motivo3 = "recobro"; l.Fecha = new DateOnly(2026, 9, 8); }),
            L(l => { l.Motivo3 = "NA"; l.Fecha = new DateOnly(2026, 9, 8); }),
        ]);
        Assert.Equal(["S37", "S38"], m.Semanas.Select(s => s.Texto));
        Assert.Equal("oferta", m.Filas[0].Motivo);
        Assert.Equal([1, 2], m.Filas[0].Valores);
        Assert.Equal(3, m.Filas[0].Total);
        Assert.Equal(2, m.Maximo);
        Assert.Equal([2, 2], m.TotalesSemana);
        Assert.Equal(4, m.Total);
    }

    [Fact]
    public void Clientes_que_vuelven_desde_tres_llamadas()
    {
        var ll = new[]
        {
            L(l => { l.IdCliente = "C1"; l.Rellamada72h = 1; l.Motivo2 = "baja"; }),
            L(l => { l.IdCliente = "C1"; l.Rellamada72h = 1; l.Motivo2 = "baja"; l.FechaHora = new DateTime(2026, 9, 9, 8, 0, 0); }),
            L(l => { l.IdCliente = "C1"; l.Rellamada72h = 0; l.Motivo2 = "comercial"; }),
            L(l => l.IdCliente = "C2"), L(l => l.IdCliente = "C2"),
            L(l => l.IdCliente = "NA"), L(l => l.IdCliente = "NA"), L(l => l.IdCliente = "NA"),
        };
        var c = CalculadoraGaia.ClientesQueVuelven(ll);
        var uno = Assert.Single(c);
        Assert.Equal("C1", uno.IdCliente);
        Assert.Equal(3, uno.Llamadas);
        Assert.Equal(2, uno.Rellamadas);
        Assert.Equal(2.0 / 3, uno.PorcentajeRellamada!.Value, 6);
        Assert.Equal("baja", uno.Motivo2);
        Assert.Equal(new DateTime(2026, 9, 9, 8, 0, 0), uno.UltimaLlamada);
    }
}
