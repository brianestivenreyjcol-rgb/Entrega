using System.Globalization;
using System.Text;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>
/// Categoría de impedimento de una etiqueta que la tabla del 10/09 (<c>Datos/impedimentos_tabla.json</c>)
/// no conoce, por palabras clave. Las etiquetas las redacta la IA de DataOrb y salen nuevas cada semana:
/// medido el 05-10-2026, 596 de 1.638 no solucionadas de sinAccesoInternet (05-09 a 04-10) traían
/// alguna desconocida, y todas iban a «Otro impedimento».
/// </summary>
/// <remarks>
/// <para>
/// Reglas sacadas del proceso de soporte de YGMM (bóveda <c>C:\Proyectos\Soporte YGMM</c>): avería
/// masiva o red externa, envío de técnico, escalado a N2 o a otro departamento, cambio de equipo o
/// pedido logístico, plazo pendiente, y lo atribuible al agente (información contradictoria,
/// diagnóstico incompleto). Gana la <b>primera</b> regla que encaja, en este orden; si ninguna
/// encaja con claridad, la etiqueta sigue en «Otro impedimento». Es a propósito conservador: mejor
/// «Otro» que una categoría equivocada.
/// </para>
/// <para>Solo se usa con etiquetas que no están en la tabla: las conocidas mandan siempre.</para>
/// </remarks>
public static class ClasificadorEtiquetas
{
    /// <summary>
    /// (bit, palabras): basta con que el texto normalizado contenga una de ellas. El orden importa:
    /// las primeras reglas son excepciones de las siguientes (revisadas contra las etiquetas reales
    /// de 90 días, 05-10-2026). Las palabras con un espacio delante son palabras sueltas.
    /// </summary>
    public static readonly IReadOnlyList<(int Bit, string[] Palabras)> Reglas =
    [
        // Ir a una tienda no es una visita de técnico, ni un SMS o un servicio sin entregar es un equipo: «Otro».
        (512, [" tienda", "entrega de sms", "servicio no entregado"]),
        // Técnico, cuando lo que se pide es la disponibilidad del cliente para la visita.
        (1, ["para el tecnico"]),
        // Depende del cliente: no está, no puede, lo aplaza o tiene que aportar algo (antes que técnico y plazo).
        (64, ["cliente no en casa", "no esta fisicamente presente", "entrada del cliente", "disponibilidad del cliente",
              "cliente no disponible", "comparacion de ofertas", "rechazo la visita", " aplazo", "autoservicio del cliente"]),
        // Lo que el agente no puede hacer por permisos o herramientas es del proceso (escalado), no de su atención.
        (4, ["autoridad del agente", "capacidades de soporte del agente", "acceso del agente", "sistema del agente",
             "agente especializado"]),
        // Atención o seguimiento del agente: informó mal, no entendió, no terminó el diagnóstico o debe una llamada.
        (32, ["comprension del agente", "agente no proporciono", "incapacidad del agente", "seguimiento del agente",
              "pendiente del agente", "comunicacion del agente", "malentendido de la solicitud", "informacion contradictoria",
              "informacion incorrecta", "solucion de problemas incompleto", "falta de pasos para la solucion"]),
        // Avería masiva o red externa: nada que hacer en la llamada salvo informar.
        (2, ["masiva", "masivo", "red externa", "infraestructura", "fibra externa", "fibra externo", "cable externo", "averia externa",
             "reparacion tecnica externa", "tecnico externo", "servicio externo", "corte generalizado", "fallo generalizado",
             "incidencia de red", "de la central", "centralita", "outage", "external network"]),
        // Visita o envío de técnico.
        (1, ["visita", " cita", "technician"]),
        // Escalado a N2, a otro departamento o investigación.
        (4, ["escalar", "escalado", "escalada", "departamento especializado", "segundo nivel", "nivel 2", "investigacion",
             "investigar", "equipo especializado", "aprobacion externa", "se requiere soporte tecnico", "soporte avanzado",
             "escalat"]),
        // Equipo por cambiar: reemplazo, avería del equipo, entrega o logística (no «configurar», que es de N1).
        (16, ["reemplazo", "nuevo router", "envio de router", "despacho de equipo", "hardware defectuoso",
              "mal funcionamiento del router", "mal funcionamiento del equipo", "cambio de router", "cambio de equipo",
              "solicitud de router", "entrega", "logistic", "tarjeta sim"]),
        // Plazo o ticket pendiente (también la instalación de fibra sin terminar: la lleva Instalaciones).
        (8, [" plazo", "cronograma", "tiempo de resolucion", "tiempo estimado", "estimacion del tiempo", "retraso",
             "esperar", " pendiente", "largo tiempo", "pending", "instalacion"]),
        // Insatisfacción del cliente (también con experiencias o comunicaciones anteriores).
        (128, ["insatisfaccion", "frustracion", "falta de confianza", "churnrisk", "experiencias negativas",
               "comunicacion enganosa"]),
    ];

    /// <summary>El bit de la etiqueta, o <see cref="Ensamblador.BitOtro"/> si ninguna regla encaja.</summary>
    public static int Bit(string etiqueta)
    {
        var t = Normalizar(etiqueta);
        foreach (var (bit, palabras) in Reglas)
        {
            if (palabras.Any(p => t.Contains(p, StringComparison.Ordinal))) return bit;
        }
        return Ensamblador.BitOtro;
    }

    /// <summary>Minúsculas, sin tildes y con un espacio delante y detrás, para buscar palabras sueltas (« cita», «agent »).</summary>
    private static string Normalizar(string texto)
    {
        var sb = new StringBuilder(texto.Length + 2).Append(' ');
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.Append(' ').ToString();
    }
}
