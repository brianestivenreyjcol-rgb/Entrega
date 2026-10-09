namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>Los criterios del estilo y la adherencia (página «Estilo» del PBI). Fracciones de 0 a 1.</summary>
public sealed record EstiloGaia(
    double? Adherencia, double? Saludo, double? SoyClaro, double? Escucho, double? BuscoInformacion,
    double? Soluciono, double? Resumo, double? ConfirmoSolucion, double? Despedida);

/// <summary>Todas las medidas del PBI para un grupo de llamadas. Porcentajes como fracción (0–1); nulo si no hay base.</summary>
public sealed record IndicadoresGaia
{
    public int Llamadas { get; init; }
    public double? Rellamada72 { get; init; }
    public double? Rellamada24 { get; init; }
    public double? NoSolucion { get; init; }
    public double? Transferencia { get; init; }
    public double? Churn { get; init; }
    public double? Ofrecimientos { get; init; }
    public double? Reactivo { get; init; }
    public double? Proactivo { get; init; }
    public int OfrecimientosReactivos { get; init; }
    public int OfrecimientosProactivos { get; init; }
    public int OfrecimientosAlineados { get; init; }
    public int OfrecimientosNoAlineados { get; init; }
    public int Ventas { get; init; }
    public double? PorcentajeVentas { get; init; }
    public double? SentimientoPositivoFinal { get; init; }
    public double? SentimientoNegativoFinal { get; init; }
    /// <summary>Duración media en segundos.</summary>
    public double? Tmo { get; init; }
    /// <summary>Silencio medio por llamada, en segundos («Tiempos en silencio» del portal).</summary>
    public double? SilencioMedio { get; init; }
    /// <summary>Llamadas con rellamada en 72 h y llamadas con el dato (la base del porcentaje).</summary>
    public int Rellamadas { get; init; }
    public int RellamadaBase { get; init; }
    public double? Menores60 { get; init; }
    public double? Entre60y180 { get; init; }
    public double? SinContexto { get; init; }
    public double? Resuelto { get; init; }
    public double? Pendiente { get; init; }
    public double? NoSePuedeCumplir { get; init; }
    public double? ProblemasNoResueltos { get; init; }
    /// <summary>Encuestas enviadas sobre llamadas de YOIGO y MASMOVIL (las únicas con encuesta en el IVR).</summary>
    public double? EnvioEncuestas { get; init; }
    public int RespuestasEncuesta { get; init; }
    public required EstiloGaia Estilo { get; init; }

    public double? Adherencia => Estilo.Adherencia;
}

/// <summary>Un KPI que se puede elegir en los desplegables (los «parámetros de campo» del PBI).</summary>
/// <param name="Clave">Valor en la URL.</param>
/// <param name="MejorAlto">Si un valor más alto es mejor (para ordenar y colorear).</param>
public sealed record KpiGaia(string Clave, string Nombre, Func<IndicadoresGaia, double?> Valor, bool MejorAlto, string Ayuda);

/// <summary>Una fila por agente (página «Ranking»).</summary>
public sealed record FilaAgenteGaia(
    string IdAgente, string Agente, string Sector, string Oleada, string Formador, string Supervisor,
    IndicadoresGaia Indicadores);

/// <summary>Un grupo con sus indicadores: un día, una etapa de formación, un motivo…</summary>
public sealed record GrupoGaia(string Clave, string Texto, IndicadoresGaia Indicadores);

/// <summary>
/// Las medidas DAX de la tabla <c>Medidas</c> del PBI pasadas a C#. Cambios a propósito (pedidos
/// por el usuario el 06-10-2026):
/// <list type="bullet">
/// <item>% Transferencia divide entre las llamadas con Transferencia 0 o 1 (el PBI usaba Rellamada72hr = 0).</item>
/// <item>% Rellamada 24 h = rellamada 72 h oficial cuya siguiente llamada llega en ≤ 24 h, sobre la
/// misma base que la de 72 h (el PBI la calculaba con las llamadas descargadas).</item>
/// <item>No hay «Abruptas» ni «Llamadas cortadas»: el dato no existe en DataOrb (el PBI ponía 0 fijo).</item>
/// </list>
/// </summary>
public static partial class CalculadoraGaia
{
    /// <summary>Pesos de la adherencia (medida <c>% Adherencia</c>).</summary>
    public const double PesoSaludo = 0.10, PesoSoyClaro = 0.15, PesoSoluciono = 0.25,
        PesoResumo = 0.20, PesoConfirmo = 0.20, PesoDespedida = 0.10;

    public static readonly IReadOnlyList<KpiGaia> Kpis =
    [
        new("adherencia", "Adherencia", i => i.Adherencia, true, "Saludo 10 %, claro y fiable 15 %, solucionó 25 %, resumió 20 %, confirmó la solución 20 % y despedida 10 %."),
        new("rellamada", "Rellamada 72 h", i => i.Rellamada72, false, "Llamadas tras las que el cliente volvió a llamar en 72 h (dato corporativo)."),
        new("rellamada24", "Rellamada 24 h", i => i.Rellamada24, false, "Rellamadas de 72 h en las que la siguiente llamada llegó en menos de 24 h."),
        new("nosolucion", "No solución", i => i.NoSolucion, false, "Encuestas «no resuelto» sobre encuestas respondidas."),
        new("transferencia", "Transferencia", i => i.Transferencia, false, "Llamadas transferidas sobre las que tienen el dato."),
        new("churn", "Churn", i => i.Churn, false, "Llamadas con riesgo de baja alto."),
        new("ofrecimientos", "Ofrecimientos", i => i.Ofrecimientos, true, "Llamadas con intento de venta."),
        new("ventas", "Ventas", i => i.PorcentajeVentas, true, "Llamadas con venta."),
        new("positivo", "Sentimiento positivo final", i => i.SentimientoPositivoFinal, true, "Clientes que acaban la llamada con sentimiento positivo."),
        new("negativo", "Sentimiento negativo final", i => i.SentimientoNegativoFinal, false, "Clientes que acaban la llamada con sentimiento negativo."),
        new("encuestas", "Envío de encuestas", i => i.EnvioEncuestas, true, "Encuestas enviadas sobre llamadas de YOIGO y MASMOVIL."),
    ];

    public static KpiGaia Kpi(string? clave)
        => Kpis.FirstOrDefault(k => string.Equals(k.Clave, clave, StringComparison.OrdinalIgnoreCase)) ?? Kpis[0];

    public static IndicadoresGaia Calcular(IReadOnlyCollection<LlamadaGaia> ll)
    {
        int n = ll.Count;
        int r72 = 0, r72Base = 0, r24 = 0, ns = 0, nsBase = 0, tr = 0, trBase = 0, churn = 0, churnBase = 0;
        int intento = 0, reactivo = 0, proactivo = 0, alineados = 0, noAlineados = 0, ventas = 0;
        int sfPos = 0, sfNeg = 0, sfBase = 0, menores60 = 0, entre = 0, sinContexto = 0;
        int resuelto = 0, pendiente = 0, noPuede = 0, noResueltos = 0, ygmm = 0, enviadas = 0;
        int durN = 0, silN = 0; double durSuma = 0, silSuma = 0;

        foreach (var l in ll)
        {
            if (l.Rellamada72h is 0 or 1)
            {
                r72Base++;
                if (l.Rellamada72h == 1)
                {
                    r72++;
                    if (l.MinutosSiguienteLlamada is { } m && m <= 24 * 60) r24++;
                }
            }
            if (l.EncuestaSolucion is 1 or 2) { nsBase++; if (l.EncuestaSolucion == 2) ns++; }
            if (l.Transferencia is 0 or 1) { trBase++; if (l.Transferencia == 1) tr++; }
            if (l.RiesgoChurn.Length > 0) { churnBase++; if (l.RiesgoChurn == "Alto") churn++; }

            if (l.IntentoVenta == true)
            {
                intento++;
                if (l.PosibleVentaEntrante == true) reactivo++;
                else if (l.PosibleVentaEntrante == false) proactivo++;
                if (l.AlineacionOferta.Equals("true", StringComparison.OrdinalIgnoreCase)) alineados++;
                else if (l.AlineacionOferta.Equals("false", StringComparison.OrdinalIgnoreCase)) noAlineados++;
            }
            if (l.TieneVenta == true) ventas++;

            if (l.SentimientoFinal.Length > 0)
            {
                sfBase++;
                if (l.SentimientoFinal == "Positivo") sfPos++;
                else if (l.SentimientoFinal == "Negativo") sfNeg++;
            }
            if (l.DuracionSegundos is { } d)
            {
                durN++; durSuma += d;
                if (l.TiempoNoHablado is { } sil) { silN++; silSuma += sil; }
                if (d < 60) menores60++;
                else if (d <= 180) entre++;
            }
            if (l.Contexto == "Insuficiente") sinContexto++;

            switch (l.EstadoResolucion)
            {
                case "Resuelto": resuelto++; break;
                case "Pendiente": pendiente++; break;
                case "No se puede cumplir": noPuede++; break;
            }
            if (l.ProblemaResuelto == false) noResueltos++;
            if (l.Marca is "YOIGO" or "MASMOVIL") { ygmm++; if (l.EncuestaEnviada) enviadas++; }
        }

        return new IndicadoresGaia
        {
            Llamadas = n,
            Rellamada72 = Div(r72, r72Base),
            Rellamada24 = Div(r24, r72Base),
            NoSolucion = Div(ns, nsBase),
            Transferencia = Div(tr, trBase),
            Churn = Div(churn, churnBase),
            Ofrecimientos = Div(intento, n),
            Reactivo = Div(reactivo, n),
            Proactivo = Div(proactivo, n),
            OfrecimientosReactivos = reactivo,
            OfrecimientosProactivos = proactivo,
            OfrecimientosAlineados = alineados,
            OfrecimientosNoAlineados = noAlineados,
            Ventas = ventas,
            PorcentajeVentas = Div(ventas, n),
            SentimientoPositivoFinal = Div(sfPos, sfBase),
            SentimientoNegativoFinal = Div(sfNeg, sfBase),
            Tmo = durN > 0 ? durSuma / durN : null,
            SilencioMedio = silN > 0 ? silSuma / silN : null,
            Rellamadas = r72,
            RellamadaBase = r72Base,
            Menores60 = Div(menores60, n),
            Entre60y180 = Div(entre, n),
            SinContexto = Div(sinContexto, n),
            Resuelto = Div(resuelto, n),
            Pendiente = Div(pendiente, n),
            NoSePuedeCumplir = Div(noPuede, n),
            ProblemasNoResueltos = Div(noResueltos, n),
            EnvioEncuestas = Div(enviadas, ygmm),
            RespuestasEncuesta = nsBase,
            Estilo = Estilo(ll),
        };
    }

    /// <summary>
    /// Los criterios del estilo. Cada uno es «sí» sobre «sí + no» de las llamadas que tienen la
    /// calificación; la adherencia es la suma ponderada (un criterio sin base cuenta 0, como el
    /// BLANK de DAX en una suma) y es nula solo si ninguno tiene base.
    /// </summary>
    public static EstiloGaia Estilo(IEnumerable<LlamadaGaia> ll)
    {
        var saludo = new Contador(); var claro = new Contador(); var escucho = new Contador(); var busco = new Contador();
        var soluciono = new Contador(); var resumo = new Contador(); var confirmo = new Contador(); var despedida = new Contador();

        foreach (var l in ll)
        {
            saludo.Sumar(Si(l.CalificacionSaludo));
            claro.Sumar(Si(l.CalificacionLenguajeClaro));
            soluciono.Sumar(Si(l.CalificacionSolucion));
            resumo.Sumar(Si(l.CalificacionResumen));
            confirmo.Sumar(Si(l.CalificacionConfirmacion));
            despedida.Sumar(Si(l.CalificacionCierre));
            busco.Sumar(BuscoInformacion(l));
            // «Escuchó»: N/A cuenta como acierto (medida % Escucho).
            var e = Escucho(l);
            if (e is not null) escucho.Sumar(e == "0" ? 0 : 1);
        }

        double? adherencia = null;
        if (saludo.Base + claro.Base + soluciono.Base + resumo.Base + confirmo.Base + despedida.Base > 0)
        {
            adherencia = (saludo.Valor ?? 0) * PesoSaludo + (claro.Valor ?? 0) * PesoSoyClaro
                + (soluciono.Valor ?? 0) * PesoSoluciono + (resumo.Valor ?? 0) * PesoResumo
                + (confirmo.Valor ?? 0) * PesoConfirmo + (despedida.Valor ?? 0) * PesoDespedida;
        }
        return new EstiloGaia(adherencia, saludo.Valor, claro.Valor, escucho.Valor, busco.Valor,
            soluciono.Valor, resumo.Valor, confirmo.Valor, despedida.Valor);
    }

    /// <summary>Una calificación de DataOrb: vacía → sin dato; «yes» → 1; cualquier otra → 0.</summary>
    public static int? Si(string calificacion)
        => calificacion.Length == 0 ? null : calificacion.Equals("yes", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

    /// <summary>
    /// Columna <c>conteo Busco Informacion</c>: 0 si el silencio pasa de la mitad de la llamada.
    /// Sin duración o sin silencio, sin dato.
    /// </summary>
    public static int? BuscoInformacion(LlamadaGaia l)
        => l.DuracionSegundos is not { } d || l.TiempoNoHablado is not { } s || d == 0 ? null : s / d > 0.5 ? 0 : 1;

    /// <summary>
    /// Columna <c>conteo Escucho</c>: «1», «0» o «N/A». Habla &lt; 80 %, se pisaron &lt; 10 veces y
    /// reconoció al cliente; lo que no tiene dato es N/A y no penaliza.
    /// </summary>
    public static string? Escucho(LlamadaGaia l)
    {
        var tr = l.PorcentajeHabla is not { } p ? "N/A" : p < 80 ? "1" : "0";
        var otc = l.VecesSePisaron is not { } v ? "N/A" : v < 10 ? "1" : "0";
        var r = l.CalificacionReconocimiento;
        string acksta;
        if (r.Equals("yes", StringComparison.OrdinalIgnoreCase)) acksta = "1";
        else if (r.Equals("no", StringComparison.OrdinalIgnoreCase) || r.Equals("notApplicable", StringComparison.OrdinalIgnoreCase)
                 || r.Equals("NA", StringComparison.OrdinalIgnoreCase)) acksta = "0";
        else acksta = "N/A";

        if (tr == "N/A" && otc == "N/A" && acksta == "N/A") return "N/A";
        return tr == "0" || otc == "0" || acksta == "0" ? "0" : "1";
    }

    /// <summary>Una fila por agente con sus indicadores.</summary>
    public static List<FilaAgenteGaia> PorAgente(IEnumerable<LlamadaGaia> ll)
        => ll.GroupBy(l => l.IdAgente)
             .Select(g =>
             {
                 var p = g.First();
                 return new FilaAgenteGaia(g.Key, p.Agente, p.Sector, p.Oleada, p.Formador, p.Supervisor, Calcular(g.ToList()));
             })
             .ToList();

    /// <summary>Agrupa por lo que diga <paramref name="clave"/> (vacío → «Sin dato»), ordenado por la clave.</summary>
    public static List<GrupoGaia> Agrupar(IEnumerable<LlamadaGaia> ll, Func<LlamadaGaia, string> clave, Func<string, string>? texto = null)
        => ll.GroupBy(l => clave(l) is { Length: > 0 } c ? c : "Sin dato")
             .OrderBy(g => g.Key, StringComparer.Ordinal)
             .Select(g => new GrupoGaia(g.Key, texto?.Invoke(g.Key) ?? g.Key, Calcular(g.ToList())))
             .ToList();

    /// <summary>Por día, en orden.</summary>
    public static List<GrupoGaia> PorDia(IEnumerable<LlamadaGaia> ll)
        => Agrupar(ll, l => l.Fecha.ToString("yyyy-MM-dd"), c => DateOnly.TryParse(c, out var d) ? d.ToString("dd/MM") : c);

    /// <summary>Por etapa de formación, en el orden en que se hacen (1 Preconexion → Aseguramiento 6).</summary>
    public static List<GrupoGaia> PorEtapa(IEnumerable<LlamadaGaia> ll)
        => Agrupar(ll, l => l.TipoConexion)
             .OrderBy(g => Array.IndexOf(LectorNominaGaia.ColumnasDias, g.Clave) is var i && i >= 0 ? i : 99)
             .ToList();

    private static double? Div(int a, int b) => b == 0 ? null : (double)a / b;

    /// <summary>Aciertos y base de un criterio.</summary>
    private sealed class Contador
    {
        public int Aciertos;
        public int Base;
        public void Sumar(int? v) { if (v is null) return; Base++; Aciertos += v.Value; }
        public double? Valor => Base == 0 ? null : (double)Aciertos / Base;
    }
}
