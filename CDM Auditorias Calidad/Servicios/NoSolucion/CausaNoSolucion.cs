namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>
/// La causa de una no solución de sinAccesoInternet: si fue de la atención o del proceso.
/// Cada llamada no solucionada va a <b>una sola</b> causa (a diferencia de los impedimentos,
/// donde una llamada cuenta en cada categoría que trae).
/// </summary>
/// <remarks>
/// <para>
/// Reglas acordadas con el proceso de soporte de YGMM (bóveda <c>C:\Proyectos\Soporte YGMM</c>,
/// 05-10-2026):
/// </para>
/// <list type="bullet">
/// <item><b>Proceso</b>: la llamada trae un impedimento que no se puede cerrar en el primer contacto
/// —envío de técnico, avería masiva, escalado a N2, plazo o ticket pendiente, cambio de equipo—
/// y la atención fue correcta. Son los casos en los que el propio proceso exime de la pregunta de
/// resolución («Gestión en conversaciones»: incidencia abierta, envío de técnico, avería masiva,
/// transferencia).</item>
/// <item><b>Proceso, con fallos de atención</b>: trae un impedimento de proceso pero también una
/// señal de atención. Se separa porque el escalado o el técnico pueden ser evitables: en voz, el
/// 90 % de los escalados a N2 eran errores de N1 que se resolvían siguiendo Schaman («Escalados de
/// Voz»), y antes de enviar un técnico hay que intentar las pruebas («Envío de técnico»).</item>
/// <item><b>Atención</b>: ningún impedimento de proceso y alguna señal de atención: cierre abrupto
/// (la llamada se cortó o se cerró sin terminar), un impedimento atribuido al agente, o más ítems de
/// la rúbrica fallados que la mediana de las llamadas que sí se solucionaron.</item>
/// <item><b>Cliente</b>: sin proceso ni atención, depende del cliente (no puede o no quiere hacer
/// las pruebas: el cierre «Pruebas no finalizadas» de Schaman) o es insatisfacción con la solución.</item>
/// <item><b>Sin causa identificada</b>: el resto («Otro impedimento», «Problema declarado sin
/// resolver» o ninguno).</item>
/// </list>
/// </remarks>
public static class CausaNoSolucion
{
    /// <summary>Señal: la llamada terminó de forma abrupta (<c>tasks_CompletionCheck_rating = abruptlyEnded</c>).</summary>
    public const int SenalCierreAbrupto = 1;

    /// <summary>Señal: más ítems de la rúbrica fallados que la mediana de las solucionadas.</summary>
    public const int SenalRubrica = 2;

    /// <summary>Impedimentos «Depende del cliente» e «Insatisfacción del cliente».</summary>
    public const int MaskCliente = 64 | 128;

    public const string Proceso = "proceso";
    public const string ProcesoYAtencion = "proceso_y_atencion";
    public const string Atencion = "atencion";
    public const string Cliente = "cliente";
    public const string SinCausa = "sin_causa";

    /// <summary>Las causas, en el orden en que se enseñan.</summary>
    public static readonly IReadOnlyList<(string Clave, string Nombre, string Explicacion)> Todas =
    [
        (Proceso, "Proceso",
            "Trae un impedimento que no se cierra en la llamada (técnico, avería masiva, escalado a N2, plazo o cambio de equipo) y la atención fue correcta."),
        (ProcesoYAtencion, "Proceso, con fallos de atención",
            "Trae un impedimento de proceso, pero también cierre abrupto, impedimento del agente o rúbrica peor de lo normal: revisar si el escalado o el técnico eran evitables."),
        (Atencion, "Atención",
            "Ningún impedimento de proceso: cierre abrupto, impedimento atribuido al agente o rúbrica peor que la mediana de las solucionadas."),
        (Cliente, "Cliente",
            "Depende del cliente (no puede o no quiere hacer las pruebas) o no está conforme con la solución, sin fallos de atención."),
        (SinCausa, "Sin causa identificada",
            "Solo «Otro impedimento», «Problema declarado sin resolver» o ningún impedimento."),
    ];

    public static string Nombre(string clave) => Todas.First(c => c.Clave == clave).Nombre;

    /// <summary>Si la llamada tiene alguna señal de atención (las mismas que el cuadrante «atención»).</summary>
    public static bool HayAtencion(int mascara, int senales)
        => (senales & (SenalCierreAbrupto | SenalRubrica)) != 0 || (mascara & Ensamblador.MaskAten) != 0;

    /// <summary>Si la llamada trae algún impedimento de proceso.</summary>
    public static bool HayProceso(int mascara) => (mascara & Ensamblador.MaskProc) != 0;

    /// <summary>La causa de una llamada a partir de sus impedimentos y sus señales de atención.</summary>
    public static string Clave(int mascara, int senales)
    {
        var proceso = HayProceso(mascara);
        var atencion = HayAtencion(mascara, senales);
        if (proceso) return atencion ? ProcesoYAtencion : Proceso;
        if (atencion) return Atencion;
        return (mascara & MaskCliente) != 0 ? Cliente : SinCausa;
    }

    /// <summary>Las señales de atención de una llamada, en palabras (para el CSV).</summary>
    public static List<string> Senales(int mascara, int senales, int fallos, int umbral)
    {
        var salida = new List<string>();
        if ((senales & SenalCierreAbrupto) != 0) salida.Add("cierre abrupto");
        if ((mascara & Ensamblador.MaskAten) != 0) salida.Add("impedimento atribuido al agente");
        if ((senales & SenalRubrica) != 0) salida.Add($"rúbrica: {fallos} de {Ensamblador.ItemsRubrica} ítems fallados (lo normal, {umbral})");
        return salida;
    }
}
