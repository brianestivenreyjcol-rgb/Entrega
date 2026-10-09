using System.Text;
using System.Text.Json;
using CDM_Auditorias_Calidad.Servicios.Comun;
using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using Xunit;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>
/// La pantalla CDM No solución: filtros encadenados, atajos y rango de
/// fechas, y las cuatro pestañas sobre el cubo de la fixture (sin BigQuery: el
/// origen es un cubo fijo). Traídas de ranking-mvc (RankingMvc.Tests/CdmTests.cs)
/// sin las del controlador y las vistas de allí, que aquí son otros.
/// </summary>
public sealed class CdmTests
{
    /// <summary>Un origen con un cubo fijo (o sin ninguno) que apunta lo que se le pide.</summary>
    private sealed class OrigenDePrueba : IOrigenCdm
    {
        private readonly Cubo? _cubo;

        public OrigenDePrueba(Cubo? cubo) => _cubo = cubo;

        public int Construcciones { get; private set; }
        public int Actualizaciones { get; private set; }
        public List<string> Claves { get; } = new();

        public (Cubo Cubo, string Version) Actual()
            => _cubo is null ? throw new SinDatos("Todavía no hay datos de No solución cargados.") : (_cubo, "v1");

        public T Respuesta<T>(string clave, Func<Cubo, T> calcular) where T : class
        {
            Claves.Add(clave);
            return calcular(Actual().Cubo);
        }

        public void AsegurarConstruccion() => Construcciones++;
        public void RenovarSiViejo() { }

        public bool PedirActualizacion()
        {
            Actualizaciones++;
            return Actualizaciones == 1;
        }

        public EstadoConstruccion Estado => new() { EnCurso = _cubo is null, Progreso = "tramo 1 de 2" };
        public bool Construyendo => _cubo is null;
    }

    private static readonly Lazy<string> CuboJson = new(() =>
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Fixtures", "nosolucion_python.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(ruta));
        return doc.RootElement.GetProperty("cubo").GetRawText();
    });

    private static Cubo CuboDeLaFixture() => JsonSerializer.Deserialize<Cubo>(CuboJson.Value)!;

    private static (ServicioCdm Servicio, OrigenDePrueba Origen) Pantalla(bool conCubo = true)
    {
        var origen = new OrigenDePrueba(conCubo ? CuboDeLaFixture() : null);
        return (new ServicioCdm(origen), origen);
    }

    private static OpcionAgente Ag(string agente, string tl, string sup, long llamadas) => new(agente, tl, sup, llamadas, 0);

    private static readonly OpcionAgente[] Plantilla =
    {
        Ag("Zoe", "TL Uno", "Sup Norte", 10),
        Ag("Álvaro", "TL Uno", "Sup Norte", 5),
        Ag("Berta", "TL Dos", "Sup Norte", 7),
        Ag("Carlos", "TL Tres", "Sup Sur", 20),
        Ag("Dora", "TL Tres", "Sup Sur", 1),
    };

    // ------------------------------------------------------------------
    // Encadenado supervisor → TL → agente
    // ------------------------------------------------------------------

    [Fact]
    public void Sin_nada_elegido_las_listas_suman_por_persona_y_van_por_orden_alfabetico()
    {
        var c = FiltrosCdm.Encadenar(Plantilla, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        Assert.Equal(new[] { ("Sup Norte", 22L), ("Sup Sur", 21L) }, c.Supervisores.Select(o => (o.Valor, o.Llamadas!.Value)));
        Assert.Equal(new[] { "TL Dos", "TL Tres", "TL Uno" }, c.Tls.Select(o => o.Valor));
        Assert.Equal(15, c.Tls.Single(o => o.Valor == "TL Uno").Llamadas);
        // En español la «Á» va con la «A», no detrás de la «Z».
        Assert.Equal(new[] { "Álvaro", "Berta", "Carlos", "Dora", "Zoe" }, c.Agentes.Select(o => o.Valor));
        Assert.Empty(c.Supervisor);
    }

    [Fact]
    public void Elegir_un_supervisor_reduce_las_listas_y_descarta_lo_que_ya_no_esta()
    {
        var c = FiltrosCdm.Encadenar(Plantilla, new[] { "Sup Norte" }, new[] { "TL Tres", "TL Uno" }, new[] { "Carlos", "Zoe", "Berta" });

        Assert.Equal(new[] { "Sup Norte" }, c.Supervisor);
        Assert.Equal(new[] { "TL Dos", "TL Uno" }, c.Tls.Select(o => o.Valor));
        Assert.Equal(new[] { "TL Uno" }, c.Tl);                       // TL Tres es de Sup Sur
        Assert.Equal(new[] { "Álvaro", "Zoe" }, c.Agentes.Select(o => o.Valor));
        Assert.Equal(new[] { "Zoe" }, c.Agente);                      // Carlos y Berta no son de TL Uno
        // La lista de supervisores no se reduce: se puede elegir otro más.
        Assert.Equal(2, c.Supervisores.Count);
    }

    [Fact]
    public void Un_supervisor_que_no_existe_se_descarta_y_no_filtra()
    {
        var c = FiltrosCdm.Encadenar(Plantilla, new[] { "Nadie" }, Array.Empty<string>(), new[] { "Dora" });
        Assert.Empty(c.Supervisor);
        Assert.Equal(3, c.Tls.Count);
        Assert.Equal(new[] { "Dora" }, c.Agente);
    }

    [Fact]
    public void Sin_opciones_lo_elegido_se_queda_y_es_lo_que_ofrecen_las_listas()
    {
        var c = FiltrosCdm.SinEncadenar(new[] { "Sup Norte" }, Array.Empty<string>(), new[] { "Zoe" });
        Assert.Equal(new[] { "Sup Norte" }, c.Supervisor);
        Assert.Equal(new[] { "Zoe" }, c.Agentes.Select(o => o.Valor));
        Assert.Null(c.Agentes[0].Llamadas);
    }

    // ------------------------------------------------------------------
    // Fechas y atajos
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("todo", "2026-06-30", "2026-09-27")]
    [InlineData("7", "2026-09-21", "2026-09-27")]
    [InlineData("30", "2026-08-29", "2026-09-27")]
    [InlineData("mes", "2026-08-01", "2026-08-31")]
    public void Los_atajos_dan_el_rango_del_informe(string atajo, string desde, string hasta)
        => Assert.Equal((desde, hasta), FiltrosCdm.RangoDeAtajo(atajo, "2026-06-30", "2026-09-27"));

    [Fact]
    public void El_mes_cerrado_es_el_del_ultimo_dia_si_acaba_ese_dia()
    {
        Assert.Equal(("2026-09-01", "2026-09-30"), FiltrosCdm.MesCerrado("2026-09-30"));
        Assert.Equal(("2025-12-01", "2025-12-31"), FiltrosCdm.MesCerrado("2026-01-15"));
        Assert.Equal(("2024-02-01", "2024-02-29"), FiltrosCdm.MesCerrado("2024-03-01"));
    }

    [Fact]
    public void Los_atajos_no_empiezan_antes_del_primer_dia_con_datos()
    {
        Assert.Equal(("2026-09-01", "2026-09-14"), FiltrosCdm.RangoDeAtajo("30", "2026-09-01", "2026-09-14"));
        Assert.Equal(("2026-08-15", "2026-08-31"), FiltrosCdm.RangoDeAtajo("mes", "2026-08-15", "2026-09-10"));
    }

    [Fact]
    public void El_atajo_activo_es_el_que_coincide_con_el_rango()
    {
        Assert.Equal("30", FiltrosCdm.AtajoActivo("2026-08-29", "2026-09-27", "2026-06-30", "2026-09-27"));
        Assert.Equal("todo", FiltrosCdm.AtajoActivo("2026-06-30", "2026-09-27", "2026-06-30", "2026-09-27"));
        Assert.Null(FiltrosCdm.AtajoActivo("2026-08-28", "2026-09-27", "2026-06-30", "2026-09-27"));
    }

    [Fact]
    public void El_rango_por_defecto_son_los_ultimos_30_dias_y_lo_que_se_sale_vuelve_al_defecto()
    {
        Assert.Equal(("2026-08-29", "2026-09-27"), FiltrosCdm.AcotarFechas(null, null, "2026-06-30", "2026-09-27"));
        Assert.Equal(("2026-08-29", "2026-09-27"), FiltrosCdm.AcotarFechas("2026-01-01", "2027-01-01", "2026-06-30", "2026-09-27"));
        // Con solo la final, la inicial son 30 días hasta ella.
        Assert.Equal(("2026-08-12", "2026-09-10"), FiltrosCdm.AcotarFechas(null, "2026-09-10", "2026-06-30", "2026-09-27"));
        // Una inicial detrás de la final no se corrige: la pantalla lo avisa.
        Assert.Equal(("2026-09-20", "2026-09-10"), FiltrosCdm.AcotarFechas("2026-09-20", "2026-09-10", "2026-06-30", "2026-09-27"));
    }

    [Theory]
    [InlineData("2026-09-01", true)]
    [InlineData("2026-9-1", false)]
    [InlineData("2026-02-30", false)]
    [InlineData("hoy", false)]
    public void Solo_valen_fechas_AAAA_MM_DD_que_existen(string texto, bool valida)
        => Assert.Equal(valida, FiltrosCdm.EsFecha(texto));

    // ------------------------------------------------------------------
    // Contexto: filtros resueltos contra el cubo
    // ------------------------------------------------------------------

    [Fact]
    public void Sin_cubo_sale_preparando_y_lanza_la_construccion()
    {
        var (servicio, origen) = Pantalla(conCubo: false);
        var ctx = servicio.Contexto(new PeticionCdm());

        Assert.True(ctx.Preparando);
        Assert.False(ctx.Listo);
        Assert.Equal("tramo 1 de 2", ctx.Progreso);
        Assert.Equal(1, origen.Construcciones);
        Assert.Null(servicio.CsvLlamadas(new PeticionCdm(), null, null));
    }

    [Fact]
    public void Sin_filtros_se_aplica_el_rango_por_defecto_y_nada_viaja_en_la_url()
    {
        var (servicio, _) = Pantalla();
        var ctx = servicio.Contexto(new PeticionCdm());

        Assert.True(ctx.Listo);
        var f = ctx.Filtros!;
        // La fixture tiene del 1 al 14 de septiembre: 30 días hacia atrás se acotan al primero.
        Assert.Equal(("2026-09-01", "2026-09-14"), (f.Desde, f.Hasta));
        Assert.Equal(14, f.Dias);
        Assert.Null(f.DesdePedido);
        Assert.Null(f.HastaPedido);
        Assert.False(f.HayFiltros);
        Assert.Empty(f.Parametros());
        Assert.Equal(new[] { "MASMOVIL", "YOIGO" }, ctx.OpcionesMarca.Select(o => o.Valor));
        Assert.All(ctx.OpcionesMarca, o => Assert.NotNull(o.Llamadas));
    }

    [Fact]
    public void Las_personas_se_encadenan_con_las_opciones_de_las_fechas_y_marcas_elegidas()
    {
        var (servicio, origen) = Pantalla();
        var ctx = servicio.Contexto(new PeticionCdm
        {
            Desde = "2026-09-03",
            Hasta = "2026-09-10",
            Marca = new() { "YOIGO", "ORANGE", "YOIGO" },
            Supervisor = new() { "Sup Uno" },
            Tl = new() { "TL B", "TL A" },
            Agente = new() { "O'Neil" },
        });

        var f = ctx.Filtros!;
        Assert.Equal(new[] { "YOIGO" }, f.Marca);                   // la marca que no existe se descarta
        Assert.Equal(new[] { "Sup Uno" }, f.Supervisor);
        Assert.Equal(new[] { "TL A" }, f.Tl);                        // TL B es de Sup Dos
        Assert.Empty(f.Agente);                                      // O'Neil es de TL B
        Assert.Equal(new[] { "TL A" }, ctx.OpcionesTl.Select(o => o.Valor));
        Assert.Equal(new[] { "2026-09-03", "2026-09-10" }, new[] { f.DesdePedido, f.HastaPedido });
        Assert.Equal(
            "desde=2026-09-03&hasta=2026-09-10&supervisor=Sup%20Uno&tl=TL%20A&marca=YOIGO",
            FiltrosCdm.Consulta(f.Parametros()));
        // Las opciones se piden con la misma llave que la API anterior: sin personas.
        Assert.Contains("('opciones', 30, '2026-09-03', '2026-09-10', ('YOIGO',), None, None, None, None)", origen.Claves);
    }

    [Fact]
    public void Una_fecha_mal_escrita_se_avisa_y_se_usa_la_de_por_defecto()
    {
        var (servicio, _) = Pantalla();
        var ctx = servicio.Contexto(new PeticionCdm { Desde = "2026-9-5" });

        Assert.Equal("2026-09-01", ctx.Filtros!.Desde);
        Assert.Contains(ctx.Avisos, a => a.Contains("«2026-9-5»") && a.Contains("AAAA-MM-DD"));
    }

    [Fact]
    public void Un_rango_al_reves_se_ensena_como_aviso_y_no_como_error()
    {
        var (servicio, _) = Pantalla();
        var peticion = new PeticionCdm { Desde = "2026-09-10", Hasta = "2026-09-05", Supervisor = new() { "Sup Uno" } };
        var ctx = servicio.Contexto(peticion);

        Assert.True(ctx.Listo);
        Assert.Contains(ctx.Avisos, a => a.Contains("posterior a la final"));
        Assert.Equal(new[] { "Sup Uno" }, ctx.Filtros!.Supervisor);   // sin opciones, lo elegido se queda
        var vista = servicio.Resumen(ctx);
        Assert.Null(vista.Datos);
        Assert.Contains("posterior a la final", vista.Aviso);
        Assert.Throws<PeticionInvalida>(() => servicio.CsvLlamadas(peticion, null, null));
    }

    // ------------------------------------------------------------------
    // Las pestañas
    // ------------------------------------------------------------------

    [Fact]
    public void El_resumen_es_el_de_agregados()
    {
        var (servicio, _) = Pantalla();
        var ctx = servicio.Contexto(new PeticionCdm());
        var vista = servicio.Resumen(ctx);
        var esperado = Agregados.Resumen(CuboDeLaFixture(), ctx.Filtros!.ParaAgregados());
        Assert.Equal(JsonSerializer.Serialize(esperado), JsonSerializer.Serialize(vista.Datos));
    }

    [Fact]
    public void Equipos_ordena_busca_y_conserva_el_puesto_del_ranking()
    {
        var (servicio, _) = Pantalla();
        var ctx = servicio.Contexto(new PeticionCdm());
        var r = Agregados.Equipos(CuboDeLaFixture(), "agente", ctx.Filtros!.ParaAgregados(), suelo: 1);
        Assert.True(r.Filas.Count >= 3);

        var porNombre = ServicioCdm.TablaEquipos(r, "nombre", null, null);
        Assert.Equal("asc", porNombre.Direccion);
        Assert.Equal(r.Filas.Select(f => f.Nombre).OrderBy(n => n, StringComparer.Create(new System.Globalization.CultureInfo("es-ES"), false)),
                     porNombre.Filas.Select(f => f.Fila.Nombre));
        Assert.All(porNombre.Filas, x => Assert.Equal(r.Filas.FindIndex(f => f.Nombre == x.Fila.Nombre) + 1, x.Puesto));
        Assert.All(porNombre.Filas, x => Assert.Equal($"{x.Fila.Tl} · {x.Fila.Supervisor}", x.Detalle));

        var porDefecto = ServicioCdm.TablaEquipos(r, "loquesea", "arriba", null);
        Assert.Equal(("pct", "desc"), (porDefecto.Orden, porDefecto.Direccion));
        Assert.Equal(r.Filas.Select(f => f.Nombre), porDefecto.Filas.Select(f => f.Fila.Nombre));

        var buscada = ServicioCdm.TablaEquipos(r, null, null, "  tl a ");
        Assert.NotEmpty(buscada.Filas);
        Assert.All(buscada.Filas, x => Assert.Equal("TL A", x.Fila.Tl));
        Assert.Equal("tl a", buscada.Busca);
    }

    [Fact]
    public void Motivos_e_internet_traen_las_llamadas_de_ejemplo_de_cada_desglose()
    {
        var (servicio, _) = Pantalla();
        var ctx = servicio.Contexto(new PeticionCdm());

        var motivos = servicio.Motivos(ctx).Datos!;
        Assert.NotEmpty(motivos.Motivos.N3);
        Assert.All(motivos.Motivos.N3, x => Assert.True(motivos.Muestras.ContainsKey(x.Nombre)));
        Assert.All(motivos.Muestras.Values, m => Assert.True(m.Muestras.Count <= Agregados.Muestras));

        var internet = servicio.Internet(ctx).Datos!;
        Assert.NotEmpty(internet.Internet.Impedimentos);
        foreach (var imp in internet.Internet.Impedimentos)
        {
            var m = internet.Muestras[imp.Bit];
            Assert.Equal(imp.Nombre, m.Nombre);
            Assert.All(m.Muestras, x => Assert.Contains(imp.Nombre, x.Impedimentos));
            // Las muestras (y su CSV) son solo de sinAccesoInternet, como la barra.
            var esperado = Agregados.MuestrasImpedimento(CuboDeLaFixture(), imp.Bit, ctx.Filtros!.ParaAgregados(), Agregados.TipologiaInternet);
            Assert.Equal(esperado.Total, m.Total);
        }
    }

    [Fact]
    public void En_internet_las_llamadas_de_un_impedimento_son_solo_de_sinAccesoInternet()
    {
        var (servicio, _) = Pantalla();
        var f = servicio.Contexto(new PeticionCdm()).Filtros!.ParaAgregados();
        var cubo = CuboDeLaFixture();
        var internet = Agregados.Internet(cubo, f);
        Assert.NotEmpty(internet.Impedimentos);
        var n3 = Array.IndexOf(Agregados.CabLlamadas, "motivo_n3");
        foreach (var imp in internet.Impedimentos)
        {
            var soloInternet = Agregados.MuestrasImpedimento(cubo, imp.Bit, f, Agregados.TipologiaInternet);
            var todas = Agregados.MuestrasImpedimento(cubo, imp.Bit, f);
            Assert.True(soloInternet.Total <= todas.Total);

            var (_, csv) = Agregados.CsvLlamadas(cubo, f, Agregados.TipologiaInternet, imp.Bit);
            var filas = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Skip(1).ToList();
            Assert.Equal(soloInternet.Total, filas.Count);
            Assert.All(filas, fila => Assert.Equal(Agregados.TipologiaInternet, fila.Split(';')[n3]));
        }
    }

    // ------------------------------------------------------------------
    // Exportar
    // ------------------------------------------------------------------

    [Fact]
    public void El_csv_de_llamadas_es_el_de_agregados_con_su_nombre()
    {
        var (servicio, _) = Pantalla();
        var fichero = servicio.CsvLlamadas(new PeticionCdm { Marca = new() { "YOIGO" } }, "sinAccesoInternet", null)!;
        var (nombre, texto) = Agregados.CsvLlamadas(
            CuboDeLaFixture(), servicio.Contexto(new PeticionCdm { Marca = new() { "YOIGO" } }).Filtros!.ParaAgregados(), "sinAccesoInternet");

        Assert.Equal("nosolucion_llamadas_20260901-20260914_filtrado.csv", fichero.Nombre);
        Assert.Equal(nombre, fichero.Nombre);
        Assert.Equal(Encoding.UTF8.GetBytes(texto), fichero.Contenido);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, fichero.Contenido[..3]);
        Assert.Throws<PeticionInvalida>(() => servicio.CsvLlamadas(new PeticionCdm(), new string('x', 121), null));
        Assert.Throws<PeticionInvalida>(() => servicio.CsvLlamadas(new PeticionCdm(), "no existe", null));
    }

    [Fact]
    public void El_csv_de_equipos_lleva_las_columnas_del_nivel_y_el_formato_de_excel()
    {
        var (servicio, _) = Pantalla();
        var fichero = servicio.CsvEquipos(new PeticionCdm(), "agente", null, null, null)!;
        var texto = Encoding.UTF8.GetString(fichero.Contenido);

        Assert.Equal("nosolucion_agentes_20260901-20260914.csv", fichero.Nombre);
        Assert.StartsWith("﻿agente;team_leader;supervisor;llamadas;encuestadas;cobertura_pct;no_solucionadas;pct_no_solucion;desvio_pct\r\n\"", texto);
        Assert.DoesNotContain("\r\n", texto[^2..]);
        Assert.All(texto.TrimStart('﻿').Split("\r\n").Skip(1), l => Assert.StartsWith("\"", l));

        var tl = Encoding.UTF8.GetString(servicio.CsvEquipos(new PeticionCdm(), "tl", null, null, null)!.Contenido);
        Assert.StartsWith("﻿team_leader;supervisor;agentes;llamadas;", tl);
        var tipologias = servicio.CsvTipologias(new PeticionCdm())!;
        Assert.Equal("nosolucion_tipologias_20260901-20260914.csv", tipologias.Nombre);
        Assert.StartsWith("﻿tipologia_n3;encuestadas;no_solucionadas;pct_no_solucion;desvio_pct;peso_pct\r\n",
            Encoding.UTF8.GetString(tipologias.Contenido));
    }

    // ------------------------------------------------------------------
    // Filtros recordados en la cookie
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(12.345, "12,35")]
    [InlineData(12.0, "12")]
    [InlineData(-0.125, "-0,12")]   // el medio exacto sube, como Math.round
    [InlineData(null, "")]
    public void Los_porcentajes_del_csv_van_a_dos_decimales_con_coma(double? valor, string esperado)
        => Assert.Equal(esperado, ServicioCdm.Dec2(valor));
}
