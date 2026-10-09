using ClosedXML.Excel;
using CDM_Auditorias_Calidad.Servicios.Gaia;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>GAIA Formación: lectura del Excel de nómina, consulta, medidas y filtros.</summary>
public class GaiaTests
{
    // ------------------------------------------------------------------
    // Excel de nómina
    // ------------------------------------------------------------------

    private static IXLWorksheet HojaEjemplo(XLWorkbook libro)
    {
        var h = libro.AddWorksheet("Oleadas");
        string[] cab = ["Sector", "Supervisor", "Coordinador", "Personal", "Cargo", "Legajo", "1 Preconexion", "2 Preconexion",
            "Aseguramiento 1", "Formador", "Oleada", "ID_Agente ", "Ventas", "CDA"];
        for (var i = 0; i < cab.Length; i++) h.Cell(1, i + 1).Value = cab[i];

        void Fila(int r, string nombre, object? id, DateTime? d1, DateTime? d2, DateTime? a1, int oleada)
        {
            h.Cell(r, 1).Value = "YGMM"; h.Cell(r, 2).Value = "Sup A"; h.Cell(r, 4).Value = nombre;
            h.Cell(r, 5).Value = "Agente en Capacitacion"; h.Cell(r, 6).Value = 1000 + r;
            if (d1 is { } x1) h.Cell(r, 7).Value = x1;
            if (d2 is { } x2) h.Cell(r, 8).Value = x2;
            if (a1 is { } x3) h.Cell(r, 9).Value = x3;
            h.Cell(r, 10).Value = "Formador X"; h.Cell(r, 11).Value = oleada;
            switch (id)
            {
                case string s: h.Cell(r, 12).Value = s; break;
                case int n: h.Cell(r, 12).Value = n; break;
            }
        }
        Fila(2, "Ana", "abc-1", new DateTime(2026, 9, 1), new DateTime(2026, 9, 2), new DateTime(2026, 9, 8), 740);
        Fila(3, "Luis", 2420515, new DateTime(2026, 9, 1), null, new DateTime(2026, 9, 9), 741);
        Fila(4, "Sin Id", null, new DateTime(2026, 9, 1), null, null, 741);
        Fila(5, "Ana bis", "abc-1", null, new DateTime(2026, 9, 3), null, 740);
        return h;
    }

    [Fact]
    public void Excel_lee_agentes_con_sus_dias_y_avisa()
    {
        using var libro = new XLWorkbook();
        var n = LectorNominaGaia.Leer(HojaEjemplo(libro), new DateTime(2026, 10, 6));

        Assert.Equal(2, n.Agentes.Count);
        var ana = n.Agentes.Single(a => a.Id == "abc-1");
        Assert.Equal("Ana", ana.Nombre);
        Assert.Equal("740", ana.Oleada);
        // Los días de las dos filas de Ana se juntan.
        Assert.Equal(4, ana.Dias.Count);
        Assert.Equal("Aseguramiento 1", ana.Dias[new DateOnly(2026, 9, 8)]);
        Assert.Equal("2 Preconexion", ana.Dias[new DateOnly(2026, 9, 3)]);

        var luis = n.Agentes.Single(a => a.Nombre == "Luis");
        Assert.Equal("2420515", luis.Id); // número sin decimales
        Assert.Equal(2, luis.Dias.Count);

        Assert.Equal(6, n.Pares);
        Assert.Contains(n.Avisos, a => a.Contains("sin", StringComparison.OrdinalIgnoreCase) || a.Contains("no tiene"));
        Assert.Contains(n.Avisos, a => a.Contains("más de una fila"));
    }

    [Fact]
    public void Excel_sin_columna_de_id_falla_con_mensaje()
    {
        using var libro = new XLWorkbook();
        var h = libro.AddWorksheet("Oleadas");
        h.Cell(1, 1).Value = "Personal";
        h.Cell(2, 1).Value = "Ana";
        var ex = Assert.Throws<InvalidDataException>(() => LectorNominaGaia.Leer(h, DateTime.Now));
        Assert.Contains("ID_Agente", ex.Message);
    }

    // ------------------------------------------------------------------
    // Consulta
    // ------------------------------------------------------------------

    [Fact]
    public void Consulta_lleva_un_par_por_agente_y_dia_y_escapa_comillas()
    {
        var ag = new AgenteGaia { Id = "o'brien" };
        ag.Dias[new DateOnly(2026, 9, 1)] = "1 Preconexion";
        ag.Dias[new DateOnly(2026, 9, 8)] = "Aseguramiento 1";
        var sql = FuenteGaia.ArmarConsulta("SELECT * FROM UNNEST([{{PARES}}])", [ag]);

        Assert.Contains("STRUCT('o\\'brien' AS id_agente, DATE '2026-09-01' AS dia)", sql);
        Assert.Contains("DATE '2026-09-08'", sql);
        Assert.DoesNotContain("{{PARES}}", sql);
    }

    [Fact]
    public void Consulta_sin_marcador_falla()
        => Assert.Throws<InvalidOperationException>(() => FuenteGaia.ArmarConsulta("SELECT 1", []));

    [Fact]
    public void Fila_de_bigquery_se_traduce_y_se_cruza_con_la_nomina()
    {
        var ag = new AgenteGaia { Id = "A1", Nombre = "Ana", Sector = "YGMM", Oleada = "740" };
        ag.Dias[new DateOnly(2026, 9, 1)] = "1 Preconexion";
        var fila = new Dictionary<string, object?>
        {
            ["IdConversacion"] = "c1", ["Fecha"] = new DateTime(2026, 9, 1), ["FechaHora"] = "2026-09-01 10:30:00",
            ["IdAgente"] = "A1", ["Marca"] = "YOIGO", ["RiesgoChurn"] = "high", ["SentimientoFinal"] = "positive",
            ["Contexto"] = "insufficient", ["EstadoResolucion"] = "resolved", ["Rellamada72h"] = 1L,
            ["DuracionSegundos"] = 120.5, ["IntentoVenta"] = true, ["EncuestaEnviada"] = false,
        };
        var l = FuenteGaia.Convertir(fila, new Dictionary<string, AgenteGaia> { ["A1"] = ag });

        Assert.Equal("Ana", l.Agente);
        Assert.Equal("1 Preconexion", l.TipoConexion);
        Assert.Equal("Alto", l.RiesgoChurn);
        Assert.Equal("Positivo", l.SentimientoFinal);
        Assert.Equal("Insuficiente", l.Contexto);
        Assert.Equal("Resuelto", l.EstadoResolucion);
        Assert.Equal(1, l.Rellamada72h);
        Assert.Equal(new DateTime(2026, 9, 1, 10, 30, 0), l.FechaHora);
        Assert.True(l.IntentoVenta);
    }

    // ------------------------------------------------------------------
    // Medidas
    // ------------------------------------------------------------------

    private static LlamadaGaia L(Action<LlamadaGaia>? ajuste = null)
    {
        var l = new LlamadaGaia { IdConversacion = Guid.NewGuid().ToString(), IdAgente = "A1", Agente = "Ana", Marca = "YOIGO", Fecha = new DateOnly(2026, 9, 1) };
        ajuste?.Invoke(l);
        return l;
    }

    [Fact]
    public void Rellamada_72_y_24_horas()
    {
        var ll = new[]
        {
            L(l => { l.Rellamada72h = 1; l.MinutosSiguienteLlamada = 600; }),
            L(l => { l.Rellamada72h = 1; l.MinutosSiguienteLlamada = 3000; }),
            L(l => l.Rellamada72h = 0),
            L(l => l.Rellamada72h = 0),
            L(), // sin dato: fuera de la base
        };
        var i = CalculadoraGaia.Calcular(ll);
        Assert.Equal(0.5, i.Rellamada72);
        Assert.Equal(0.25, i.Rellamada24);
    }

    [Fact]
    public void Transferencia_divide_entre_su_propio_campo()
    {
        // El PBI dividía entre «Transferencia = 1 o Rellamada72hr = 0»; aquí, entre Transferencia 0 o 1.
        var ll = new[]
        {
            L(l => { l.Transferencia = 1; l.Rellamada72h = 1; }),
            L(l => { l.Transferencia = 0; l.Rellamada72h = 1; }),
            L(l => { l.Transferencia = 0; l.Rellamada72h = 1; }),
            L(l => { l.Transferencia = 0; l.Rellamada72h = 1; }),
        };
        Assert.Equal(0.25, CalculadoraGaia.Calcular(ll).Transferencia);
    }

    [Fact]
    public void No_solucion_churn_ventas_y_ofrecimientos()
    {
        var ll = new[]
        {
            L(l => { l.EncuestaSolucion = 2; l.RiesgoChurn = "Alto"; l.IntentoVenta = true; l.PosibleVentaEntrante = true; l.AlineacionOferta = "true"; l.TieneVenta = true; }),
            L(l => { l.EncuestaSolucion = 1; l.RiesgoChurn = "Sin riesgo"; l.IntentoVenta = true; l.PosibleVentaEntrante = false; l.AlineacionOferta = "false"; }),
            L(l => { l.EncuestaSolucion = 0; l.RiesgoChurn = "Moderado"; l.IntentoVenta = false; }),
            L(l => l.RiesgoChurn = "Alto"),
        };
        var i = CalculadoraGaia.Calcular(ll);
        Assert.Equal(0.5, i.NoSolucion);
        Assert.Equal(0.5, i.Churn);
        Assert.Equal(0.5, i.Ofrecimientos);
        Assert.Equal(0.25, i.Reactivo);
        Assert.Equal(0.25, i.Proactivo);
        Assert.Equal(1, i.OfrecimientosAlineados);
        Assert.Equal(1, i.OfrecimientosNoAlineados);
        Assert.Equal(0.25, i.PorcentajeVentas);
        Assert.Equal(2, i.RespuestasEncuesta);
    }

    [Fact]
    public void Duraciones_contexto_y_encuestas_enviadas()
    {
        var ll = new[]
        {
            L(l => { l.DuracionSegundos = 30; l.Contexto = "Insuficiente"; l.EncuestaEnviada = true; }),
            L(l => { l.DuracionSegundos = 120; l.Contexto = "Suficiente"; }),
            L(l => { l.DuracionSegundos = 300; l.Marca = "JAZZTEL"; l.EncuestaEnviada = false; }),
            L(l => { l.DuracionSegundos = 150; l.Marca = "MASMOVIL"; }),
        };
        var i = CalculadoraGaia.Calcular(ll);
        Assert.Equal(150, i.Tmo);
        Assert.Equal(0.25, i.Menores60);
        Assert.Equal(0.5, i.Entre60y180);
        Assert.Equal(0.25, i.SinContexto);
        // Solo YOIGO y MASMOVIL cuentan para el envío de encuestas: 1 de 3.
        Assert.Equal(1.0 / 3, i.EnvioEncuestas!.Value, 6);
    }

    [Fact]
    public void Adherencia_pondera_los_seis_criterios()
    {
        // Todo «yes» → 100 %; solo falla «solucionó» (25 %) en la mitad → 87,5 %.
        LlamadaGaia Todo(string solucion) => L(l =>
        {
            l.CalificacionSaludo = "yes"; l.CalificacionLenguajeClaro = "Yes"; l.CalificacionSolucion = solucion;
            l.CalificacionResumen = "yes"; l.CalificacionConfirmacion = "yes"; l.CalificacionCierre = "yes";
        });
        Assert.Equal(1.0, CalculadoraGaia.Estilo([Todo("yes")]).Adherencia!.Value, 6);
        var e = CalculadoraGaia.Estilo([Todo("yes"), Todo("no")]);
        Assert.Equal(0.5, e.Soluciono);
        Assert.Equal(0.875, e.Adherencia!.Value, 6);
    }

    [Fact]
    public void Adherencia_sin_calificaciones_es_nula_y_criterio_sin_base_cuenta_cero()
    {
        Assert.Null(CalculadoraGaia.Estilo([L()]).Adherencia);
        var solo = CalculadoraGaia.Estilo([L(l => l.CalificacionSaludo = "yes")]);
        Assert.Equal(0.10, solo.Adherencia!.Value, 6);
    }

    [Fact]
    public void Escucho_y_busco_informacion()
    {
        Assert.Equal("1", CalculadoraGaia.Escucho(L(l => { l.PorcentajeHabla = 50; l.VecesSePisaron = 2; l.CalificacionReconocimiento = "yes"; })));
        Assert.Equal("0", CalculadoraGaia.Escucho(L(l => { l.PorcentajeHabla = 85; l.VecesSePisaron = 2; l.CalificacionReconocimiento = "yes"; })));
        Assert.Equal("0", CalculadoraGaia.Escucho(L(l => l.CalificacionReconocimiento = "notApplicable")));
        Assert.Equal("N/A", CalculadoraGaia.Escucho(L()));

        Assert.Equal(1, CalculadoraGaia.BuscoInformacion(L(l => { l.DuracionSegundos = 100; l.TiempoNoHablado = 50; })));
        Assert.Equal(0, CalculadoraGaia.BuscoInformacion(L(l => { l.DuracionSegundos = 100; l.TiempoNoHablado = 51; })));
        Assert.Null(CalculadoraGaia.BuscoInformacion(L(l => l.DuracionSegundos = 100)));
    }

    [Fact]
    public void Sentimiento_final()
    {
        var i = CalculadoraGaia.Calcular([L(l => l.SentimientoFinal = "Positivo"), L(l => l.SentimientoFinal = "Negativo"),
            L(l => l.SentimientoFinal = "Neutro"), L(l => l.SentimientoFinal = "Positivo"), L()]);
        Assert.Equal(0.5, i.SentimientoPositivoFinal);
        Assert.Equal(0.25, i.SentimientoNegativoFinal);
    }

    [Fact]
    public void Etapas_en_orden_de_formacion()
    {
        var etapas = CalculadoraGaia.PorEtapa([L(l => l.TipoConexion = "Aseguramiento 1"), L(l => l.TipoConexion = "1 Preconexion"),
            L(l => l.TipoConexion = "3 Preconexion")]);
        Assert.Equal(["1 Preconexion", "3 Preconexion", "Aseguramiento 1"], etapas.Select(e => e.Clave));
    }

    // ------------------------------------------------------------------
    // Filtros
    // ------------------------------------------------------------------

    [Fact]
    public void Filtros_en_cascada_y_rango_por_defecto()
    {
        var ll = new List<LlamadaGaia>
        {
            L(l => { l.Fecha = new DateOnly(2026, 9, 1); l.Sector = "YGMM"; l.Marca = "YOIGO"; }),
            L(l => { l.Fecha = new DateOnly(2026, 9, 2); l.Sector = "YGMM"; l.Marca = "MASMOVIL"; }),
            L(l => { l.Fecha = new DateOnly(2026, 9, 5); l.Sector = "Orange"; l.Marca = "ORANGE"; l.IdAgente = "B"; l.Agente = "Beto"; }),
        };
        var todo = FiltrosGaia.Resolver(ll, new PeticionGaia());
        Assert.Equal(3, todo.Llamadas.Count);
        Assert.Equal(new DateOnly(2026, 9, 1), todo.Desde);
        Assert.False(todo.HayFiltros);

        var ygmm = FiltrosGaia.Resolver(ll, new PeticionGaia { Sector = ["YGMM"] });
        Assert.Equal(2, ygmm.Llamadas.Count);
        // La marca se reduce a las del sector; el sector sigue enseñando todas sus opciones.
        Assert.Equal(2, ygmm.Desplegables.Single(g => g.Campo == "marca").Opciones.Count);
        Assert.Equal(2, ygmm.Desplegables.Single(g => g.Campo == "sector").Opciones.Count);
        Assert.Contains(ygmm.Parametros(), p => p.Key == "sector" && p.Value == "YGMM");

        var rango = FiltrosGaia.Resolver(ll, new PeticionGaia { Desde = "2026-09-02", Hasta = "2026-12-31" });
        Assert.Equal(2, rango.Llamadas.Count);
        Assert.Equal(new DateOnly(2026, 9, 5), rango.Hasta); // acotado a lo que hay
    }
}
