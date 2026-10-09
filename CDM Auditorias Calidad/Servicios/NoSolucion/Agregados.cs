using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using CDM_Auditorias_Calidad.Servicios.Comun;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>El filtro pedido no existe en los datos. Se traduce a 400.</summary>
public sealed class FiltroInvalido : Exception
{
    public FiltroInvalido(string mensaje) : base(mensaje) { }
}

/// <summary>
/// Los filtros de todas las vistas. La ventana va por <c>Desde</c>/<c>Hasta</c>
/// o, si no vienen, por los últimos <c>Dias</c>. Las listas vacías o nulas son
/// «todos».
/// </summary>
public sealed record Filtros(
    int Dias,
    string? Desde = null,
    string? Hasta = null,
    IReadOnlyList<string>? Marca = null,
    IReadOnlyList<string>? Servicio = null,
    IReadOnlyList<string>? Supervisor = null,
    IReadOnlyList<string>? Tl = null,
    IReadOnlyList<string>? Agente = null);

/// <summary>
/// Cálculos de las vistas de No solución sobre el cubo. Hace lo mismo que
/// backend/app/nosolucion/agregados.py y debe devolver lo mismo.
/// </summary>
/// <remarks>
/// <para>
/// Funciones puras: reciben el cubo y los filtros y devuelven lo que pinta la
/// pantalla, con los nombres ya resueltos. % de no solución = no
/// solucionadas ÷ (solucionadas + no solucionadas), sobre llamadas entrantes
/// encuestadas.
/// </para>
/// <para>
/// Los porcentajes se redondean con <see cref="FormatoPython.Round"/>
/// (half-even, como Python) y los órdenes son estables, como
/// <c>list.sort</c>.
/// </para>
/// </remarks>
public static class Agregados
{
    private static readonly Dictionary<string, string> SueloClave = new()
    {
        ["supervisor"] = "sup", ["tl"] = "tl", ["agente"] = "ag",
    };

    /// <summary>Nombres de los días, empezando en lunes como <c>weekday()</c>.</summary>
    private static readonly string[] DiasSemana =
        { "lunes", "martes", "miércoles", "jueves", "viernes", "sábado", "domingo" };

    /// <summary>Encuestas mínimas para dibujar el % diario de una marca.</summary>
    public const int MinEncPunto = 8;

    /// <summary>Llamadas de ejemplo por tipología o por impedimento.</summary>
    public const int Muestras = 10;

    /// <summary>La tipología (N3) de la pestaña «Sin acceso a internet».</summary>
    public const string TipologiaInternet = "sinAccesoInternet";

    /// <summary><c>round(100.0 * a / b, 4)</c>, o nulo si <c>b</c> es 0.</summary>
    public static double? Pct(long a, long b) => b != 0 ? FormatoPython.Round(100.0 * a / b, 4) : null;

    /// <summary>Redondeo de 0,5 hacia arriba (<c>Math.round</c> de JavaScript).</summary>
    public static int Redondear(double x) => (int)(x + 0.5);

    private static double? Resta(double? a, double? b)
        => a is not null && b is not null ? FormatoPython.Round(a.Value - b.Value, 4) : null;

    private static int DiaDeLaSemana(string iso)
    {
        var fecha = DateTime.ParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return ((int)fecha.DayOfWeek + 6) % 7;
    }

    /// <summary>Un filtro como lista de valores, sin vacíos.</summary>
    public static List<string> ListaFiltro(IEnumerable<string?>? valores)
        => valores?.Where(v => !string.IsNullOrEmpty(v)).Select(v => v!).ToList() ?? new List<string>();

    /// <summary>
    /// El filtro como va en la llave de caché y en el ETag: la lista ordenada
    /// y sin repetidos, o nulo si no hay filtro.
    /// </summary>
    public static IReadOnlyList<string>? ClaveFiltro(IEnumerable<string?>? valores)
    {
        var l = ListaFiltro(valores);
        return l.Count > 0 ? l.Distinct().OrderBy(v => v, Ensamblador.TextoPython).ToList() : null;
    }

    // =====================================================================
    // SELECCIÓN: los días de la ventana, el periodo anterior y el filtro
    // =====================================================================
    private sealed class Seleccion
    {
        public MetaCubo Meta { get; }
        public List<string> Fechas { get; }
        public List<DocDia> Docs { get; }
        public List<DocDia> DocsPrev { get; }

        /// <summary>Índices elegidos de marca, servicio y agente; nulo = todos.</summary>
        public HashSet<int>? B { get; }
        public HashSet<int>? S { get; }
        public HashSet<int>? A { get; }

        public Seleccion(Cubo cubo, Filtros f, bool conPersonas = true)
        {
            Meta = cubo.Meta;
            var fechas = Meta.Dias;
            int ini, n;

            if (!string.IsNullOrEmpty(f.Desde) || !string.IsNullOrEmpty(f.Hasta))
            {
                var d0 = string.IsNullOrEmpty(f.Desde) ? fechas[0] : f.Desde;
                var d1 = string.IsNullOrEmpty(f.Hasta) ? fechas[^1] : f.Hasta;
                if (string.CompareOrdinal(d0, d1) > 0)
                {
                    throw new FiltroInvalido($"La fecha inicial ({d0}) es posterior a la final ({d1}).");
                }
                var elegidas = fechas
                    .Where(x => string.CompareOrdinal(d0, x) <= 0 && string.CompareOrdinal(x, d1) <= 0)
                    .ToList();
                if (elegidas.Count == 0)
                {
                    throw new FiltroInvalido(
                        $"No hay datos entre {d0} y {d1}. Hay del {fechas[0]} al {fechas[^1]}.");
                }
                Fechas = elegidas;
                ini = fechas.IndexOf(elegidas[0]);
                n = elegidas.Count;
            }
            else
            {
                n = Math.Max(1, Math.Min(f.Dias, fechas.Count));
                Fechas = fechas.GetRange(fechas.Count - n, n);
                ini = fechas.Count - n;
            }

            // Periodo anterior de igual largo; vacío si no cabe entero.
            var previas = ini - n >= 0 ? fechas.GetRange(ini - n, n) : new List<string>();

            B = Indices(Meta.D("b"), f.Marca, "marca desconocida", listar: true);
            S = Indices(Meta.D("s"), f.Servicio, "servicio desconocida", listar: true);

            // Supervisor y TL se resuelven al conjunto de agentes que tienen debajo.
            if (conPersonas)
            {
                var u = Indices(Meta.D("u"), f.Supervisor, "supervisor desconocido");
                var t = Indices(Meta.D("t"), f.Tl, "team leader desconocido");
                var a = Indices(Meta.D("a"), f.Agente, "agente desconocido");
                if (u is not null || t is not null || a is not null)
                {
                    A = new HashSet<int>();
                    for (var i = 0; i < Meta.Amap.Count; i++)
                    {
                        var (ui, ti) = (Meta.Amap[i][0], Meta.Amap[i][1]);
                        if ((u is null || u.Contains(ui)) && (t is null || t.Contains(ti)) && (a is null || a.Contains(i)))
                        {
                            A.Add(i);
                        }
                    }
                }
            }

            Docs = Fechas.Where(cubo.Dias.ContainsKey).Select(x => cubo.Dias[x]).ToList();
            DocsPrev = previas.Where(cubo.Dias.ContainsKey).Select(x => cubo.Dias[x]).ToList();
        }

        private static HashSet<int>? Indices(List<string> lista, IReadOnlyList<string>? valores, string mensaje, bool listar = false)
        {
            var pedidos = ListaFiltro(valores);
            if (pedidos.Count == 0) return null;
            var salida = new HashSet<int>();
            foreach (var v in pedidos)
            {
                var i = lista.IndexOf(v);
                if (i < 0)
                {
                    throw new FiltroInvalido(listar
                        ? $"{mensaje}: {FormatoPython.Repr(v)} (valores: {string.Join(", ", lista)})"
                        : $"{mensaje}: {FormatoPython.Repr(v)}");
                }
                salida.Add(i);
            }
            return salida;
        }

        public bool HayFiltro => B is not null || S is not null || A is not null;

        private bool Pasa(int b, int s, int a)
            => (B is null || B.Contains(b)) && (S is null || S.Contains(s)) && (A is null || A.Contains(a));

        /// <summary>Las filas de un corte que pasan el filtro.</summary>
        public IEnumerable<int[]> Filas(string campo, IEnumerable<DocDia>? docs = null)
        {
            foreach (var doc in docs ?? Docs)
            {
                foreach (var r in doc.Corte(campo))
                {
                    if (Pasa(r[0], r[1], r[2])) yield return r;
                }
            }
        }

        /// <summary>Las no solucionadas de un día que pasan el filtro.</summary>
        public IEnumerable<FilaNos> Nos(DocDia doc) => doc.Nos.Where(r => Pasa(r.B, r.S, r.A));

        /// <summary>Suelos del informe (pensados para ~90 días) escalados a la ventana.</summary>
        public int Prorratear(int @base, int minimo)
            => Math.Max(minimo, Redondear(@base * Fechas.Count / 90.0));
    }

    private static Totales CalcularTotales(IEnumerable<int[]> filas)
    {
        long ll = 0, sol = 0, nosol = 0;
        foreach (var r in filas)
        {
            ll += r[3];
            sol += r[4];
            nosol += r[5];
        }
        var enc = sol + nosol;
        return new Totales(ll, sol, nosol, enc, Pct(nosol, enc), Pct(enc, ll));
    }

    // =====================================================================
    // RESUMEN
    // =====================================================================
    public static RespuestaResumen Resumen(Cubo cubo, Filtros f)
    {
        var sel = new Seleccion(cubo, f);
        var meta = sel.Meta;
        var marcas = meta.D("b");
        var t = CalcularTotales(sel.Filas("eq"));
        var previo = sel.DocsPrev.Count == sel.Fechas.Count && sel.DocsPrev.Count > 0
            ? CalcularTotales(sel.Filas("eq", sel.DocsPrev))
            : null;
        var delta = previo is not null ? Resta(t.Pct, previo.Pct) : null;

        var serie = new List<DiaSerie>(sel.Docs.Count);
        foreach (var doc in sel.Docs)
        {
            var o = new DiaSerie
            {
                Dia = doc.D,
                Parcial = doc.Parcial,
                Marcas = marcas.ToDictionary(m => m, _ => new MarcaDia()),
            };
            foreach (var r in sel.Filas("eq", new[] { doc }))
            {
                o.Llamadas += r[3];
                o.Sol += r[4];
                o.Nosol += r[5];
                var mm = o.Marcas[marcas[r[0]]];
                mm.Encuestadas += r[4] + r[5];
                mm.Nosol += r[5];
            }
            o.Encuestadas = o.Sol + o.Nosol;
            o.Pct = Pct(o.Nosol, o.Encuestadas);
            foreach (var mm in o.Marcas.Values)
            {
                mm.Pct = mm.Encuestadas >= MinEncPunto ? Pct(mm.Nosol, mm.Encuestadas) : null;
            }
            serie.Add(o);
        }

        var sueloDia = Math.Max(10, meta.Suelo?.GetValueOrDefault("dia", 30) ?? 30);
        var peores = serie
            .Where(x => !x.Parcial && x.Encuestadas >= sueloDia && x.Pct is not null)
            .OrderByDescending(x => x.Pct!.Value)
            .Take(6)
            .Select(x => new PeorDia(
                x.Dia, DiasSemana[DiaDeLaSemana(x.Dia)], x.Encuestadas, x.Nosol, x.Pct,
                t.Pct is not null ? FormatoPython.Round(x.Pct!.Value - t.Pct.Value, 4) : null))
            .ToList();

        var saliente = meta.PorDireccion?.GetValueOrDefault("Outbound") ?? new Dictionary<string, long>();
        var provisionales = new HashSet<string>(meta.DiasProvisionales ?? new List<string>());
        foreach (var x in serie) x.Provisional = provisionales.Contains(x.Dia);

        return new RespuestaResumen(
            new VentanaResumen(sel.Fechas[0], sel.Fechas[^1], sel.Fechas.Count),
            serie.Where(x => x.Provisional).Select(x => x.Dia).ToList(),
            t,
            previo,
            delta,
            meta.Cruce,
            marcas.Where((_, i) => sel.B is null || sel.B.Contains(i)).ToList(),
            serie,
            serie.Where(x => x.Parcial).Select(x => x.Dia).ToList(),
            peores,
            sueloDia,
            new Saliente(
                saliente.GetValueOrDefault("llamadas", 0),
                saliente.GetValueOrDefault("sol", 0) + saliente.GetValueOrDefault("nosol", 0)));
    }

    // =====================================================================
    // EQUIPOS
    // =====================================================================
    private sealed class AcumuladoEquipo
    {
        public int K;
        public long Ll, Sol, Nosol;
        public readonly SortedSet<int> Ags = new();
    }

    /// <summary>
    /// Ranking de supervisores, TL o agentes por % de no solución, con el
    /// suelo de encuestas prorrateado a la ventana (o el que se pida).
    /// </summary>
    public static RespuestaEquipos Equipos(Cubo cubo, string nivel, Filtros f, int? suelo = null)
    {
        if (!SueloClave.TryGetValue(nivel, out var claveSuelo))
        {
            throw new FiltroInvalido($"nivel desconocido: {FormatoPython.Repr(nivel)}");
        }

        var sel = new Seleccion(cubo, f);
        var meta = sel.Meta;
        var amap = meta.Amap;
        var media = CalcularTotales(sel.Filas("eq")).Pct;

        var agg = new Dictionary<int, AcumuladoEquipo>();
        var orden = new List<int>();
        foreach (var r in sel.Filas("eq"))
        {
            var a = r[2];
            var k = nivel == "agente" ? a : amap[a][nivel == "supervisor" ? 0 : 1];
            if (!agg.TryGetValue(k, out var o))
            {
                o = new AcumuladoEquipo { K = k };
                agg[k] = o;
                orden.Add(k);
            }
            o.Ll += r[3];
            o.Sol += r[4];
            o.Nosol += r[5];
            o.Ags.Add(a);
        }

        var @base = meta.Suelo?.GetValueOrDefault(claveSuelo, 30) ?? 30;
        var minimo = suelo is > 0 ? suelo.Value : sel.Prorratear(@base, 5);
        var nombres = nivel == "agente" ? meta.D("a") : nivel == "tl" ? meta.D("t") : meta.D("u");

        var filas = new List<FilaEquipo>();
        foreach (var k in orden)
        {
            var o = agg[k];
            var enc = o.Sol + o.Nosol;
            if (enc < minimo) continue;

            var p = Pct(o.Nosol, enc);
            var fila = new FilaEquipo
            {
                Nombre = nombres[o.K],
                Llamadas = o.Ll,
                Encuestadas = enc,
                Nosol = o.Nosol,
                Pct = p,
                Cobertura = Pct(enc, o.Ll),
                Desvio = Resta(p, media),
            };
            if (nivel == "agente")
            {
                fila.Tl = meta.D("t")[amap[o.K][1]];
                fila.Supervisor = meta.D("u")[amap[o.K][0]];
            }
            else
            {
                fila.Agentes = o.Ags.Count;
                if (nivel == "tl") fila.Supervisor = meta.D("u")[amap[o.Ags.Min][0]];
            }
            filas.Add(fila);
        }
        filas = filas.OrderByDescending(x => x.Pct ?? 0).ToList();

        var cobs = filas.Where(x => x.Cobertura is not null).Select(x => x.Cobertura!.Value).OrderBy(c => c).ToList();
        RangoCobertura? rangoCob = null;
        if (cobs.Count > 3)
        {
            rangoCob = new RangoCobertura(cobs[(int)(cobs.Count * 0.1)], cobs[(int)(cobs.Count * 0.9)]);
        }

        return new RespuestaEquipos(nivel, media, minimo, @base, agg.Count, agg.Count - filas.Count, filas, rangoCob);
    }

    // =====================================================================
    // MOTIVOS
    // =====================================================================
    /// <summary>Suma llamadas, sol y nosol de <c>tp</c> por la columna <c>iClave</c> (3 = N2, 4 = N3).</summary>
    private static (List<int> Orden, Dictionary<int, long[]> Valores) PorClave(Seleccion sel, int iClave)
    {
        var orden = new List<int>();
        var m = new Dictionary<int, long[]>();
        foreach (var r in sel.Filas("tp"))
        {
            if (!m.TryGetValue(r[iClave], out var t))
            {
                t = new long[3];
                m[r[iClave]] = t;
                orden.Add(r[iClave]);
            }
            t[0] += r[5];
            t[1] += r[6];
            t[2] += r[7];
        }
        return (orden, m);
    }

    /// <summary>Áreas N2 y las 25 tipologías N3 que más no solucionadas acumulan.</summary>
    public static RespuestaMotivos Motivos(Cubo cubo, Filtros f)
    {
        var sel = new Seleccion(cubo, f);
        var meta = sel.Meta;
        var media = CalcularTotales(sel.Filas("eq")).Pct;
        var suelo = meta.Suelo ?? new Dictionary<string, int>();
        var s2 = sel.Prorratear(suelo.GetValueOrDefault("n2", 50), 10);
        var s3 = sel.Prorratear(suelo.GetValueOrDefault("n3", 30), 8);

        FilaMotivo Fila(string nombre, long[] v)
        {
            var enc = v[1] + v[2];
            var p = Pct(v[2], enc);
            return new FilaMotivo
            {
                Nombre = nombre, Llamadas = v[0], Encuestadas = enc, Nosol = v[2],
                Pct = p, Cobertura = Pct(enc, v[0]), Desvio = Resta(p, media),
            };
        }

        var (ordenN2, porN2) = PorClave(sel, 3);
        var n2 = ordenN2
            .Where(k => porN2[k][1] + porN2[k][2] >= s2)
            .Select(k => Fila(meta.D("n2")[k], porN2[k]))
            .OrderByDescending(x => x.Nosol)
            .ToList();

        var (ordenN3, porN3) = PorClave(sel, 4);
        var n3 = ordenN3
            .Where(k => porN3[k][1] + porN3[k][2] >= s3)
            .Select(k => Fila(meta.D("n3")[k], porN3[k]))
            .OrderByDescending(x => x.Nosol)
            .Take(25)
            .ToList();

        var peso = n3.Sum(x => x.Nosol);
        if (peso == 0) peso = 1;
        foreach (var x in n3) x.Peso = FormatoPython.Round(100.0 * x.Nosol / peso, 4);

        return new RespuestaMotivos(media, s2, s3, n2, n3);
    }

    // =====================================================================
    // LAS NO SOLUCIONADAS, UNA A UNA: ejemplos y CSV
    // =====================================================================
    private static int? IndiceN3(MetaCubo meta, string? n3)
    {
        if (n3 is null) return null;
        var i = meta.D("n3").IndexOf(n3);
        if (i < 0) throw new FiltroInvalido($"tipología desconocida: {FormatoPython.Repr(n3)}");
        return i;
    }

    private static int? ComprobarBit(MetaCubo meta, int? bit)
    {
        if (bit is null) return null;
        var cats = (meta.ImpedCats ?? new List<CategoriaImpedimento>()).Select(c => c.Bit).ToHashSet();
        if (bit != 0 && !cats.Contains(bit.Value))
        {
            throw new FiltroInvalido($"impedimento desconocido: {bit.Value.ToString(CultureInfo.InvariantCulture)}");
        }
        return bit;
    }

    /// <summary>Las no solucionadas que pasan los filtros, con su día, en el orden del cubo.</summary>
    private static List<(string Dia, FilaNos Fila)> Llamadas(Seleccion sel, string? n3 = null, int? bit = null)
    {
        var n3i = IndiceN3(sel.Meta, n3);
        var b = ComprobarBit(sel.Meta, bit);
        var salida = new List<(string, FilaNos)>();
        foreach (var doc in sel.Docs)
        {
            foreach (var r in sel.Nos(doc))
            {
                if (n3i is not null && r.N3 != n3i) continue;
                if (b is not null && (b == 0 ? r.Mascara != 0 : (r.Mascara & b.Value) == 0)) continue;
                salida.Add((doc.D, r));
            }
        }
        return salida;
    }

    /// <summary>Las más recientes primero; dentro del día, por (id, id_externo).</summary>
    private static List<(string Dia, FilaNos Fila)> Ordenar(List<(string Dia, FilaNos Fila)> llamadas)
        => llamadas
            .OrderByDescending(x => x.Dia, StringComparer.Ordinal)
            .ThenBy(x => x.Fila.Id, Ensamblador.TextoPython)
            .ThenBy(x => x.Fila.IdExterno, Ensamblador.TextoPython)
            .ToList();

    private static List<string> NombresImpedimento(MetaCubo meta, int mascara)
        => (meta.ImpedCats ?? new List<CategoriaImpedimento>()).Where(c => (mascara & c.Bit) != 0).Select(c => c.Nom).ToList();

    /// <summary>Separador, fin de línea y marca BOM del CSV, los del informe HTML.</summary>
    private const string CsvSep = ";";
    private const string CsvEol = "\r\n";
    private const string CsvBom = "﻿";

    /// <summary>Columnas del CSV: las del informe y, al final, la avería y los impedimentos.</summary>
    public static readonly string[] CabLlamadas =
    {
        "id_llamada", "fecha", "marca", "direccion", "sector", "supervisor", "team_leader", "agente",
        "area_n2", "motivo_n3", "resultado", "resumen", "averia_n4", "impedimentos",
    };

    /// <summary>Entrecomilla un valor si lleva comillas, separador, coma o salto de línea.</summary>
    private static string CsvEscapa(string? v)
    {
        var s = v ?? "";
        return s.IndexOfAny(new[] { '"', ';', ',', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }

    /// <summary>
    /// (nombre de fichero, texto) del CSV con todas las no solucionadas de
    /// los filtros, opcionalmente de una tipología o de un impedimento.
    /// </summary>
    public static (string Nombre, string Texto) CsvLlamadas(Cubo cubo, Filtros f, string? n3 = null, int? bit = null)
    {
        var sel = new Seleccion(cubo, f);
        var meta = sel.Meta;
        var dic = meta;
        var sb = new StringBuilder(CsvBom);
        sb.Append(string.Join(CsvSep, CabLlamadas.Select(CsvEscapa))).Append(CsvEol);
        foreach (var (dia, r) in Ordenar(Llamadas(sel, n3, bit)))
        {
            var (u, t) = (meta.Amap[r.A][0], meta.Amap[r.A][1]);
            var valores = new[]
            {
                r.Id, dia, dic.D("b")[r.B], "Inbound", dic.D("s")[r.S],
                dic.D("u")[u], dic.D("t")[t], dic.D("a")[r.A], dic.D("n2")[r.N2], dic.D("n3")[r.N3],
                "NO SOLUCIONADA", r.Texto, r.N4, string.Join(" / ", NombresImpedimento(meta, r.Mascara)),
            };
            sb.Append(string.Join(CsvSep, valores.Select(CsvEscapa))).Append(CsvEol);
        }
        var acotado = sel.HayFiltro || n3 is not null || bit is not null;
        var nombre = $"nosolucion_llamadas_{sel.Fechas[0].Replace("-", "")}-{sel.Fechas[^1].Replace("-", "")}{(acotado ? "_filtrado" : "")}.csv";
        return (nombre, sb.ToString());
    }

    private static Muestra ComoMuestra(MetaCubo meta, string dia, FilaNos r)
        => new(dia, meta.D("a")[r.A], meta.D("b")[r.B], meta.D("s")[r.S], r.Texto, r.Id,
               string.IsNullOrEmpty(r.N4) ? null : r.N4, NombresImpedimento(meta, r.Mascara));

    private static string HuellaSha1(string texto)
        => Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(texto))).ToLowerInvariant();

    /// <summary>
    /// Diez ejemplos reproducibles: los de menor SHA-1 del id (o del texto si
    /// no hay id), ordenados después por (día, id). Salen los mismos que en
    /// Python.
    /// </summary>
    private static List<(string Dia, FilaNos Fila)> Elegir(List<(string Dia, FilaNos Fila)> llamadas)
        => llamadas
            .OrderBy(x => HuellaSha1(string.IsNullOrEmpty(x.Fila.Id) ? x.Fila.Texto : x.Fila.Id), StringComparer.Ordinal)
            .Take(Muestras)
            .OrderBy(x => x.Dia, StringComparer.Ordinal)
            .ThenBy(x => x.Fila.Id, Ensamblador.TextoPython)
            .ToList();

    /// <summary>Diez llamadas no solucionadas de ejemplo de una tipología N3, con los filtros.</summary>
    public static RespuestaMuestras MuestrasN3(Cubo cubo, string n3, Filtros f)
    {
        var sel = new Seleccion(cubo, f);
        var todas = Llamadas(sel, n3: n3);
        return new RespuestaMuestras(n3, todas.Count, Elegir(todas).Select(x => ComoMuestra(sel.Meta, x.Dia, x.Fila)).ToList());
    }

    /// <summary>
    /// Diez llamadas de ejemplo de una categoría de impedimento (0 = sin ninguno), con los filtros y,
    /// si se da, solo de una tipología N3 (la pestaña de internet pasa <see cref="TipologiaInternet"/>
    /// para que el total cuadre con su barra).
    /// </summary>
    public static RespuestaMuestrasImpedimento MuestrasImpedimento(Cubo cubo, int bit, Filtros f, string? n3 = null)
    {
        var sel = new Seleccion(cubo, f);
        ComprobarBit(sel.Meta, bit);
        var cats = (sel.Meta.ImpedCats ?? new List<CategoriaImpedimento>()).ToDictionary(c => c.Bit);
        var nombre = bit != 0 ? cats[bit].Nom : "Sin ningún impedimento";
        var todas = Llamadas(sel, n3, bit);
        return new RespuestaMuestrasImpedimento(bit, nombre, todas.Count,
            Elegir(todas).Select(x => ConCausa(sel, x.Dia, x.Fila, ComoMuestra(sel.Meta, x.Dia, x.Fila))).ToList());
    }

    /// <summary>La muestra con el nombre de su causa, si es de sinAccesoInternet y el cubo la sabe (v3).</summary>
    private static Muestra ConCausa(Seleccion sel, string dia, FilaNos r, Muestra m)
        => CausaDe(sel, dia, r) is { } c ? m with { Causa = CausaNoSolucion.Nombre(c.Clave) } : m;

    /// <summary>La causa de una no solucionada de sinAccesoInternet, con sus señales y fallos; nulo si no se sabe.</summary>
    private static (string Clave, int Senales, int Fallos)? CausaDe(Seleccion sel, string dia, FilaNos r)
    {
        if (sel.Meta.D("n3")[r.N3] != TipologiaInternet) return null;
        var doc = sel.Docs.FirstOrDefault(d => d.D == dia);
        if (doc?.Causas is null || !doc.Causas.TryGetValue(r.Id, out var c)) return null;
        return (CausaNoSolucion.Clave(r.Mascara, c[0]), c[0], c[1]);
    }

    /// <summary>Las no solucionadas de sinAccesoInternet de una causa (o de todas), con su día.</summary>
    private static List<(string Dia, FilaNos Fila, (string Clave, int Senales, int Fallos) Causa)> LlamadasCausa(Seleccion sel, string? clave)
    {
        if (clave is not null && CausaNoSolucion.Todas.All(c => c.Clave != clave))
        {
            throw new FiltroInvalido($"causa desconocida: {FormatoPython.Repr(clave)}");
        }
        var salida = new List<(string, FilaNos, (string, int, int))>();
        foreach (var (dia, r) in Llamadas(sel, TipologiaInternet))
        {
            if (CausaDe(sel, dia, r) is not { } c) continue;
            if (clave is null || c.Clave == clave) salida.Add((dia, r, c));
        }
        return salida;
    }

    /// <summary>Diez llamadas de ejemplo de una causa de no solución de sinAccesoInternet (cubo v3).</summary>
    public static RespuestaMuestras MuestrasCausa(Cubo cubo, string clave, Filtros f)
    {
        var sel = new Seleccion(cubo, f);
        var todas = LlamadasCausa(sel, clave).Select(x => (x.Dia, x.Fila)).ToList();
        return new RespuestaMuestras(clave, todas.Count,
            Elegir(todas).Select(x => ConCausa(sel, x.Dia, x.Fila, ComoMuestra(sel.Meta, x.Dia, x.Fila))).ToList());
    }

    /// <summary>Columnas del CSV de causas.</summary>
    public static readonly string[] CabCausas =
    {
        "id_llamada", "fecha", "marca", "sector", "supervisor", "team_leader", "agente",
        "causa", "senales_de_atencion", "impedimentos_de_proceso", "otros_impedimentos", "averia_n4", "resumen",
    };

    /// <summary>
    /// (nombre, texto) del CSV con cada no solucionada de sinAccesoInternet y su causa: atención,
    /// proceso, las dos, cliente o sin causa, con las señales que la deciden.
    /// </summary>
    public static (string Nombre, string Texto) CsvCausas(Cubo cubo, Filtros f, string? clave = null)
    {
        var sel = new Seleccion(cubo, f);
        var meta = sel.Meta;
        var cats = meta.ImpedCats ?? new List<CategoriaImpedimento>();
        var umbral = meta.UmbralAtencion ?? 0;
        var sb = new StringBuilder(CsvBom);
        sb.Append(string.Join(CsvSep, CabCausas.Select(CsvEscapa))).Append(CsvEol);
        var llamadas = LlamadasCausa(sel, clave);
        var causaDe = new Dictionary<(string, string), (string Clave, int Senales, int Fallos)>();
        foreach (var x in llamadas) causaDe[(x.Dia, x.Fila.Id)] = x.Causa;
        foreach (var (dia, r) in Ordenar(llamadas.Select(x => (x.Dia, x.Fila)).ToList()))
        {
            var c = causaDe[(dia, r.Id)];
            var (u, t) = (meta.Amap[r.A][0], meta.Amap[r.A][1]);
            var proceso = cats.Where(x => (r.Mascara & x.Bit) != 0 && (x.Bit & Ensamblador.MaskProc) != 0).Select(x => x.Nom);
            var otros = cats.Where(x => (r.Mascara & x.Bit) != 0 && (x.Bit & Ensamblador.MaskProc) == 0 && x.Bit != Ensamblador.MaskAten).Select(x => x.Nom);
            var valores = new[]
            {
                r.Id, dia, meta.D("b")[r.B], meta.D("s")[r.S], meta.D("u")[u], meta.D("t")[t], meta.D("a")[r.A],
                CausaNoSolucion.Nombre(c.Clave), string.Join(" / ", CausaNoSolucion.Senales(r.Mascara, c.Senales, c.Fallos, umbral)),
                string.Join(" / ", proceso), string.Join(" / ", otros), r.N4, r.Texto,
            };
            sb.Append(string.Join(CsvSep, valores.Select(CsvEscapa))).Append(CsvEol);
        }
        var acotado = sel.HayFiltro || clave is not null;
        var nombre = $"nosolucion_internet_causas_{sel.Fechas[0].Replace("-", "")}-{sel.Fechas[^1].Replace("-", "")}{(acotado ? "_filtrado" : "")}.csv";
        return (nombre, sb.ToString());
    }

    // =====================================================================
    // OPCIONES DE LOS DESPLEGABLES
    // =====================================================================
    /// <summary>
    /// Lo que ofrecen los desplegables, con el volumen de llamadas de cada
    /// opción: los agentes con su TL y su supervisor (respetando marca y
    /// servicio, no las personas), y las marcas y servicios (cada lista
    /// respeta el filtro de la otra, no el suyo).
    /// </summary>
    public static RespuestaOpciones Opciones(Cubo cubo, Filtros f)
    {
        var sel = new Seleccion(cubo, f, conPersonas: false);
        var meta = sel.Meta;
        var acc = new Dictionary<int, long[]>();
        var porMarca = new Dictionary<int, long>();
        var porServicio = new Dictionary<int, long>();
        foreach (var doc in sel.Docs)
        {
            foreach (var r in doc.Eq)
            {
                var enMarca = sel.B is null || sel.B.Contains(r[0]);
                var enServicio = sel.S is null || sel.S.Contains(r[1]);
                if (enServicio) porMarca[r[0]] = porMarca.GetValueOrDefault(r[0]) + r[3];
                if (enMarca) porServicio[r[1]] = porServicio.GetValueOrDefault(r[1]) + r[3];
                if (enMarca && enServicio)
                {
                    if (!acc.TryGetValue(r[2], out var t)) acc[r[2]] = t = new long[2];
                    t[0] += r[3];
                    t[1] += r[4] + r[5];
                }
            }
        }
        var agentes = acc
            .Where(p => p.Value[0] > 0)
            .Select(p => new OpcionAgente(
                meta.D("a")[p.Key], meta.D("t")[meta.Amap[p.Key][1]], meta.D("u")[meta.Amap[p.Key][0]],
                p.Value[0], p.Value[1]))
            .OrderByDescending(x => x.Llamadas)
            .ThenBy(x => x.Agente, Ensamblador.TextoPython)
            .ToList();

        static List<OpcionVolumen> Volumen(List<string> nombres, Dictionary<int, long> conteo)
            => nombres.Select((n, i) => new OpcionVolumen(n, conteo.GetValueOrDefault(i))).ToList();

        return new RespuestaOpciones(agentes, Volumen(meta.D("b"), porMarca), Volumen(meta.D("s"), porServicio));
    }

    // =====================================================================
    // SIN ACCESO A INTERNET
    // =====================================================================
    private sealed class AcumuladoAveria
    {
        public long N, Enc, Nosol;
        public readonly List<int> OrdenHijos = new();
        public readonly Dictionary<int, AcumuladoAveria> Hijos = new();
    }

    /// <summary>
    /// La vista de sinAccesoInternet: totales, cuadrantes atención/proceso,
    /// impedimentos con su reparto, rúbrica y averías N4 › N5.
    /// </summary>
    public static RespuestaInternet Internet(Cubo cubo, Filtros f)
    {
        var sel = new Seleccion(cubo, f);
        var meta = sel.Meta;
        var abrupto = meta.D("cierre").IndexOf("abruptlyEnded");

        // net: [b, s, a, n4, n5, ticket, escbo, rell, evento, cierre, res, n, nosol]
        long tot = 0, sol = 0, nosol = 0, ticket = 0, escbo = 0, rell = 0, cierreAbrupto = 0;
        var n4 = new Dictionary<int, AcumuladoAveria>();
        var ordenN4 = new List<int>();
        foreach (var r in sel.Filas("net"))
        {
            long n = r[11];
            tot += n;
            if (r[10] == 1) sol += n;
            else if (r[10] == 2) nosol += n;
            if (r[5] == 1) ticket += n;
            if (r[6] == 1) escbo += n;
            if (r[7] == 1) rell += n;
            if (r[9] == abrupto) cierreAbrupto += n;

            if (!n4.TryGetValue(r[3], out var o))
            {
                o = new AcumuladoAveria();
                n4[r[3]] = o;
                ordenN4.Add(r[3]);
            }
            if (!o.Hijos.TryGetValue(r[4], out var h))
            {
                h = new AcumuladoAveria();
                o.Hijos[r[4]] = h;
                o.OrdenHijos.Add(r[4]);
            }
            foreach (var x in new[] { o, h })
            {
                x.N += n;
                if (r[10] is 1 or 2) x.Enc += n;
                if (r[10] == 2) x.Nosol += n;
            }
        }
        var enc = sol + nosol;
        var media = Pct(nosol, enc);

        // cuad: [b, s, a, aten, proc, res, n]
        var clavesCuadrante = new[] { (1, 1), (0, 1), (1, 0), (0, 0) };
        var etiquetas = new Dictionary<(int, int), string>
        {
            [(1, 1)] = "Las dos a la vez",
            [(0, 1)] = "Solo señal de proceso",
            [(1, 0)] = "Solo señal de atención",
            [(0, 0)] = "Ni una cosa ni la otra",
        };
        var q = clavesCuadrante.ToDictionary(k => k, _ => new long[2]);
        foreach (var r in sel.Filas("cuad"))
        {
            var o = q[(r[3], r[4])];
            o[0] += r[6];
            if (r[5] == 2) o[1] += r[6];
        }
        var qNosol = q.Values.Sum(o => o[1]);
        var cuadrantes = clavesCuadrante
            .Select(k => new Cuadrante($"{k.Item1}{k.Item2}", etiquetas[k], q[k][0], q[k][1], Pct(q[k][1], qNosol)))
            .ToList();

        // imp: [b, s, a, bit, res, n]. Una llamada con varios impedimentos cuenta en cada uno.
        var im = new Dictionary<int, long[]>();
        var porMarca = new Dictionary<(int Bit, int Indice), long[]>();
        var ordenMarca = new List<(int, int)>();
        var porServicio = new Dictionary<(int Bit, int Indice), long[]>();
        var ordenServicio = new List<(int, int)>();
        foreach (var r in sel.Filas("imp"))
        {
            if (!im.TryGetValue(r[3], out var t)) im[r[3]] = t = new long[2];
            t[0] += r[5];
            if (r[4] == 2) t[1] += r[5];
            Acumular(porMarca, ordenMarca, (r[3], r[0]), r[5], r[4] == 2);
            Acumular(porServicio, ordenServicio, (r[3], r[1]), r[5], r[4] == 2);
        }

        List<Reparto> Repartir(int bit, Dictionary<(int Bit, int Indice), long[]> acumulado, List<(int, int)> orden, List<string> nombres)
            => orden
                .Where(k => k.Item1 == bit && acumulado[k][0] > 0)
                .Select(k => new Reparto(nombres[k.Item2], acumulado[k][0], acumulado[k][1], Pct(acumulado[k][1], acumulado[k][0])))
                .OrderByDescending(x => x.Nosol)
                .ToList();

        long[] Im(int bit) => im.GetValueOrDefault(bit) ?? new long[2];

        var ext = ExtensionInternet(sel);

        var impedimentos = (meta.ImpedCats ?? new List<CategoriaImpedimento>())
            .Where(c => Im(c.Bit)[0] > 0)
            .Select(c => new Impedimento(
                c.Nom, c.Tipo, c.Bit, Im(c.Bit)[0], Im(c.Bit)[1], Pct(Im(c.Bit)[1], Im(c.Bit)[0]),
                Repartir(c.Bit, porMarca, ordenMarca, meta.D("b")),
                Repartir(c.Bit, porServicio, ordenServicio, meta.D("s")),
                ext is null ? null : ext.SoloEste.GetValueOrDefault(c.Bit),
                ext?.Acompanan.GetValueOrDefault(c.Bit)))
            .OrderByDescending(x => x.Nosol)
            .ToList();

        // rub: [b, s, a, item, valor, res, n]. Valor 1 sí, 0 no; 2 (no aplica) y -1 (sin dato) no cuentan.
        var rb = new Dictionary<int, long[]>();
        foreach (var r in sel.Filas("rub"))
        {
            var (it, val, res, n) = (r[3], r[4], r[5], (long)r[6]);
            if (val is not (0 or 1)) continue;
            if (!rb.TryGetValue(it, out var t)) rb[it] = t = new long[4];
            if (res == 1)
            {
                t[1] += n;
                t[0] += val == 1 ? n : 0;
            }
            else if (res == 2)
            {
                t[3] += n;
                t[2] += val == 1 ? n : 0;
            }
        }
        var rubrica = new List<ItemRubrica>();
        for (var it = 0; it < 7; it++)   // los dos ítems circulares (7 y 8) no se enseñan
        {
            var t = rb.GetValueOrDefault(it) ?? new long[4];
            if (t[1] == 0 && t[3] == 0) continue;
            var pSol = Pct(t[0], t[1]);
            var pNo = Pct(t[2], t[3]);
            rubrica.Add(new ItemRubrica(meta.RubNom[it], pSol, pNo, Resta(pSol, pNo)));
        }
        rubrica = rubrica.OrderByDescending(x => x.Brecha ?? 0).ToList();

        var averias = ordenN4
            .OrderByDescending(k => n4[k].Nosol)
            .Select(k =>
            {
                var o = n4[k];
                return new Averia(
                    meta.D("n4")[k], o.N, o.Enc, o.Nosol, Pct(o.Nosol, o.Enc),
                    o.OrdenHijos
                        .OrderByDescending(hk => o.Hijos[hk].Nosol)
                        .Select(hk => new DetalleAveria(meta.D("n5")[hk], o.Hijos[hk].N, o.Hijos[hk].Enc, o.Hijos[hk].Nosol, Pct(o.Hijos[hk].Nosol, o.Hijos[hk].Enc)))
                        .ToList());
            })
            .ToList();

        return new RespuestaInternet(
            new TotalesInternet(tot, enc, sol, nosol, media, ticket, escbo, rell, cierreAbrupto),
            cuadrantes,
            qNosol,
            meta.UmbralAtencion,
            meta.UmbralBase,
            impedimentos,
            Im(0)[0],
            rubrica,
            averias,
            ext?.Causas,
            ext is null ? null : ext.Cuadre with { SumaFilas = impedimentos.Sum(x => x.Nosol) },
            Repetidas(sel));
    }

    /// <summary>Lo que se calcula con el corte <c>ver</c> (cubo v3); nulo con un cubo anterior.</summary>
    private sealed record Extension(
        List<CausaInternet> Causas, CuadreImpedimentos Cuadre,
        Dictionary<int, long> SoloEste, Dictionary<int, List<Conteo>> Acompanan);

    /// <summary>
    /// La causa única de cada no solucionada de sinAccesoInternet, el cuadre de llamadas distintas
    /// de «Qué frena el proceso» y, por impedimento, cuántas lo traen solo a él y con qué otros viene.
    /// </summary>
    private static Extension? ExtensionInternet(Seleccion sel)
    {
        if (sel.Docs.All(d => d.Ver is null)) return null;
        var meta = sel.Meta;
        var cats = meta.ImpedCats ?? new List<CategoriaImpedimento>();
        var nombre = cats.ToDictionary(c => c.Bit, c => c.Nom);

        var porCausa = CausaNoSolucion.Todas.ToDictionary(c => c.Clave, _ => new long[2]);
        var detalle = CausaNoSolucion.Todas.ToDictionary(c => c.Clave, _ => new Dictionary<(string Nombre, string Grupo), long>());
        var soloEste = new Dictionary<int, long>();
        var juntos = new Dictionary<(int, int), long>();
        long nosol = 0, conImp = 0, sinImp = 0, conVarios = 0;

        void Sumar(string causa, string razon, string grupo, long n)
        {
            var d = detalle[causa];
            d[(razon, grupo)] = d.GetValueOrDefault((razon, grupo)) + n;
        }

        // ver: [b, s, a, mascara, senales, res, n]
        foreach (var r in sel.Filas("ver"))
        {
            var (m, senales, res, n) = (r[3], r[4], r[5], (long)r[6]);
            var causa = CausaNoSolucion.Clave(m, senales);
            porCausa[causa][0] += n;
            if (res != 2) continue;
            porCausa[causa][1] += n;
            nosol += n;

            var bits = cats.Where(c => (m & c.Bit) != 0).Select(c => c.Bit).ToList();
            if (bits.Count == 0) sinImp += n; else conImp += n;
            if (bits.Count > 1) conVarios += n;
            if (bits.Count == 1) soloEste[bits[0]] = soloEste.GetValueOrDefault(bits[0]) + n;
            foreach (var a in bits)
            {
                foreach (var b in bits)
                {
                    if (a != b) juntos[(a, b)] = juntos.GetValueOrDefault((a, b)) + n;
                }
            }

            // Las razones dentro de cada causa (una llamada puede tener varias).
            if (causa is CausaNoSolucion.Proceso or CausaNoSolucion.ProcesoYAtencion)
            {
                foreach (var b in bits.Where(b => (b & Ensamblador.MaskProc) != 0)) Sumar(causa, nombre[b], "proceso", n);
            }
            if (causa is CausaNoSolucion.Atencion or CausaNoSolucion.ProcesoYAtencion)
            {
                if ((senales & CausaNoSolucion.SenalCierreAbrupto) != 0) Sumar(causa, "Cierre abrupto de la llamada", "atencion", n);
                if ((m & Ensamblador.MaskAten) != 0) Sumar(causa, "Impedimento atribuido al agente", "atencion", n);
                if ((senales & CausaNoSolucion.SenalRubrica) != 0) Sumar(causa, "Rúbrica peor que la mediana de las solucionadas", "atencion", n);
            }
            if (causa == CausaNoSolucion.Cliente)
            {
                foreach (var b in bits.Where(b => (b & CausaNoSolucion.MaskCliente) != 0)) Sumar(causa, nombre[b], "otro", n);
            }
            if (causa == CausaNoSolucion.SinCausa)
            {
                if (bits.Count == 0) Sumar(causa, "Ningún impedimento", "otro", n);
                foreach (var b in bits) Sumar(causa, nombre[b], "otro", n);
            }
        }

        var causas = CausaNoSolucion.Todas
            .Select(c => new CausaInternet(
                c.Clave, c.Nombre, c.Explicacion, porCausa[c.Clave][0], porCausa[c.Clave][1],
                Pct(porCausa[c.Clave][1], nosol),
                detalle[c.Clave].OrderByDescending(p => p.Value)
                    .Select(p => new Conteo(p.Key.Nombre, p.Value, p.Key.Grupo)).ToList()))
            .ToList();
        var acompanan = cats.ToDictionary(
            c => c.Bit,
            c => juntos.Where(p => p.Key.Item1 == c.Bit).OrderByDescending(p => p.Value).Take(3)
                       .Select(p => new Conteo(nombre[p.Key.Item2], p.Value)).ToList());
        return new Extension(causas, new CuadreImpedimentos(nosol, conImp, sinImp, conVarios, 0), soloEste, acompanan);
    }

    /// <summary>Registros frente a conversaciones y llamadas distintas, con el filtro de marca (cubo v3).</summary>
    private static ControlRepetidas? Repetidas(Seleccion sel)
    {
        if (sel.Docs.Count == 0 || sel.Docs.Any(d => d.Dup is null)) return null;
        var t = new long[6];
        foreach (var d in sel.Docs)
        {
            foreach (var r in d.Dup!)
            {
                if (sel.B is not null && !sel.B.Contains((int)r[0])) continue;
                for (var j = 0; j < 6; j++) t[j] += r[1 + j];
            }
        }
        return new ControlRepetidas(t[0], t[1], t[2], t[3], t[4], t[5]);
    }

    private static void Acumular(
        Dictionary<(int Bit, int Indice), long[]> destino, List<(int, int)> orden, (int, int) clave, long n, bool nosol)
    {
        if (!destino.TryGetValue(clave, out var t))
        {
            destino[clave] = t = new long[2];
            orden.Add(clave);
        }
        t[0] += n;
        if (nosol) t[1] += n;
    }

    // =====================================================================
    // META
    // =====================================================================
    /// <summary>Qué datos hay: rango, marcas, servicios y suelos. Es lo que sondea la pantalla.</summary>
    public static MetaPublica MetaPublica(Cubo cubo)
    {
        var m = cubo.Meta;
        return new MetaPublica(
            m.DiaMin, m.DiaMax, m.Ventana, m.Dias.Count, m.DiasParciales,
            m.DiasProvisionales ?? new List<string>(),
            m.Origen ?? "informe", m.EtiquetasNuevas, m.Generado, m.Proyecto, m.Fuente,
            m.Cruce, m.D("b"), m.D("s"), m.Suelo, m.EtiquetasPorPalabras);
    }
}

// =========================================================================
// Las respuestas, con los nombres de campo del backend en Python
// =========================================================================

public sealed record Totales(
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("sol")] long Sol,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("encuestadas")] long Encuestadas,
    [property: JsonPropertyName("pct")] double? Pct,
    [property: JsonPropertyName("cobertura")] double? Cobertura);

public sealed class MarcaDia
{
    [JsonPropertyName("encuestadas")] public long Encuestadas { get; set; }
    [JsonPropertyName("nosol")] public long Nosol { get; set; }
    [JsonPropertyName("pct")] public double? Pct { get; set; }
}

public sealed class DiaSerie
{
    [JsonPropertyName("dia")] public string Dia { get; set; } = "";
    [JsonPropertyName("parcial")] public bool Parcial { get; set; }
    [JsonPropertyName("llamadas")] public long Llamadas { get; set; }
    [JsonPropertyName("sol")] public long Sol { get; set; }
    [JsonPropertyName("nosol")] public long Nosol { get; set; }
    [JsonPropertyName("marcas")] public Dictionary<string, MarcaDia> Marcas { get; set; } = new();
    [JsonPropertyName("encuestadas")] public long Encuestadas { get; set; }
    [JsonPropertyName("pct")] public double? Pct { get; set; }
    [JsonPropertyName("provisional")] public bool Provisional { get; set; }
}

public sealed record PeorDia(
    [property: JsonPropertyName("dia")] string Dia,
    [property: JsonPropertyName("dia_semana")] string DiaSemana,
    [property: JsonPropertyName("encuestadas")] long Encuestadas,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("pct")] double? Pct,
    [property: JsonPropertyName("desvio")] double? Desvio);

public sealed record VentanaResumen(
    [property: JsonPropertyName("desde")] string Desde,
    [property: JsonPropertyName("hasta")] string Hasta,
    [property: JsonPropertyName("dias")] int Dias);

public sealed record Saliente(
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("respuestas")] long Respuestas);

public sealed record RespuestaResumen(
    [property: JsonPropertyName("ventana")] VentanaResumen Ventana,
    [property: JsonPropertyName("provisionales")] List<string> Provisionales,
    [property: JsonPropertyName("totales")] Totales Totales,
    [property: JsonPropertyName("previo")] Totales? Previo,
    [property: JsonPropertyName("delta_pp")] double? DeltaPp,
    [property: JsonPropertyName("cruce_nomina")] double? CruceNomina,
    [property: JsonPropertyName("marcas")] List<string> Marcas,
    [property: JsonPropertyName("serie")] List<DiaSerie> Serie,
    [property: JsonPropertyName("parciales")] List<string> Parciales,
    [property: JsonPropertyName("peores")] List<PeorDia> Peores,
    [property: JsonPropertyName("suelo_dia")] int SueloDia,
    [property: JsonPropertyName("saliente")] Saliente Saliente);

/// <summary>
/// Una fila de equipos. <c>tl</c>, <c>supervisor</c> y <c>agentes</c> solo
/// salen en los niveles que los tienen: se omiten cuando son nulos.
/// </summary>
public sealed class FilaEquipo
{
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("llamadas")] public long Llamadas { get; set; }
    [JsonPropertyName("encuestadas")] public long Encuestadas { get; set; }
    [JsonPropertyName("nosol")] public long Nosol { get; set; }
    [JsonPropertyName("pct")] public double? Pct { get; set; }
    [JsonPropertyName("cobertura")] public double? Cobertura { get; set; }
    [JsonPropertyName("desvio")] public double? Desvio { get; set; }

    [JsonPropertyName("tl"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Tl { get; set; }

    [JsonPropertyName("supervisor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Supervisor { get; set; }

    [JsonPropertyName("agentes"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Agentes { get; set; }
}

public sealed record RangoCobertura(
    [property: JsonPropertyName("p10")] double P10,
    [property: JsonPropertyName("p90")] double P90);

public sealed record RespuestaEquipos(
    [property: JsonPropertyName("nivel")] string Nivel,
    [property: JsonPropertyName("media")] double? Media,
    [property: JsonPropertyName("suelo")] int Suelo,
    [property: JsonPropertyName("suelo_base_90d")] int SueloBase90d,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("fuera")] int Fuera,
    [property: JsonPropertyName("filas")] List<FilaEquipo> Filas,
    [property: JsonPropertyName("cobertura")] RangoCobertura? Cobertura);

/// <summary>Una fila de motivos. <c>peso</c> solo sale en las N3.</summary>
public sealed class FilaMotivo
{
    [JsonPropertyName("nombre")] public string Nombre { get; set; } = "";
    [JsonPropertyName("llamadas")] public long Llamadas { get; set; }
    [JsonPropertyName("encuestadas")] public long Encuestadas { get; set; }
    [JsonPropertyName("nosol")] public long Nosol { get; set; }
    [JsonPropertyName("pct")] public double? Pct { get; set; }
    [JsonPropertyName("cobertura")] public double? Cobertura { get; set; }
    [JsonPropertyName("desvio")] public double? Desvio { get; set; }

    [JsonPropertyName("peso"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Peso { get; set; }
}

public sealed record RespuestaMotivos(
    [property: JsonPropertyName("media")] double? Media,
    [property: JsonPropertyName("suelo_n2")] int SueloN2,
    [property: JsonPropertyName("suelo_n3")] int SueloN3,
    [property: JsonPropertyName("n2")] List<FilaMotivo> N2,
    [property: JsonPropertyName("n3")] List<FilaMotivo> N3);

/// <summary>Una llamada de ejemplo. <c>conversacion</c> es su id (<c>conversationId</c>).</summary>
public sealed record Muestra(
    [property: JsonPropertyName("dia")] string Dia,
    [property: JsonPropertyName("agente")] string Agente,
    [property: JsonPropertyName("marca")] string Marca,
    [property: JsonPropertyName("servicio")] string Servicio,
    [property: JsonPropertyName("texto")] string Texto,
    [property: JsonPropertyName("conversacion")] string Conversacion,
    [property: JsonPropertyName("averia")] string? Averia,
    [property: JsonPropertyName("impedimentos")] List<string> Impedimentos,
    [property: JsonPropertyName("causa"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Causa = null);

public sealed record RespuestaMuestras(
    [property: JsonPropertyName("n3")] string N3,
    [property: JsonPropertyName("total")] long Total,
    [property: JsonPropertyName("muestras")] List<Muestra> Muestras);

public sealed record RespuestaMuestrasImpedimento(
    [property: JsonPropertyName("bit")] int Bit,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("total")] long Total,
    [property: JsonPropertyName("muestras")] List<Muestra> Muestras);

public sealed record OpcionAgente(
    [property: JsonPropertyName("agente")] string Agente,
    [property: JsonPropertyName("tl")] string Tl,
    [property: JsonPropertyName("supervisor")] string Supervisor,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("encuestadas")] long Encuestadas);

public sealed record OpcionVolumen(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("llamadas")] long Llamadas);

public sealed record RespuestaOpciones(
    [property: JsonPropertyName("agentes")] List<OpcionAgente> Agentes,
    [property: JsonPropertyName("marcas")] List<OpcionVolumen> Marcas,
    [property: JsonPropertyName("servicios")] List<OpcionVolumen> Servicios);

public sealed record TotalesInternet(
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("encuestadas")] long Encuestadas,
    [property: JsonPropertyName("sol")] long Sol,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("pct")] double? Pct,
    [property: JsonPropertyName("con_ticket")] long ConTicket,
    [property: JsonPropertyName("escaladas_bo")] long EscaladasBo,
    [property: JsonPropertyName("rellamada_72h")] long Rellamada72h,
    [property: JsonPropertyName("cierre_abrupto")] long CierreAbrupto);

public sealed record Cuadrante(
    [property: JsonPropertyName("clave")] string Clave,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("peso")] double? Peso);

public sealed record Reparto(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("pct")] double? Pct);

public sealed record Impedimento(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("tipo")] string? Tipo,
    [property: JsonPropertyName("bit")] int Bit,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("pct")] double? Pct,
    [property: JsonPropertyName("por_marca")] List<Reparto> PorMarca,
    [property: JsonPropertyName("por_servicio")] List<Reparto> PorServicio,
    [property: JsonPropertyName("solo_este"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? SoloEste = null,
    [property: JsonPropertyName("acompanan"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<Conteo>? Acompanan = null);

public sealed record ItemRubrica(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("pct_solucionadas")] double? PctSolucionadas,
    [property: JsonPropertyName("pct_no_solucionadas")] double? PctNoSolucionadas,
    [property: JsonPropertyName("brecha")] double? Brecha);

public sealed record DetalleAveria(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("encuestadas")] long Encuestadas,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("pct")] double? Pct);

public sealed record Averia(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("encuestadas")] long Encuestadas,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("pct")] double? Pct,
    [property: JsonPropertyName("detalle")] List<DetalleAveria> Detalle);

public sealed record RespuestaInternet(
    [property: JsonPropertyName("totales")] TotalesInternet Totales,
    [property: JsonPropertyName("cuadrantes")] List<Cuadrante> Cuadrantes,
    [property: JsonPropertyName("cuadrantes_nosol")] long CuadrantesNosol,
    [property: JsonPropertyName("umbral_atencion")] int? UmbralAtencion,
    [property: JsonPropertyName("umbral_base")] int? UmbralBase,
    [property: JsonPropertyName("impedimentos")] List<Impedimento> Impedimentos,
    [property: JsonPropertyName("sin_impedimento")] long SinImpedimento,
    [property: JsonPropertyName("rubrica")] List<ItemRubrica> Rubrica,
    [property: JsonPropertyName("averias")] List<Averia> Averias,
    [property: JsonPropertyName("causas"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<CausaInternet>? Causas = null,
    [property: JsonPropertyName("cuadre_impedimentos"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CuadreImpedimentos? CuadreImpedimentos = null,
    [property: JsonPropertyName("repetidas"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ControlRepetidas? Repetidas = null);

// =========================================================================
// Extensiones de esta web (cubo v3): no existen en el Python de referencia
// =========================================================================

/// <summary>Un nombre con su cifra; <c>Grupo</c> dice si es una razón de proceso, de atención u otra.</summary>
public sealed record Conteo(
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("n")] long N,
    [property: JsonPropertyName("grupo"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Grupo = null);

/// <summary>
/// Una causa de no solución de sinAccesoInternet (<see cref="CausaNoSolucion"/>): cada llamada no
/// solucionada está en una sola. <c>Detalle</c> son sus razones, que sí se pueden repetir.
/// </summary>
public sealed record CausaInternet(
    [property: JsonPropertyName("clave")] string Clave,
    [property: JsonPropertyName("nombre")] string Nombre,
    [property: JsonPropertyName("explicacion")] string Explicacion,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("peso")] double? Peso,
    [property: JsonPropertyName("detalle")] List<Conteo> Detalle);

/// <summary>
/// Cuadre de «Qué frena el proceso»: las filas cuentan una llamada en cada categoría que trae,
/// así que su suma pasa del número de llamadas.
/// </summary>
public sealed record CuadreImpedimentos(
    [property: JsonPropertyName("nosol")] long Nosol,
    [property: JsonPropertyName("con_impedimento")] long ConImpedimento,
    [property: JsonPropertyName("sin_impedimento")] long SinImpedimento,
    [property: JsonPropertyName("con_varios")] long ConVarios,
    [property: JsonPropertyName("suma_filas")] long SumaFilas);

/// <summary>Registros de sinAccesoInternet frente a conversaciones y llamadas físicas distintas.</summary>
public sealed record ControlRepetidas(
    [property: JsonPropertyName("registros")] long Registros,
    [property: JsonPropertyName("conversaciones")] long Conversaciones,
    [property: JsonPropertyName("llamadas")] long Llamadas,
    [property: JsonPropertyName("registros_nosol")] long RegistrosNosol,
    [property: JsonPropertyName("conversaciones_nosol")] long ConversacionesNosol,
    [property: JsonPropertyName("llamadas_nosol")] long LlamadasNosol);

public sealed record MetaPublica(
    [property: JsonPropertyName("dia_min")] string? DiaMin,
    [property: JsonPropertyName("dia_max")] string? DiaMax,
    [property: JsonPropertyName("ventana")] int Ventana,
    [property: JsonPropertyName("dias_disponibles")] int DiasDisponibles,
    [property: JsonPropertyName("dias_parciales")] List<string> DiasParciales,
    [property: JsonPropertyName("dias_provisionales")] List<string> DiasProvisionales,
    [property: JsonPropertyName("origen")] string Origen,
    [property: JsonPropertyName("etiquetas_nuevas")] int? EtiquetasNuevas,
    [property: JsonPropertyName("generado")] string? Generado,
    [property: JsonPropertyName("proyecto")] string? Proyecto,
    [property: JsonPropertyName("fuente")] string? Fuente,
    [property: JsonPropertyName("cruce_nomina")] double? CruceNomina,
    [property: JsonPropertyName("marcas")] List<string> Marcas,
    [property: JsonPropertyName("servicios")] List<string> Servicios,
    [property: JsonPropertyName("suelo")] Dictionary<string, int>? Suelo,
    [property: JsonPropertyName("etiquetas_por_palabras"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? EtiquetasPorPalabras = null);
