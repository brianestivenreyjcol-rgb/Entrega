using System.Globalization;
using CDM_Auditorias_Calidad.Servicios.Comun;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>Un día tal y como sale de BigQuery, con nombres (antes de indexar).</summary>
public sealed class DocCrudo
{
    public string Dia { get; init; } = "";
    public string? Extraido { get; init; }
    public List<FilaBase> Base { get; init; } = new();
    public List<FilaInternet> Internet { get; init; } = new();
    public List<FilaLlamada> Llamadas { get; init; } = new();
}

/// <summary>
/// Día × marca × dirección × servicio × equipo × agente × N2 × N3:
/// <c>[b, dir, s, u, t, a, n2, n3, cruzado, ll, sol, nosol]</c>.
/// </summary>
public sealed record FilaBase(
    string B, string Dir, string S, string U, string T, string A, string N2, string N3,
    int Cruzado, int Ll, int Sol, int Nosol);

/// <summary>
/// Una llamada de sinAccesoInternet, con su equipo:
/// <c>[b, s, u, t, a, n4, n5, res, ticket, escbo, rell, evento, cierre, etiquetas, rubrica]</c>.
/// </summary>
/// <param name="Id">El <c>conversationId</c> (v3): cruza con las no solucionadas y sirve para contar repetidas.</param>
/// <param name="Llamada">El <c>externalConversationId</c> sin el sufijo <c>_N</c> (v3): la llamada física,
/// que puede tener varios tramos si se transfirió.</param>
public sealed record FilaInternet(
    string B, string S, string U, string T, string A, string N4, string N5,
    int Res, int Ticket, int Escbo, int Rell, string Evento, string Cierre,
    List<string> Etiquetas, List<int> Rubrica, string Id = "", string Llamada = "");

/// <summary>
/// Una llamada entrante no solucionada, con su id y su texto:
/// <c>[b, s, u, t, a, n2, n3, n4, etiquetas, id, id_externo, texto]</c>.
/// </summary>
public sealed record FilaLlamada(
    string B, string S, string U, string T, string A, string N2, string N3, string N4,
    List<string> Etiquetas, string Id, string IdExterno, string Texto);

/// <summary>
/// Ensambla el cubo de No solución (<see cref="Cubo"/>) a partir de los días
/// descargados: diccionarios de nombres, categorías de impedimento, umbral
/// de atención, días parciales, cuadrantes y el cuadre entre equipos y
/// tipologías. Hace lo mismo que backend/app/nosolucion/diario.py y debe
/// producir el mismo cubo, byte a byte.
/// </summary>
/// <remarks>
/// Ordena como <c>sorted</c> de Python: textos por punto de código, listas
/// de enteros elemento a elemento, y siempre de forma estable.
/// </remarks>
public static class Ensamblador
{
    public const string TablaGamma = "mo-vendor-management-reporting.JZZBOGOTA.ALL_dataorb_to_mo_insight_all_data_flattened_GAMMA";
    public const string TablaNomina = "mo-vendor-management-reporting.JZZBOGOTA.NominaBogota";

    /// <summary>
    /// Versión del formato del cubo. Va en la firma de la fuente
    /// (<see cref="FuenteBigQuery.Firma"/>): un cubo guardado con otro
    /// formato no se reutiliza, se reconstruye.
    /// </summary>
    /// <remarks>v3 (05-10-2026): añade <c>ver</c>, <c>dup</c> y <c>causas</c> (ver <see cref="Cubo"/>).</remarks>
    public const int VersionCubo = 3;

    /// <summary>Rúbrica, en el orden de las columnas de la consulta: siete ítems y dos circulares.</summary>
    public const int ItemsRubrica = 7;
    public const int ItemsCirculares = 2;

    public static readonly string[] RubNom =
    {
        "Saludo de apertura", "Descubrimiento del problema", "Sondeo de necesidades",
        "Reconocimiento emocional", "Manejo de objeciones", "Lenguaje claro",
        "Cierre y despedida", "Confirmó que quedaba resuelto", "Frases de resolución",
    };

    public static readonly IReadOnlyList<CategoriaImpedimento> ImpedCats = new[]
    {
        new CategoriaImpedimento { Bit = 1, Nom = "Visita o envío de técnico", Tipo = "proceso" },
        new CategoriaImpedimento { Bit = 2, Nom = "Avería masiva o red externa", Tipo = "proceso" },
        new CategoriaImpedimento { Bit = 4, Nom = "Escalado, nivel 2 o investigación", Tipo = "proceso" },
        new CategoriaImpedimento { Bit = 8, Nom = "Plazo o ticket pendiente", Tipo = "proceso" },
        new CategoriaImpedimento { Bit = 16, Nom = "Configuración o equipo por cambiar", Tipo = "proceso" },
        new CategoriaImpedimento { Bit = 32, Nom = "Atención o seguimiento del agente", Tipo = "atencion" },
        new CategoriaImpedimento { Bit = 64, Nom = "Depende del cliente", Tipo = "neutro" },
        new CategoriaImpedimento { Bit = 128, Nom = "Insatisfacción del cliente", Tipo = "neutro" },
        new CategoriaImpedimento { Bit = 256, Nom = "Problema declarado sin resolver", Tipo = "neutro" },
        new CategoriaImpedimento { Bit = 512, Nom = "Otro impedimento", Tipo = "neutro" },
    };

    public const int MaskProc = 31;
    public const int MaskAten = 32;
    public const int BitOtro = 512;

    /// <summary>Suelos de volumen del informe (a 90 días; los agregados los prorratean).</summary>
    public static readonly Dictionary<string, int> Suelo = new()
    {
        ["dia"] = 30, ["sup"] = 100, ["tl"] = 50, ["ag"] = 30, ["n2"] = 50, ["n3"] = 30,
    };

    /// <summary>Un día por debajo del 80 % de la mediana de su día de la semana es parcial.</summary>
    public const double UmbralParcial = 0.8;

    private static readonly string[] Dimensiones = { "b", "dir", "s", "u", "t", "a", "n2", "n3", "n4", "n5", "evento", "cierre" };

    /// <summary>
    /// Compara dos textos como Python: por punto de código, no por unidad
    /// UTF-16 (se nota con emoji y otros caracteres fuera del plano básico).
    /// </summary>
    public static int CompararTexto(string a, string b)
    {
        var ea = a.EnumerateRunes().GetEnumerator();
        var eb = b.EnumerateRunes().GetEnumerator();
        while (true)
        {
            var ha = ea.MoveNext();
            var hb = eb.MoveNext();
            if (!ha && !hb) return 0;
            if (!ha) return -1;
            if (!hb) return 1;
            var c = ea.Current.Value.CompareTo(eb.Current.Value);
            if (c != 0) return c;
        }
    }

    /// <summary>El orden de Python para textos, como comparador.</summary>
    public static readonly Comparer<string> TextoPython = Comparer<string>.Create(CompararTexto);

    /// <summary><c>sorted()</c> de una lista de listas de enteros: elemento a elemento.</summary>
    public static int CompararEnteros(int[] a, int[] b)
    {
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var c = a[i].CompareTo(b[i]);
            if (c != 0) return c;
        }
        return a.Length.CompareTo(b.Length);
    }

    private static readonly Comparer<int[]> EnterosPython = Comparer<int[]>.Create(CompararEnteros);

    /// <summary><c>statistics.median</c>.</summary>
    private static double Mediana(List<long> valores)
    {
        var ordenados = valores.OrderBy(v => v).ToList();
        var n = ordenados.Count;
        if (n == 0) throw new InvalidOperationException("mediana de una lista vacía");
        return n % 2 == 1 ? ordenados[n / 2] : (ordenados[n / 2 - 1] + ordenados[n / 2]) / 2.0;
    }

    private static int Fallos(IReadOnlyList<int> rubrica)
    {
        var n = 0;
        for (var i = 0; i < Math.Min(ItemsRubrica, rubrica.Count); i++) if (rubrica[i] == 0) n++;
        return n;
    }

    /// <summary>Ensambla el cubo a partir de los días descargados.</summary>
    /// <param name="docsEntrada">Los días descargados, con nombres.</param>
    /// <param name="tablaImped">Etiqueta de impedimento → bits de su categoría.</param>
    /// <param name="diasProvisionales">Últimos días que se marcan como provisionales.</param>
    /// <param name="conjuntosImped">Conjunto entero de etiquetas (unidas con <c>||</c>, ordenadas) → máscara exacta.</param>
    public static Cubo Ensamblar(
        IEnumerable<DocCrudo> docsEntrada, IReadOnlyDictionary<string, int> tablaImped,
        int diasProvisionales = 5, IReadOnlyDictionary<string, int>? conjuntosImped = null)
    {
        conjuntosImped ??= new Dictionary<string, int>();
        var docs = docsEntrada.OrderBy(d => d.Dia, TextoPython).ToList();
        var fechas = docs.Select(d => d.Dia).ToList();

        // --- diccionarios: lista ordenada de nombres por dimensión
        var vistos = Dimensiones.ToDictionary(k => k, _ => new HashSet<string>());
        foreach (var d in docs)
        {
            foreach (var r in d.Base)
            {
                vistos["b"].Add(r.B); vistos["dir"].Add(r.Dir); vistos["s"].Add(r.S); vistos["u"].Add(r.U);
                vistos["t"].Add(r.T); vistos["a"].Add(r.A); vistos["n2"].Add(r.N2); vistos["n3"].Add(r.N3);
            }
            foreach (var r in d.Internet)
            {
                vistos["b"].Add(r.B); vistos["s"].Add(r.S); vistos["u"].Add(r.U); vistos["t"].Add(r.T);
                vistos["a"].Add(r.A); vistos["n4"].Add(r.N4); vistos["n5"].Add(r.N5);
                vistos["evento"].Add(r.Evento); vistos["cierre"].Add(r.Cierre);
            }
            foreach (var r in d.Llamadas)
            {
                vistos["b"].Add(r.B); vistos["s"].Add(r.S); vistos["u"].Add(r.U); vistos["t"].Add(r.T);
                vistos["a"].Add(r.A); vistos["n2"].Add(r.N2); vistos["n3"].Add(r.N3);
            }
        }
        var dic = Dimensiones.ToDictionary(k => k, k => vistos[k].OrderBy(v => v, TextoPython).ToList());
        foreach (var k in new[] { "u", "t", "a" })
        {
            if (!dic[k].Contains("SIN ASIGNAR"))
            {
                dic[k].Add("SIN ASIGNAR");
                dic[k] = dic[k].OrderBy(v => v, TextoPython).ToList();
            }
        }
        var ix = dic.ToDictionary(par => par.Key, par => par.Value.Select((v, i) => (v, i)).ToDictionary(t => t.v, t => t.i));

        // --- agente → (supervisor, tl): el del día más reciente en la
        // consulta base; si solo sale en las otras dos, el de esas.
        var amapBase = new Dictionary<string, (string U, string T)>();
        var amapOtros = new Dictionary<string, (string U, string T)>();
        foreach (var d in docs)
        {
            foreach (var r in d.Base) amapBase[r.A] = (r.U, r.T);
            foreach (var r in d.Internet) amapOtros[r.A] = (r.U, r.T);
            foreach (var r in d.Llamadas) amapOtros[r.A] = (r.U, r.T);
        }
        var amapNom = new Dictionary<string, (string U, string T)>(amapOtros);
        foreach (var (a, ut) in amapBase) amapNom[a] = ut;
        var amap = dic["a"].Select(a => amapNom.TryGetValue(a, out var ut)
                ? new[] { ix["u"][ut.U], ix["t"][ut.T] }
                : new[] { ix["u"]["SIN ASIGNAR"], ix["t"]["SIN ASIGNAR"] })
            .ToList();

        // --- días parciales: < 80 % de la mediana de su mismo día de la semana
        var volumen = docs.ToDictionary(d => d.Dia, d => d.Base.Sum(r => (long)r.Ll));
        var porSemana = new Dictionary<int, List<long>>();
        foreach (var (iso, v) in volumen)
        {
            var semana = DiaDeLaSemana(iso);
            if (!porSemana.TryGetValue(semana, out var lista)) porSemana[semana] = lista = new List<long>();
            lista.Add(v);
        }
        var parciales = new List<string>();
        foreach (var (iso, v) in volumen)
        {
            var med = Mediana(porSemana[DiaDeLaSemana(iso)]);
            if (med != 0 && v < UmbralParcial * med) parciales.Add(iso);
        }

        // --- umbral de atención: mediana de ítems fallados en las llamadas solucionadas
        var sol = docs.SelectMany(d => d.Internet).Where(r => r.Res == 1).Select(r => Fallos(r.Rubrica)).OrderBy(x => x).ToList();
        var umbral = sol.Count > 0 ? sol[sol.Count / 2] : 0;
        const string abrupto = "abruptlyEnded";

        var nuevas = new HashSet<string>();
        var porPalabras = new HashSet<string>();

        int Mascara(IReadOnlyList<string> etiquetas)
        {
            // 1) conjunto de etiquetas ya visto: su máscara exacta
            var clave = string.Join("||", etiquetas.Distinct().OrderBy(e => e, TextoPython));
            if (conjuntosImped.TryGetValue(clave, out var exacta)) return exacta;
            // 2) conjunto nuevo: unión de lo que aporta cada etiqueta. Una etiqueta que la tabla no
            // conoce va por palabras clave (ClasificadorEtiquetas, v3; en el Python iba siempre a «Otro»).
            var m = 0;
            foreach (var e in etiquetas)
            {
                if (tablaImped.TryGetValue(e, out var bits)) m |= bits;
                else
                {
                    nuevas.Add(e);
                    var bit = ClasificadorEtiquetas.Bit(e);
                    if (bit != BitOtro) porPalabras.Add(e);
                    m |= bit;
                }
            }
            return m;
        }

        var dias = new Dictionary<string, DocDia>();
        long totLl = 0, totCruz = 0;
        var ctrl = new long[3];
        var porDir = new Dictionary<string, long[]>();
        var ordenDir = new List<string>();

        foreach (var d in docs)
        {
            var tot = new long[3];
            var @out = new long[3];
            var eq = new Dictionary<(int, int, int), int[]>();
            var tp = new Dictionary<(int, int, int, int, int), int[]>();

            foreach (var r in d.Base)
            {
                var v = new[] { r.Ll, r.Sol, r.Nosol };
                totLl += r.Ll;
                totCruz += r.Cruzado != 0 ? r.Ll : 0;
                if (!porDir.TryGetValue(r.Dir, out var pd))
                {
                    porDir[r.Dir] = pd = new long[3];
                    ordenDir.Add(r.Dir);
                }
                for (var j = 0; j < 3; j++)
                {
                    tot[j] += v[j];
                    ctrl[j] += v[j];
                    pd[j] += v[j];
                }
                if (r.Dir != "Inbound")
                {
                    for (var j = 0; j < 3; j++) @out[j] += v[j];
                    continue;
                }
                var (bi, si, ai) = (ix["b"][r.B], ix["s"][r.S], ix["a"][r.A]);
                var kx = (bi, si, ai);
                var ky = (bi, si, ai, ix["n2"][r.N2], ix["n3"][r.N3]);
                if (!eq.TryGetValue(kx, out var x)) eq[kx] = x = new int[3];
                if (!tp.TryGetValue(ky, out var y)) tp[ky] = y = new int[3];
                for (var j = 0; j < 3; j++)
                {
                    x[j] += v[j];
                    y[j] += v[j];
                }
            }

            var net = new Dictionary<(int, int, int, int, int, int, int, int, int, int, int), int[]>();
            var imp = new Dictionary<(int, int, int, int, int), int>();
            var rub = new Dictionary<(int, int, int, int, int, int), int>();
            var cuad = new Dictionary<(int, int, int, int, int, int), int>();
            var ver = new Dictionary<(int, int, int, int, int, int), int>();
            var causas = new Dictionary<string, int[]>();
            var dup = new Dictionary<int, (long Filas, HashSet<string> Conv, HashSet<string> Llam, long FilasNosol, HashSet<string> ConvNosol, HashSet<string> LlamNosol)>();
            foreach (var r in d.Internet)
            {
                var (bi, si, ai) = (ix["b"][r.B], ix["s"][r.S], ix["a"][r.A]);
                var k = (bi, si, ai, ix["n4"][r.N4], ix["n5"][r.N5], r.Ticket, r.Escbo, r.Rell,
                         ix["evento"][r.Evento], ix["cierre"][r.Cierre], r.Res);
                if (!net.TryGetValue(k, out var t)) net[k] = t = new int[2];
                t[0] += 1;
                t[1] += r.Res == 2 ? 1 : 0;

                var m = Mascara(r.Etiquetas);
                var puesto = false;
                foreach (var c in ImpedCats)
                {
                    if ((m & c.Bit) != 0)
                    {
                        var ki = (bi, si, ai, c.Bit, r.Res);
                        imp[ki] = imp.GetValueOrDefault(ki) + 1;
                        puesto = true;
                    }
                }
                if (!puesto)
                {
                    var ki = (bi, si, ai, 0, r.Res);
                    imp[ki] = imp.GetValueOrDefault(ki) + 1;
                }
                var maxItems = Math.Min(r.Rubrica.Count, ItemsRubrica + ItemsCirculares);
                for (var it = 0; it < maxItems; it++)
                {
                    var kr = (bi, si, ai, it, r.Rubrica[it], r.Res);
                    rub[kr] = rub.GetValueOrDefault(kr) + 1;
                }
                var fallos = Fallos(r.Rubrica);
                var aten = r.Cierre == abrupto || (m & MaskAten) != 0 || fallos > umbral;
                var kc = (bi, si, ai, aten ? 1 : 0, (m & MaskProc) != 0 ? 1 : 0, r.Res);
                cuad[kc] = cuad.GetValueOrDefault(kc) + 1;

                // v3: la combinación exacta de impedimentos y señales (para la causa única de cada
                // llamada), las señales de cada no solucionada por su id y el control de repetidas.
                var senales = (r.Cierre == abrupto ? CausaNoSolucion.SenalCierreAbrupto : 0)
                              | (fallos > umbral ? CausaNoSolucion.SenalRubrica : 0);
                var kv = (bi, si, ai, m, senales, r.Res);
                ver[kv] = ver.GetValueOrDefault(kv) + 1;
                if (r.Res == 2 && r.Id.Length > 0) causas[r.Id] = new[] { senales, fallos };
                if (!dup.TryGetValue(bi, out var dp))
                {
                    dp = (0, new HashSet<string>(), new HashSet<string>(), 0, new HashSet<string>(), new HashSet<string>());
                }
                dp.Filas++;
                dp.Conv.Add(r.Id);
                dp.Llam.Add(r.Llamada.Length > 0 ? r.Llamada : r.Id);
                if (r.Res == 2)
                {
                    dp.FilasNosol++;
                    dp.ConvNosol.Add(r.Id);
                    dp.LlamNosol.Add(r.Llamada.Length > 0 ? r.Llamada : r.Id);
                }
                dup[bi] = dp;
            }

            // Las no solucionadas, una por fila, con su máscara de
            // impedimentos (la misma tabla que el corte de internet).
            var nos = new List<FilaNos>(d.Llamadas.Count);
            foreach (var r in d.Llamadas)
            {
                var m = r.Etiquetas.Count > 0 ? Mascara(r.Etiquetas) : 0;
                nos.Add(new FilaNos(ix["b"][r.B], ix["s"][r.S], ix["a"][r.A], ix["n2"][r.N2], ix["n3"][r.N3], m,
                                    r.Id, r.IdExterno, r.N4, r.Texto));
            }

            dias[d.Dia] = new DocDia
            {
                D = d.Dia,
                Parcial = parciales.Contains(d.Dia),
                Extraido = d.Extraido,
                Tot = tot,
                Out = @out,
                Eq = eq.Select(p => new[] { p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Value[0], p.Value[1], p.Value[2] }).OrderBy(f => f, EnterosPython).ToList(),
                Tp = tp.Select(p => new[] { p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Key.Item4, p.Key.Item5, p.Value[0], p.Value[1], p.Value[2] }).OrderBy(f => f, EnterosPython).ToList(),
                Net = net.Select(p => new[]
                {
                    p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Key.Item4, p.Key.Item5, p.Key.Item6, p.Key.Item7,
                    p.Key.Item8, p.Key.Item9, p.Key.Item10, p.Key.Item11, p.Value[0], p.Value[1],
                }).OrderBy(f => f, EnterosPython).ToList(),
                Imp = imp.Select(p => new[] { p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Key.Item4, p.Key.Item5, p.Value }).OrderBy(f => f, EnterosPython).ToList(),
                Rub = rub.Select(p => new[] { p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Key.Item4, p.Key.Item5, p.Key.Item6, p.Value }).OrderBy(f => f, EnterosPython).ToList(),
                Cuad = cuad.Select(p => new[] { p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Key.Item4, p.Key.Item5, p.Key.Item6, p.Value }).OrderBy(f => f, EnterosPython).ToList(),
                Nos = nos.OrderBy(f => f.Id, TextoPython).ThenBy(f => f.IdExterno, TextoPython).ToList(),
                Ver = ver.Select(p => new[] { p.Key.Item1, p.Key.Item2, p.Key.Item3, p.Key.Item4, p.Key.Item5, p.Key.Item6, p.Value }).OrderBy(f => f, EnterosPython).ToList(),
                // Sin ids (datos de antes de v3) no se puede contar repetidas: nulo, y la vista no lo enseña.
                Dup = d.Internet.Any(r => r.Id.Length == 0) ? null : dup.OrderBy(p => p.Key).Select(p => new long[]
                {
                    p.Key, p.Value.Filas, p.Value.Conv.Count, p.Value.Llam.Count,
                    p.Value.FilasNosol, p.Value.ConvNosol.Count, p.Value.LlamNosol.Count,
                }).ToList(),
                Causas = causas,
            };
        }

        // --- cuadre interno: equipo y tipología salen de la misma consulta
        var sEq = new long[3];
        var sTp = new long[3];
        foreach (var dd in dias.Values)
        {
            foreach (var r in dd.Eq) for (var j = 0; j < 3; j++) sEq[j] += r[3 + j];
            foreach (var r in dd.Tp) for (var j = 0; j < 3; j++) sTp[j] += r[5 + j];
        }
        if (!sEq.SequenceEqual(sTp))
        {
            throw new InvalidOperationException(
                $"No cuadra: equipo=[{string.Join(", ", sEq)}] tipologia=[{string.Join(", ", sTp)}]");
        }

        var extraidos = docs.Where(d => !string.IsNullOrEmpty(d.Extraido)).Select(d => d.Extraido!).ToList();
        var meta = new MetaCubo
        {
            Generado = extraidos.Count > 0 ? extraidos.OrderByDescending(e => e, TextoPython).First() : null,
            Fuente = TablaGamma[(TablaGamma.IndexOf('.') + 1)..] + " + " + TablaNomina[(TablaNomina.LastIndexOf('.') + 1)..],
            Proyecto = TablaGamma[..TablaGamma.IndexOf('.')],
            Origen = "bigquery",
            VersionCubo = VersionCubo,
            DiaMin = fechas[0],
            DiaMax = fechas[^1],
            Ventana = fechas.Count,
            Dias = fechas,
            DiasParciales = parciales.OrderBy(p => p, TextoPython).ToList(),
            DiasProvisionales = diasProvisionales > 0 ? fechas.Skip(Math.Max(0, fechas.Count - diasProvisionales)).ToList() : new List<string>(),
            Cruce = totLl != 0 ? FormatoPython.Round(100.0 * totCruz / totLl, 4) : null,
            Ctrl = new Dictionary<string, long> { ["ll"] = ctrl[0], ["sol"] = ctrl[1], ["nosol"] = ctrl[2] },
            PorDireccion = ordenDir.ToDictionary(k => k, k => new Dictionary<string, long>
            {
                ["llamadas"] = porDir[k][0], ["sol"] = porDir[k][1], ["nosol"] = porDir[k][2],
            }),
            Suelo = new Dictionary<string, int>(Suelo),
            Dic = dic,
            Amap = amap,
            RubNom = RubNom.ToList(),
            ImpedCats = ImpedCats.Select(c => new CategoriaImpedimento { Bit = c.Bit, Nom = c.Nom, Tipo = c.Tipo }).ToList(),
            UmbralAtencion = umbral,
            UmbralBase = sol.Count,
            EtiquetasNuevas = nuevas.Count,
            EtiquetasPorPalabras = porPalabras.Count,
            Cuadre = sEq.ToList(),
        };

        return new Cubo { Meta = meta, Dias = dias };
    }

    private static int DiaDeLaSemana(string iso)
    {
        var fecha = DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return ((int)fecha.DayOfWeek + 6) % 7;   // weekday(): lunes = 0
    }
}
