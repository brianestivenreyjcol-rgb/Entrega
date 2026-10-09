using System.Text.Json;
using System.Text.Json.Nodes;
using CDM_Auditorias_Calidad.Servicios.Comun;
using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using Xunit;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>
/// No solución en .NET contra lo que devuelve el Python de referencia
/// (Fixtures/nosolucion_python.json, generado con
/// herramientas/contrato/fixtures_nosolucion.py): el reparto por días, el
/// cubo ensamblado, cada vista con varios filtros, los errores y las claves
/// del ETag.
/// </summary>
public sealed class NoSolucionTests
{
    private static readonly Lazy<JsonElement> Fixture = new(() =>
    {
        var ruta = Path.Combine(AppContext.BaseDirectory, "Fixtures", "nosolucion_python.json");
        return JsonDocument.Parse(File.ReadAllText(ruta)).RootElement.Clone();
    });

    private static string S(JsonElement e) => e.GetString() ?? "";
    private static int I(JsonElement e) => e.GetInt32();
    private static List<string> Ls(JsonElement e) => e.EnumerateArray().Select(S).ToList();

    /// <summary>Compara dos JSON campo a campo y devuelve la primera diferencia, o null.</summary>
    private static string? PrimeraDiferencia(JsonNode? a, JsonNode? b, string ruta = "")
    {
        switch (a, b)
        {
            case (null, null):
                return null;
            case (null, _):
            case (_, null):
                return $"{ruta}: Python={a?.ToJsonString() ?? "null"} .NET={b?.ToJsonString() ?? "null"}";
            case (JsonObject oa, JsonObject ob):
                foreach (var clave in oa.Select(p => p.Key).Union(ob.Select(p => p.Key)))
                {
                    if (!oa.ContainsKey(clave)) return $"{ruta}.{clave}: sobra en .NET";
                    if (!ob.ContainsKey(clave)) return $"{ruta}.{clave}: falta en .NET";
                    var d = PrimeraDiferencia(oa[clave], ob[clave], $"{ruta}.{clave}");
                    if (d is not null) return d;
                }
                return null;
            case (JsonArray la, JsonArray lb):
                if (la.Count != lb.Count) return $"{ruta}[]: {la.Count} elementos en Python, {lb.Count} en .NET";
                for (var i = 0; i < la.Count; i++)
                {
                    var d = PrimeraDiferencia(la[i], lb[i], $"{ruta}[{i}]");
                    if (d is not null) return d;
                }
                return null;
            case (JsonValue va, JsonValue vb):
                var ea = va.GetValue<JsonElement>();
                var eb = vb.GetValue<JsonElement>();
                if (ea.ValueKind == JsonValueKind.Number && eb.ValueKind == JsonValueKind.Number)
                {
                    return Math.Abs(ea.GetDouble() - eb.GetDouble()) <= 1e-9 * Math.Max(1, Math.Abs(ea.GetDouble()))
                        ? null : $"{ruta}: {ea} vs {eb}";
                }
                return ea.ToString() == eb.ToString() && ea.ValueKind == eb.ValueKind ? null : $"{ruta}: {ea} vs {eb}";
            default:
                return $"{ruta}: tipos distintos ({a.GetType().Name} vs {b.GetType().Name})";
        }
    }

    private static void IgualQuePython(JsonElement esperado, object obtenido, string que)
    {
        var diferencia = PrimeraDiferencia(
            JsonNode.Parse(esperado.GetRawText()), JsonNode.Parse(JsonSerializer.Serialize(obtenido)), que);
        Assert.True(diferencia is null, diferencia);
    }

    // ------------------------------------------------------------------
    // Del resultado de las consultas al cubo
    // ------------------------------------------------------------------

    private static List<Dictionary<string, object?>> Filas(JsonElement lista)
        => lista.EnumerateArray().Select(f => f.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.ValueKind switch
            {
                JsonValueKind.String => (object?)p.Value.GetString(),
                JsonValueKind.Number => p.Value.GetInt64(),
                _ => null,
            })).ToList();

    [Fact]
    public void Agrupar_reparte_y_ordena_las_filas_como_extraer_rango()
    {
        var agrupar = Fixture.Value.GetProperty("agrupar");
        var filas = agrupar.GetProperty("filas").EnumerateArray().ToArray();
        var docs = FuenteBigQuery.Agrupar(Filas(filas[0]), Filas(filas[1]), Filas(filas[2]), S(agrupar.GetProperty("extraido")));

        var esperado = agrupar.GetProperty("docs");
        Assert.Equal(esperado.EnumerateObject().Count(), docs.Count);
        foreach (var dia in esperado.EnumerateObject())
        {
            var d = docs[dia.Name];
            var obtenido = new
            {
                dia = d.Dia,
                @base = d.Base.Select(r => new object[] { r.B, r.Dir, r.S, r.U, r.T, r.A, r.N2, r.N3, r.Cruzado, r.Ll, r.Sol, r.Nosol }),
                internet = d.Internet.Select(r => new object[]
                {
                    r.B, r.S, r.U, r.T, r.A, r.N4, r.N5, r.Res, r.Ticket, r.Escbo, r.Rell, r.Evento, r.Cierre, r.Etiquetas, r.Rubrica,
                }),
                llamadas = d.Llamadas.Select(r => new object[] { r.B, r.S, r.U, r.T, r.A, r.N2, r.N3, r.N4, r.Etiquetas, r.Id, r.IdExterno, r.Texto }),
            };
            IgualQuePython(dia.Value, obtenido, dia.Name);
        }
    }

    private static List<DocCrudo> DocsDeLaFixture()
    {
        var docs = new List<DocCrudo>();
        foreach (var d in Fixture.Value.GetProperty("docs").EnumerateArray())
        {
            docs.Add(new DocCrudo
            {
                Dia = S(d.GetProperty("dia")),
                Extraido = S(d.GetProperty("extraido")),
                Base = d.GetProperty("base").EnumerateArray().Select(r =>
                {
                    var f = r.EnumerateArray().ToArray();
                    return new FilaBase(S(f[0]), S(f[1]), S(f[2]), S(f[3]), S(f[4]), S(f[5]), S(f[6]), S(f[7]),
                                        I(f[8]), I(f[9]), I(f[10]), I(f[11]));
                }).ToList(),
                Internet = d.GetProperty("internet").EnumerateArray().Select(r =>
                {
                    var f = r.EnumerateArray().ToArray();
                    return new FilaInternet(S(f[0]), S(f[1]), S(f[2]), S(f[3]), S(f[4]), S(f[5]), S(f[6]),
                                            I(f[7]), I(f[8]), I(f[9]), I(f[10]), S(f[11]), S(f[12]),
                                            Ls(f[13]), f[14].EnumerateArray().Select(I).ToList());
                }).ToList(),
                Llamadas = d.GetProperty("llamadas").EnumerateArray().Select(r =>
                {
                    var f = r.EnumerateArray().ToArray();
                    return new FilaLlamada(S(f[0]), S(f[1]), S(f[2]), S(f[3]), S(f[4]), S(f[5]), S(f[6]), S(f[7]),
                                           Ls(f[8]), S(f[9]), S(f[10]), S(f[11]));
                }).ToList(),
            });
        }
        return docs;
    }

    private static Dictionary<string, int> Tabla(string clave)
        => Fixture.Value.GetProperty(clave).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetInt32());

    [Fact]
    public void Ensamblar_produce_exactamente_el_cubo_que_ensambla_python()
    {
        var cubo = Ensamblador.Ensamblar(
            DocsDeLaFixture(), Tabla("tabla"),
            Fixture.Value.GetProperty("dias_provisionales").GetInt32(), Tabla("conjuntos"));
        IgualQuePython(Fixture.Value.GetProperty("cubo"), SinExtensiones(cubo), "cubo");
    }

    /// <summary>
    /// El cubo sin lo que añadió esta web en la v3 (los cortes ver, dup y causas, el número de versión
    /// y el recuento de etiquetas clasificadas por palabras), que el Python de referencia no tiene: lo demás
    /// tiene que salir igual (la fixture no trae etiquetas que las palabras clave reconozcan).
    /// </summary>
    private static JsonNode SinExtensiones(Cubo cubo)
    {
        var nodo = JsonNode.Parse(JsonSerializer.Serialize(cubo))!;
        nodo["meta"]!["version_cubo"] = 2;
        nodo["meta"]!.AsObject().Remove("etiquetas_por_palabras");
        foreach (var (_, dia) in nodo["dias"]!.AsObject())
        {
            var o = dia!.AsObject();
            o.Remove("ver");
            o.Remove("dup");
            o.Remove("causas");
        }
        return nodo;
    }

    [Fact]
    public void El_cubo_de_disco_se_lee_y_se_vuelve_a_escribir_igual()
    {
        var original = Fixture.Value.GetProperty("cubo");
        var cubo = JsonSerializer.Deserialize<Cubo>(original.GetRawText())!;
        Assert.Equal(2, cubo.Meta.VersionCubo);
        Assert.NotEmpty(cubo.Dias.Values.SelectMany(d => d.Nos));
        IgualQuePython(original, cubo, "cubo");
    }

    // ------------------------------------------------------------------
    // Las vistas, con los mismos filtros que en Python
    // ------------------------------------------------------------------

    private static Cubo CuboDeLaFixture()
        => JsonSerializer.Deserialize<Cubo>(Fixture.Value.GetProperty("cubo").GetRawText())!;

    private static List<string>? Lista(JsonElement f, string clave)
        => f.GetProperty(clave).ValueKind == JsonValueKind.Null ? null : Ls(f.GetProperty(clave));

    private static string? Texto(JsonElement f, string clave)
        => f.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Entero(JsonElement f, string clave)
        => f.TryGetProperty(clave, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;

    private static Filtros FiltrosDe(JsonElement f) => new(
        f.GetProperty("dias").GetInt32(), Texto(f, "desde"), Texto(f, "hasta"),
        Lista(f, "marca"), Lista(f, "servicio"), Lista(f, "supervisor"), Lista(f, "tl"), Lista(f, "agente"));

    private static object Calcular(Cubo cubo, JsonElement caso)
    {
        var f = FiltrosDe(caso.GetProperty("filtros"));
        return S(caso.GetProperty("vista")) switch
        {
            "resumen" => Agregados.Resumen(cubo, f),
            "motivos" => Agregados.Motivos(cubo, f),
            "internet" => Agregados.Internet(cubo, f),
            "equipos" => Agregados.Equipos(cubo, S(caso.GetProperty("nivel")), f, Entero(caso, "suelo")),
            "opciones" => Agregados.Opciones(cubo, f),
            "muestras" => Agregados.MuestrasN3(cubo, S(caso.GetProperty("n3")), f),
            "muestras_impedimento" => Agregados.MuestrasImpedimento(cubo, caso.GetProperty("bit").GetInt32(), f),
            "csv" => Csv(cubo, f, Texto(caso, "n3"), Entero(caso, "bit")),
            "meta" => Agregados.MetaPublica(cubo),
            var otra => throw new InvalidOperationException("Vista sin probar: " + otra),
        };
    }

    private static object Csv(Cubo cubo, Filtros f, string? n3, int? bit)
    {
        var (nombre, texto) = Agregados.CsvLlamadas(cubo, f, n3, bit);
        return new Dictionary<string, string> { ["nombre"] = nombre, ["texto"] = texto };
    }

    public static IEnumerable<object[]> Casos()
    {
        var i = 0;
        foreach (var c in Fixture.Value.GetProperty("casos").EnumerateArray())
        {
            yield return new object[] { i++, S(c.GetProperty("vista")) };
        }
    }

    [Theory]
    [MemberData(nameof(Casos))]
    public void Cada_vista_devuelve_lo_mismo_que_python(int indice, string vista)
    {
        var caso = Fixture.Value.GetProperty("casos")[indice];
        var cubo = CuboDeLaFixture();
        var que = $"{vista}#{indice} {caso.GetProperty("filtros").GetRawText()}";

        if (caso.TryGetProperty("error", out var error))
        {
            var ex = Assert.Throws<FiltroInvalido>(() => Calcular(cubo, caso));
            Assert.Equal(S(error), ex.Message);
            return;
        }
        IgualQuePython(caso.GetProperty("resultado"), Calcular(cubo, caso), que);
    }

    [Fact]
    public void Las_muestras_solo_llevan_el_id_acordado()
    {
        var r = Agregados.MuestrasN3(CuboDeLaFixture(), "sinAccesoInternet", new Filtros(90));
        Assert.NotEmpty(r.Muestras);
        var json = JsonSerializer.Serialize(r);
        Assert.Contains("\"conversacion\"", json);
        Assert.DoesNotContain("conversacion_externa", json);
        Assert.DoesNotContain("ext-", json);
    }

    [Fact]
    public void Equipos_por_nivel_llevan_las_claves_de_su_nivel()
    {
        var cubo = CuboDeLaFixture();
        var f = new Filtros(90);

        var agentes = Agregados.Equipos(cubo, "agente", f, suelo: 1);
        Assert.All(agentes.Filas, x => { Assert.NotNull(x.Tl); Assert.NotNull(x.Supervisor); Assert.Null(x.Agentes); });

        var tls = Agregados.Equipos(cubo, "tl", f, suelo: 1);
        Assert.All(tls.Filas, x => { Assert.Null(x.Tl); Assert.NotNull(x.Supervisor); Assert.NotNull(x.Agentes); });

        var sups = Agregados.Equipos(cubo, "supervisor", f, suelo: 1);
        Assert.All(sups.Filas, x => { Assert.Null(x.Tl); Assert.Null(x.Supervisor); Assert.NotNull(x.Agentes); });

        var json = JsonSerializer.Serialize(sups.Filas[0]);
        Assert.DoesNotContain("\"tl\"", json);
        Assert.DoesNotContain("\"supervisor\"", json);
    }

    // ------------------------------------------------------------------
    // Claves de caché y ETag
    // ------------------------------------------------------------------

    public static IEnumerable<object[]> Claves()
    {
        var i = 0;
        foreach (var _ in Fixture.Value.GetProperty("claves").EnumerateArray()) yield return new object[] { i++ };
    }

    [Theory]
    [MemberData(nameof(Claves))]
    public void La_clave_del_etag_es_el_repr_de_python(int indice)
    {
        var c = Fixture.Value.GetProperty("claves")[indice];
        var partes = c.GetProperty("partes").EnumerateArray().Select(p => p.ValueKind switch
        {
            JsonValueKind.Null => (object?)null,
            JsonValueKind.String => p.GetString(),
            JsonValueKind.Number => p.GetInt32(),
            JsonValueKind.Array => Ls(p),
            _ => throw new InvalidOperationException(p.ValueKind.ToString()),
        });
        var repr = FormatoPython.ReprTupla(partes);
        Assert.Equal(S(c.GetProperty("repr")), repr);

        var hash = Convert.ToHexString(System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(repr)))
            .ToLowerInvariant()[..10];
        Assert.Equal(S(c.GetProperty("hash")), hash);
    }

    [Fact]
    public void Los_filtros_van_a_la_clave_ordenados_y_sin_repetidos()
    {
        Assert.Null(Agregados.ClaveFiltro(null));
        Assert.Null(Agregados.ClaveFiltro(new[] { "", null }));
        Assert.Equal(new[] { "MASMOVIL", "YOIGO" }, Agregados.ClaveFiltro(new[] { "YOIGO", "", "MASMOVIL", "YOIGO" }));
    }

    // ------------------------------------------------------------------
    // Consultas, ventana y renovación
    // ------------------------------------------------------------------

    [Fact]
    public void Las_consultas_llevan_las_fechas_validadas_el_universo_y_la_nomina()
    {
        var sql = FuenteBigQuery.SqlBase("2026-09-01", "2026-09-30");
        Assert.Contains("WHERE day BETWEEN '2026-09-01' AND '2026-09-30'", sql);
        Assert.Contains("brand IN ('YOIGO','MASMOVIL') AND serviceProviderLocation = 'JAZZBOG'", sql);
        Assert.Contains("brand IN ('JAZZTEL','ORANGE') AND serviceProviderLocation = 'JZZ_BOGOTA'", sql);
        Assert.Contains("LOWER(TRIM(Avaya)) AS clave", sql);
        Assert.Contains("CAST(n.clave IS NOT NULL AS INT64) AS cruzado", sql);
        Assert.Contains("GROUP BY 1, 2, 3, 4, 5, 6, 7, 8, 9, 10", sql);

        var internet = FuenteBigQuery.SqlInternet("2026-09-01", "2026-09-30");
        Assert.Contains("AS r8", internet);
        Assert.Contains("IFNULL(n.Agente, 'SIN ASIGNAR') AS agente", internet);
        Assert.Contains("LEFT JOIN nomina n ON LOWER(TRIM(u.primaryAgentId)) = n.clave AND n.rn = 1", internet);

        var nosol = FuenteBigQuery.SqlNoSolucionadas("2026-09-01", "2026-09-30");
        Assert.Contains("enh_Resolution_request = 2", nosol);
        Assert.Contains(@"r'[\r\n]+'", nosol);
        Assert.Contains("AS conversacion,", nosol);
        Assert.DoesNotContain("QUALIFY", nosol);

        Assert.ThrowsAny<FormatException>(() => FuenteBigQuery.SqlBase("2026-09-01'; DROP", "2026-09-30"));
    }

    [Fact]
    public void Los_tramos_cubren_la_ventana_hasta_ayer_y_la_firma_lleva_la_version()
    {
        var fuente = new FuenteBigQuery("DSN=NO;", 90, 45, 5, "no-existe.json",
            hoy: () => new DateOnly(2026, 9, 28));

        var (desde, hasta) = fuente.RangoObjetivo();
        Assert.Equal(new DateOnly(2026, 9, 27), hasta);
        Assert.Equal(new DateOnly(2026, 6, 30), desde);

        var tramos = fuente.Tramos();
        Assert.Equal(2, tramos.Count);
        Assert.Equal(("2026-06-30", "2026-08-13"), tramos[0]);
        Assert.Equal(("2026-08-14", "2026-09-27"), tramos[1]);
        Assert.Equal("ventana-unica-90-v3-ygmm-jzor", fuente.Firma());
    }

    [Fact]
    public void La_edad_del_cubo_sale_de_cuando_se_trajo()
    {
        var cubo = new Cubo { Meta = new MetaCubo { Generado = "2026-09-28T00:00:00" } };
        Assert.Equal(13.5, ServicioNoSolucion.EdadHoras(cubo, new DateTime(2026, 9, 28, 13, 30, 0))!.Value, 6);
        Assert.Null(ServicioNoSolucion.EdadHoras(new Cubo()));
        Assert.Null(ServicioNoSolucion.EdadHoras(new Cubo { Meta = new MetaCubo { Generado = "no es fecha" } }));
    }

    [Fact]
    public void El_float_de_python_se_escribe_con_decimal()
    {
        Assert.Equal("12.0", JsonSerializer.Serialize(new FormatoPython.FloatPython(12)));
        Assert.Equal("0.5", JsonSerializer.Serialize(new FormatoPython.FloatPython(0.5)));
    }
}
