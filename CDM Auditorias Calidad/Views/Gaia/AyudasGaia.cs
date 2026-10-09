using System.Text.RegularExpressions;
using CDM_Auditorias_Calidad.Infraestructura;
using CDM_Auditorias_Calidad.Servicios.Gaia;
using CDM_Auditorias_Calidad.Models;
using CDM_Auditorias_Calidad.Models.Gaia;

namespace CDM_Auditorias_Calidad.Views.Gaia;

/// <summary>
/// Pequeños ayudantes de presentación de las vistas de GAIA Formación: solo dan forma a lo que se pinta
/// (etiquetas, formatos y la clase de color de cada umbral). Los cálculos viven en <see cref="CalculadoraGaia"/>.
/// Va en la carpeta de las vistas porque <c>@functions</c> no funciona en <c>_ViewImports</c>; se importa allí
/// con <c>@using static</c>.
/// </summary>
public static class AyudasGaia
{
    /// <summary>Una llamada con menos de esto no cuenta como máximo o mínimo de un día, ni se colorea un agente.</summary>
    public const int MinLlamadasFiable = 10;

    /// <summary>«1 Preconexion» → «Pre 1»; «Aseguramiento 3» → «Aseg. 3» (para los ejes).</summary>
    public static string EtapaCorta(string etapa)
    {
        if (etapa.EndsWith("Preconexion", StringComparison.OrdinalIgnoreCase)) return "Pre " + etapa.Split(' ')[0];
        if (etapa.StartsWith("Aseguramiento", StringComparison.OrdinalIgnoreCase)) return "Aseg. " + etapa.Split(' ').Last();
        return etapa;
    }

    /// <summary>Etapa con tilde para leerla: «1 Preconexion» → «1 Preconexión».</summary>
    public static string EtapaLegible(string etapa) => etapa.Replace("Preconexion", "Preconexión");

    /// <summary>«incidenciaOrReclamación» → «Incidencia o reclamación»; «NA» o vacío → «—».</summary>
    public static string MotivoLegible(string motivo)
    {
        if (string.IsNullOrWhiteSpace(motivo) || motivo == "NA") return "—";
        var partes = Regex
            .Split(Regex.Replace(motivo.Trim(), "(?<=[a-zñáéíóú])O(?=[A-ZÑÁÉÍÓÚ])", "Or"), "(?<=[a-zñáéíóú])(?=[A-ZÑÁÉÍÓÚ])")
            .Select(p => p == "Or" ? "o" : ConTildes(p.ToLowerInvariant()));
        var texto = string.Join(" ", partes);
        return char.ToUpper(texto[0], Formato.Es) + texto[1..];
    }

    /// <summary>«yes» → Sí, «no» → No, vacío → «—» y cualquier otra cosa (NA, notApplicable) → N/A.</summary>
    public static string Calificacion(string valor)
        => valor.Length == 0 ? "—" : valor.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "Sí"
           : valor.Equals("no", StringComparison.OrdinalIgnoreCase) ? "No" : "N/A";

    public static string ChipCalificacion(string valor)
        => valor.Equals("yes", StringComparison.OrdinalIgnoreCase) ? "chip chip-bueno"
           : valor.Equals("no", StringComparison.OrdinalIgnoreCase) ? "chip chip-critico" : "chip";

    public static string SiNo(bool? valor) => valor switch { true => "Sí", false => "No", _ => "—" };

    public static string RellamadaTexto(int? valor) => valor switch { 1 => "Sí", 0 => "No", _ => "—" };

    public static string EncuestaTexto(int? valor) => valor switch { 1 => "Resuelto", 2 => "No resuelto", _ => "—" };

    /// <summary>Segundos → m:ss («5:32»); vacío → «—».</summary>
    public static string Duracion(double? segundos)
    {
        if (segundos is not { } s) return "—";
        var total = (int)Math.Round(s);
        return $"{total / 60}:{total % 60:00}";
    }

    public static string Texto(string valor) => string.IsNullOrWhiteSpace(valor) || valor == "NA" ? "—" : valor;

    /// <summary>Lo que DataOrb escribe cuando no hay dato («No aplicable», «Not applicable», «String», «NA»…).</summary>
    public static bool HayTexto(string valor) => CalculadoraGaia.TieneValor(valor);

    /// <summary>Clase de tono de la barra de adherencia según los umbrales del PBI (≤ 40 %, ≤ 70 %, más).</summary>
    public static string TonoAdherencia(double? v)
        => v is not { } a ? "tono-neutro"
           : a <= PaginaRankingEstiloGaia.UmbralBajo ? "tono-critico" : a <= PaginaRankingEstiloGaia.UmbralMedio ? "tono-atencion" : "tono-bueno";

    /// <summary>Clase del semáforo pastel de una celda de adherencia.</summary>
    public static string MapaAdherencia(double? v)
        => v is not { } a ? ""
           : a <= PaginaRankingEstiloGaia.UmbralBajo ? "mapa-rojo" : a <= PaginaRankingEstiloGaia.UmbralMedio ? "mapa-ambar" : "mapa-verde";

    /// <summary>
    /// Si un valor es mejor o peor que el total del filtro (más alto o más bajo es mejor según el indicador).
    /// Margen: un punto porcentual o el 10 % del total, lo que sea mayor. Con pocas llamadas no se compara.
    /// </summary>
    public static string ClaseFrenteAlTotal(KpiGaia kpi, IndicadoresGaia fila, IndicadoresGaia total)
    {
        if (fila.Llamadas < MinLlamadasFiable) return "";
        if (kpi.Valor(fila) is not { } v || kpi.Valor(total) is not { } t) return "";
        var margen = Math.Max(0.01, t * 0.10);
        if (Math.Abs(v - t) <= margen) return "";
        return (v > t) == kpi.MejorAlto ? "mejor-total" : "peor-total";
    }

    /// <summary>Para la ficha de las gráficas: «Rellamada 72 h» → «Rellamada (72 h)» (la ficha parte donde empieza la primera cifra).</summary>
    public static string NombreFicha(string nombre) => nombre.Replace(" 72 h", " (72 h)").Replace(" 24 h", " (24 h)");

    /// <summary>Color fijo de cada marca en las piezas (el mismo que en No solución).</summary>
    public static string ClaseMarca(string marca) => marca switch
    {
        "YOIGO" => "serie-yoigo", "MASMOVIL" => "serie-masmovil", "JAZZTEL" => "serie-jazztel", "ORANGE" => "serie-orange", _ => "serie-otra",
    };

    /// <summary>
    /// Reparte los desplegables visibles del panel de filtros en grupos separados por una línea: cada «bloque» es una lista de
    /// campos; los visibles que no estén en ninguno van al último grupo y los grupos vacíos no salen.
    /// </summary>
    public static IEnumerable<List<GrupoFiltro>> EnBloques(IEnumerable<GrupoFiltro> grupos, params string[][] bloques)
    {
        var visibles = grupos.Where(g => g.Visible).ToList();
        var nombrados = bloques.SelectMany(b => b).ToHashSet();
        for (var i = 0; i < bloques.Length; i++)
        {
            var lista = visibles.Where(g => bloques[i].Contains(g.Campo)).ToList();
            if (i == bloques.Length - 1) lista.AddRange(visibles.Where(g => !nombrados.Contains(g.Campo)));
            if (lista.Count > 0) yield return lista;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Indicador + volumen (el patrón del portal): una línea suavizada con su área y las llamadas detrás
    // ---------------------------------------------------------------------------------------

    /// <summary>Un indicador de la gráfica «indicador + volumen» (una pestaña): cómo se llama y de dónde sale su valor en cada periodo.</summary>
    /// <param name="Segundos">Los valores son segundos (TMO) y no fracciones.</param>
    public sealed record IndicadorGaia(string Clave, string Nombre, Func<IndicadoresGaia, double?> Valor, bool Segundos = false);

    /// <summary>Los indicadores (pestañas) con sus semanas y sus días; las llamadas de cada periodo salen de sus indicadores.</summary>
    /// <param name="Id">Distingue las gráficas de una misma página (sirve para los degradados del SVG).</param>
    public sealed record IndicadorVolumenGaia(string Id, IReadOnlyList<IndicadorGaia> Indicadores, IReadOnlyList<GrupoGaia> Semanas, IReadOnlyList<GrupoGaia> Dias)
    {
        /// <summary>Ancho en píxeles del plano (decide cuántas pastillas caben).</summary>
        public int AnchoEstimado { get; init; } = 900;
    }

    /// <summary>
    /// Una línea suavizada (curva de Catmull-Rom pasada a Bézier) por los puntos con valor, que se corta donde falta un dato, y su área
    /// hasta el eje. <paramref name="ys"/> son posiciones (0 arriba, 1 abajo) y <paramref name="x"/> la posición horizontal de cada periodo.
    /// </summary>
    public static (string Linea, string Area) TrazosSuaves(IReadOnlyList<double?> ys, Func<int, double> x)
    {
        var linea = new System.Text.StringBuilder();
        var area = new System.Text.StringBuilder();
        var i = 0;
        while (i < ys.Count)
        {
            if (ys[i] is null) { i++; continue; }
            var tramo = new List<(double X, double Y)>();
            while (i < ys.Count && ys[i] is { } y) { tramo.Add((x(i), y)); i++; }
            var curva = new System.Text.StringBuilder($"M{C(tramo[0].X)},{C(tramo[0].Y)} ");
            for (var k = 0; k + 1 < tramo.Count; k++)
            {
                var p0 = tramo[Math.Max(0, k - 1)];
                var p1 = tramo[k];
                var p2 = tramo[k + 1];
                var p3 = tramo[Math.Min(tramo.Count - 1, k + 2)];
                // Los puntos de control se quedan dentro del rango vertical del tramo: así la curva no se pasa
                // de largo en picos y valles (no baja de 0 ni inventa máximos que no están en los datos).
                double Acotar(double y) => Math.Clamp(y, Math.Min(p1.Y, p2.Y), Math.Max(p1.Y, p2.Y));
                var c1 = (X: p1.X + (p2.X - p0.X) / 6, Y: Acotar(p1.Y + (p2.Y - p0.Y) / 6));
                var c2 = (X: p2.X - (p3.X - p1.X) / 6, Y: Acotar(p2.Y - (p3.Y - p1.Y) / 6));
                curva.Append($"C{C(c1.X)},{C(c1.Y)} {C(c2.X)},{C(c2.Y)} {C(p2.X)},{C(p2.Y)} ");
            }
            linea.Append(curva);
            area.Append(curva).Append($"L{C(tramo[^1].X)},1000 L{C(tramo[0].X)},1000 Z ");
        }
        return (linea.ToString(), area.ToString());
    }

    /// <summary>Una celda de una columna del ranking: porcentaje, segundos con dos decimales o entero; «—» sin dato.</summary>
    public static string CeldaRanking(double? valor, ColumnaRankingGaia columna)
        => valor is not { } v ? "—"
           : columna.Formato == "pct" ? Formato.Porcentaje(v)
           : columna.Formato == "seg" ? Formato.Decimal2(v)
           : Formato.Entero((int)Math.Round(v));

    /// <summary>Un motivo de contacto legible; «Sin asignar» (lo que no trae motivo) se queda como viene.</summary>
    public static string MotivoDe(string clave) => clave == "Sin asignar" ? clave : MotivoLegible(clave);


    public static string DiaLargo(string clave) => DateOnly.TryParse(clave, out var d) ? Formato.Fecha(d) : clave;

    public static string P(double fraccion) => Formato.Coord(fraccion * 100) + "%";
    public static string C(double fraccion) => Formato.Coord(fraccion * 1000);

    // ---------------------------------------------------------------------------------------
    // Textos largos, motivos y obstáculos
    // ---------------------------------------------------------------------------------------

    private static readonly Dictionary<string, string> Tildes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["movil"] = "móvil", ["tecnico"] = "técnico", ["logistica"] = "logística", ["telefonia"] = "telefonía",
        ["generico"] = "genérico", ["erronea"] = "errónea", ["erroneo"] = "erróneo", ["area"] = "área", ["mas"] = "más",
    };

    /// <summary>Un motivo de DataOrb a veces llega sin tildes («facturacionOrCobros»): se las pone a lo habitual.</summary>
    private static string ConTildes(string palabra)
    {
        if (Tildes.TryGetValue(palabra, out var t)) return t;
        return palabra.Length > 4 && palabra.EndsWith("ion", StringComparison.Ordinal) ? palabra[..^3] + "ión" : palabra;
    }

    /// <summary>Los obstáculos de una llamada: vienen separados por « | »; sin los que no dicen nada (NA, String…).</summary>
    public static IReadOnlyList<string> ObstaculosDe(string texto)
        => texto.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Where(CalculadoraGaia.TieneValor).Distinct().ToList();

    /// <summary>
    /// Un resumen largo («Objetivo del Agente: …\nCatalizador del Contacto: …») en párrafos, cada uno con su rótulo
    /// (lo que va antes de los primeros dos puntos, si es corto) aparte del texto.
    /// </summary>
    public static IReadOnlyList<(string? Rotulo, string Texto)> Parrafos(string resumen)
        => resumen.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(linea =>
        {
            var i = linea.IndexOf(": ", StringComparison.Ordinal);
            return i is > 2 and < 45 && !linea[..i].Contains('.') ? ((string?)linea[..i], linea[(i + 2)..]) : (null, linea);
        }).ToList();

    // ---------------------------------------------------------------------------------------
    // Gráficas genéricas (varias series, día / semana / etapa, porcentajes o segundos)
    // ---------------------------------------------------------------------------------------

    /// <summary>Una línea o un grupo de columnas: sus valores en el orden de los puntos del gráfico.</summary>
    /// <param name="Clase">Clase de color fijo (<c>serie-a</c> … <c>serie-e</c>, <c>serie-espana</c>, <c>serie-colombia</c>).</param>
    public sealed record SerieGaia(string Nombre, string Clase, IReadOnlyList<double?> Valores);

    /// <summary>
    /// Lo que necesitan las gráficas de línea y de columnas: un punto por día, semana o etapa.
    /// <paramref name="Volumen"/> es lo que se enseña en la ficha (llamadas o base).
    /// </summary>
    public sealed record GraficoGaia(IReadOnlyList<string> Etiquetas, IReadOnlyList<string> Titulos, IReadOnlyList<int> Volumen, IReadOnlyList<SerieGaia> Series)
    {
        /// <summary>Los valores son segundos (TMO).</summary>
        public bool Segundos { get; init; }
        /// <summary>Los valores son recuentos (llamadas): enteros con miles.</summary>
        public bool Entero { get; init; }
        public string NombreVolumen { get; init; } = "Llamadas";
        /// <summary>Etiquetas del eje X (las líneas con el volumen debajo van sin ellas).</summary>
        public bool EjeX { get; init; } = true;
        /// <summary>Las columnas llevan el color de los umbrales de la adherencia.</summary>
        public bool PorUmbral { get; init; }
        public bool ConLeyenda { get; init; } = true;
        /// <summary>Las líneas llevan el volumen (llamadas, base, auditorías) como columnas grises detrás, en el mismo plano.</summary>
        public bool ConVolumen { get; init; } = true;
        /// <summary>Los puntos son días (si son muchos, se rotula uno de cada dos o menos).</summary>
        public bool Dia { get; init; }
        /// <summary>Ancho en píxeles del plano: decide cuántas pastillas caben (media tarjeta, 400; tarjeta entera, 800).</summary>
        public int AnchoEstimado { get; init; } = 800;
        public int MaxEtiquetasX { get; init; } = 12;
        public string Descripcion { get; init; } = "";
        /// <summary>Sin «Llamadas N» (o <see cref="NombreVolumen"/>) al final de la ficha: cuando el volumen ya es una de las series.</summary>
        public bool SinVolumen { get; init; }
        /// <summary>Un texto más al final de la ficha de cada periodo (p. ej. «Nota 85,20 %»), en el orden de los puntos.</summary>
        public IReadOnlyList<string>? FichaExtra { get; init; }
        /// <summary>Los puntos son etapas de formación: con una sola serie, las columnas llevan el color de su grupo (preconexión o aseguramiento).</summary>
        public bool PorEtapa { get; init; }
    }

    /// <summary>Grupo de una etapa de formación: «1 Preconexión» → <c>etapa-pre</c>; «Aseguramiento 3» → <c>etapa-aseg</c> (colores de la marca, guía 2.2).</summary>
    public static string ClaseEtapa(string etapa)
        => etapa.Contains("Preconexi", StringComparison.OrdinalIgnoreCase) ? "etapa-pre" : "etapa-aseg";

    /// <summary>El texto de la ficha de un periodo: «Etiqueta · Serie valor · … · Llamadas N» (la ficha de site.js lo parte por « · »).</summary>
    public static string Ficha(GraficoGaia g, int i)
    {
        var partes = new List<string> { g.Titulos[i] };
        partes.AddRange(g.Series.Select(s => $"{NombreFicha(s.Nombre)} {Valor(s.Valores[i], g)}"));
        if (!g.SinVolumen) partes.Add($"{g.NombreVolumen} {Formato.Entero(g.Volumen[i])}");
        if (g.FichaExtra is { } extra && i < extra.Count) partes.Add(extra[i]);
        return string.Join(" · ", partes);
    }

    /// <summary>Un valor de un gráfico escrito como se lee: m:ss, entero o porcentaje.</summary>
    public static string Valor(double? valor, GraficoGaia g)
        => valor is not { } v ? "—" : g.Segundos ? Duracion(v) : g.Entero ? Formato.Entero((int)Math.Round(v)) : Formato.Porcentaje(v);

    /// <summary>Segundos → minutos para el eje («2,5 min»).</summary>
    public static string Minutos(double segundos) => (segundos / 60).ToString("0.#", Formato.Es) + " min";

    /// <summary>Eje de una gráfica: de <see cref="Min"/> a <see cref="Max"/> con marcas cada <see cref="Paso"/> (en la unidad de los valores).</summary>
    public sealed record EscalaGaia(double Min, double Max, double Paso)
    {
        /// <summary>Posición vertical (0 arriba, 1 abajo) de un valor.</summary>
        public double Y(double v) => 1 - (v - Min) / (Max - Min);
        public IEnumerable<double> Marcas() { for (var v = Min; v <= Max + Paso / 2; v += Paso) yield return v; }
    }

    /// <summary>La escala de unos valores: las columnas parten de cero; las líneas de porcentaje se ajustan a los datos.</summary>
    public static EscalaGaia Escala(GraficoGaia g, IEnumerable<double> valores, bool columnas)
    {
        var lista = valores.ToList();
        var max = lista.DefaultIfEmpty(0).Max();
        if (g.Segundos) { var (tope, paso) = EscalaGrafico.DesdeCero(max / 60, 4); return new(0, tope * 60, paso * 60); }
        if (g.Entero) { var (tope, paso) = EscalaGrafico.DesdeCero(max, 3); return new(0, tope, paso); }
        if (columnas)
        {
            var (tope, paso) = EscalaGrafico.DesdeCero(max * 100, 4);
            // Un porcentaje no pasa de 100: el eje se queda ahí aunque DesdeCero deje aire sobre la columna más alta.
            if (tope > 100 && max * 100 <= 100) tope = 100;
            return new(0, tope / 100, paso / 100);
        }
        var (min, mx, p) = EscalaGrafico.Porcentaje(lista);
        return new(min, mx, p);
    }

    /// <summary>Una marca del eje Y escrita según la unidad.</summary>
    public static string Eje(GraficoGaia g, double v, EscalaGaia e)
        => g.Segundos ? Minutos(v) : g.Entero ? Formato.Entero((int)Math.Round(v))
           : e.Paso * 100 < 1 ? (v * 100).ToString("0.0", Formato.Es) + Formato.EspacioFino + "%" : Formato.PorcentajeEntero(Math.Round(v * 100));

    private static double MaximoDe(SerieGaia s) => s.Valores.Where(v => v is not null).Select(v => v!.Value).DefaultIfEmpty(0).Max();

    /// <summary>Dos series con escalas muy distintas (una cinco veces mayor que la otra): eje izquierdo para la primera y derecho para la segunda.</summary>
    public static bool Doble(GraficoGaia g)
    {
        if (g.Segundos || g.Entero || g.Series.Count != 2) return false;
        var a = MaximoDe(g.Series[0]);
        var b = MaximoDe(g.Series[1]);
        return a > 0 && b > 0 && (a / b > 5 || b / a > 5);
    }

    /// <summary>
    /// En qué puntos va la pastilla con el valor: en todos si caben; si no, en los que caben sin pisarse (uno de cada dos o de
    /// cada tantos como haga falta) y siempre en el primero y el último. Cuenta con el sitio de verdad de cada pastilla sobre
    /// un plano de <see cref="GraficoGaia.AnchoEstimado"/> px: centrada en su punto, salvo la primera y la última, que se
    /// corren hacia dentro (.al-inicio / .al-final del CSS: el 84 % de la pastilla queda del lado del plano).
    /// </summary>
    public static HashSet<int> Rotulados(GraficoGaia g)
    {
        var n = g.Etiquetas.Count;
        var r = new HashSet<int>();
        if (n == 0) return r;
        double ancho = g.Segundos ? 46 : g.Entero ? 42 : 58;
        const double hueco = 4;
        const double haciaDentro = 0.84;
        double Centro(int i) => (i + 0.5) / n * g.AnchoEstimado;
        double Izquierda(int i) => Centro(i) - ancho * (i == 0 ? 1 - haciaDentro : i == n - 1 ? haciaDentro : 0.5);
        double Derecha(int i) => Centro(i) + ancho * (i == 0 ? haciaDentro : i == n - 1 ? 1 - haciaDentro : 0.5);

        // Con muchos días, como mucho uno de cada dos aunque quepan: si no, la gráfica es una fila de pastillas.
        var pasoMinimo = g.Dia && n > 20 ? 2 : 1;
        var previo = 0;
        r.Add(0);
        for (var i = 1; i < n - 1; i++)
        {
            if (i - previo < pasoMinimo || Izquierda(i) < Derecha(previo) + hueco) continue;
            r.Add(i);
            previo = i;
        }
        if (n > 1)
        {
            // La última siempre: si pisa la anterior, se quita la anterior (nunca la primera).
            while (previo > 0 && (n - 1 - previo < pasoMinimo || Izquierda(n - 1) < Derecha(previo) + hueco))
            {
                r.Remove(previo);
                previo = r.Max();
            }
            r.Add(n - 1);
        }
        return r;
    }

    /// <summary>
    /// Marca de la página según el filtro de marca: solo ORANGE → «orange»; solo YOIGO y/o MASMOVIL → «ygmm»; solo JAZZTEL →
    /// «jazztel»; nada marcado o marcas de grupos distintos → el color por defecto de GAIA (naranja).
    /// </summary>
    public static string MarcaDePagina(IEnumerable<string>? marcas, string porDefecto = "orange")
    {
        var grupos = (marcas ?? []).Select(m => m.ToUpperInvariant() switch
        {
            "ORANGE" => "orange", "JAZZTEL" => "jazztel", "YOIGO" or "MASMOVIL" => "ygmm", _ => "",
        }).Distinct().ToList();
        return grupos.Count == 1 && grupos[0] != "" ? grupos[0] : porDefecto;
    }

    private static GraficoGaia Construir(string escala, IReadOnlyList<(string Clave, string Texto, int Volumen)> puntos,
        IEnumerable<(string Nombre, string Clase, IReadOnlyList<double?> Valores)> series)
        => new(
            puntos.Select(p => escala switch { "dia" => DateOnly.TryParse(p.Clave, out var d) ? Formato.Fecha(d) : p.Texto, "etapa" => EtapaCorta(p.Texto), _ => p.Clave }).ToList(),
            puntos.Select(p => escala switch { "dia" => DiaLargo(p.Clave), "etapa" => EtapaLegible(p.Texto), _ => p.Texto.Replace(" · ", " del ") }).ToList(),
            puntos.Select(p => p.Volumen).ToList(),
            series.Select(s => new SerieGaia(s.Nombre, s.Clase, s.Valores)).ToList())
        {
            MaxEtiquetasX = escala == "semana" ? 7 : 12,
            Dia = escala == "dia",
            PorEtapa = escala == "etapa",
        };

    /// <summary>Gráfico de grupos (días, semanas o etapas) con una serie por indicador.</summary>
    public static GraficoGaia Grafico(string escala, IReadOnlyList<GrupoGaia> grupos, params (string Nombre, string Clase, Func<IndicadoresGaia, double?> Valor)[] series)
        => Construir(escala, grupos.Select(g => (g.Clave, g.Texto, g.Indicadores.Llamadas)).ToList(),
            series.Select(s => (s.Nombre, s.Clase, (IReadOnlyList<double?>)grupos.Select(g => s.Valor(g.Indicadores)).ToList())));

    /// <summary>Gráfico de españolización: una serie con España y otra con Colombia; el volumen es la base.</summary>
    public static GraficoGaia GraficoEspanolizacion(string escala, IReadOnlyList<GrupoEspanolizacion> grupos)
        => Construir(escala, grupos.Select(g => (g.Clave, g.Texto, g.Espanolizacion.Base)).ToList(),
            [
                ("España", "serie-espana", grupos.Select(g => g.Espanolizacion.Espana).ToList()),
                ("Colombia", "serie-colombia", grupos.Select(g => g.Espanolizacion.Colombia).ToList()),
            ]) with { NombreVolumen = "Base" };

    // ---------------------------------------------------------------------------------------
    // Selectores, dispersión y barras apiladas
    // ---------------------------------------------------------------------------------------

    public sealed record OpcionSelectorGaia(string Texto, string Url, bool Activa);

    /// <summary>Un desplegable de enlaces (el indicador de las gráficas, los ejes de la dispersión).</summary>
    public sealed record SelectorGaia(string Id, string Titulo, string Actual, IReadOnlyList<OpcionSelectorGaia> Opciones);

    public sealed record PuntoGaia(string Nombre, string Detalle, double X, double Y, int Llamadas);

    /// <summary>Los agentes enfrentados en dos indicadores; <paramref name="TotalX"/> y <paramref name="TotalY"/> son las líneas de referencia.</summary>
    public sealed record DispersionGaia(IReadOnlyList<PuntoGaia> Puntos, KpiGaia X, KpiGaia Y, double? TotalX, double? TotalY);

    /// <summary>Un tramo de una barra al 100 % apilada.</summary>
    public sealed record TramoGaia(string Nombre, string Clase, double Fraccion);

    /// <summary>Clase de tono de un sentimiento («Positivo», «Neutro», «Mixto», «Negativo»).</summary>
    public static string ClaseSentimiento(string texto) => texto switch
    {
        "Positivo" => "tono-bueno", "Negativo" => "tono-critico", "Mixto" => "tono-atencion", _ => "tono-neutro",
    };
}
