using Microsoft.AspNetCore.Html;

namespace CDM_Auditorias_Calidad.Infraestructura;

/// <summary>
/// Iconos SVG de trazo (24 × 24) que toman el color del texto. Sustituyen a las imágenes PNG
/// y a los emojis del Power BI: la guía de estilos no admite emojis ni iconos de colores.
/// </summary>
public static class Iconos
{
    private static readonly Dictionary<string, string> Trazos = new()
    {
        ["portapapeles"] = """<rect x="8" y="2" width="8" height="4" rx="1"/><path d="M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2"/><path d="m9 14 2 2 4-4"/>""",
        ["filtro"] = """<path d="M22 3H2l8 9.46V19l4 2v-8.54L22 3z"/>""",
        ["borrar-filtros"] = """<path d="M13.01 3H2l8 9.46V19l4 2v-8.54l.9-1.06"/><path d="m22 3-5 5"/><path d="m17 3 5 5"/>""",
        ["mas-filtros"] = """<path d="M21 4h-7"/><path d="M10 4H3"/><path d="M21 12h-9"/><path d="M8 12H3"/><path d="M21 20h-5"/><path d="M12 20H3"/><path d="M14 2v4"/><path d="M8 10v4"/><path d="M16 18v4"/>""",
        ["refrescar"] = """<path d="M3 12a9 9 0 0 1 9-9 9.75 9.75 0 0 1 6.74 2.74L21 8"/><path d="M21 3v5h-5"/><path d="M21 12a9 9 0 0 1-9 9 9.75 9.75 0 0 1-6.74-2.74L3 16"/><path d="M8 16H3v5"/>""",
        ["hoja"] = """<path d="M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z"/><path d="M14 2v4a2 2 0 0 0 2 2h4"/><path d="M8 13h2"/><path d="M14 13h2"/><path d="M8 17h2"/><path d="M14 17h2"/>""",
        ["columnas"] = """<path d="M3 3v16a2 2 0 0 0 2 2h16"/><path d="M18 17V9"/><path d="M13 17V5"/><path d="M8 17v-3"/>""",
        ["birrete"] = """<path d="M21.42 10.92a1 1 0 0 0-.02-1.84L12.83 5.18a2 2 0 0 0-1.66 0L2.6 9.08a1 1 0 0 0 0 1.83l8.57 3.91a2 2 0 0 0 1.66 0z"/><path d="M22 10v6"/><path d="M6 12.5V16a6 3 0 0 0 12 0v-3.5"/>""",
        ["escudo"] = """<path d="M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z"/><path d="m9 12 2 2 4-4"/>""",
        ["diana"] = """<circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/>""",
        ["personas"] = """<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>""",
        ["calendario"] = """<rect width="18" height="18" x="3" y="4" rx="2"/><path d="M16 2v4"/><path d="M8 2v4"/><path d="M3 10h18"/>""",
        ["calendario-mes"] = """<rect width="18" height="18" x="3" y="4" rx="2"/><path d="M16 2v4"/><path d="M8 2v4"/><path d="M3 10h18"/><path d="M8 14h.01"/><path d="M12 14h.01"/><path d="M16 14h.01"/><path d="M8 18h.01"/><path d="M12 18h.01"/><path d="M16 18h.01"/>""",
        ["estrella"] = """<path d="M12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26z"/>""",
        ["sube"] = """<path d="m22 7-8.5 8.5-5-5L2 17"/><path d="M16 7h6v6"/>""",
        ["baja"] = """<path d="m22 17-8.5-8.5-5 5L2 7"/><path d="M16 17h6v-6"/>""",
        ["igual"] = """<path d="M5 12h14"/>""",
        ["derecha"] = """<path d="m9 18 6-6-6-6"/>""",
        ["abajo"] = """<path d="m6 9 6 6 6-6"/>""",
        ["alerta"] = """<circle cx="12" cy="12" r="10"/><path d="M12 8v4"/><path d="M12 16h.01"/>""",
        ["reloj"] = """<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>""",
        ["sol"] = """<circle cx="12" cy="12" r="4"/><path d="M12 2v2"/><path d="M12 20v2"/><path d="m4.93 4.93 1.41 1.41"/><path d="m17.66 17.66 1.41 1.41"/><path d="M2 12h2"/><path d="M20 12h2"/><path d="m6.34 17.66-1.41 1.41"/><path d="m19.07 4.93-1.41 1.41"/>""",
        ["luna"] = """<path d="M12 3a6 6 0 0 0 9 9 9 9 0 1 1-9-9Z"/>""",
        ["tema-auto"] = """<circle cx="12" cy="12" r="10"/><path d="M12 18a6 6 0 0 0 0-12v12z"/>""",
        ["telefono"] = """<path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72 12.84 12.84 0 0 0 .7 2.81 2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45 12.84 12.84 0 0 0 2.81.7A2 2 0 0 1 22 16.92z"/>""",
        ["cruce"] = """<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>""",
        ["no-resuelta"] = """<circle cx="12" cy="12" r="10"/><path d="m15 9-6 6"/><path d="m9 9 6 6"/>""",
        ["copiar"] = """<rect width="14" height="14" x="8" y="8" rx="2"/><path d="M4 16c-1.1 0-2-.9-2-2V4c0-1.1.9-2 2-2h10c1.1 0 2 .9 2 2"/>""",
        ["transferir"] = """<path d="M8 3 4 7l4 4"/><path d="M4 7h16"/><path d="m16 21 4-4-4-4"/><path d="M20 17H4"/>""",
        ["baja-cliente"] = """<path d="M2 21a8 8 0 0 1 13.292-6"/><circle cx="10" cy="8" r="5"/><path d="M22 19h-6"/>""",
        ["etiqueta"] = """<path d="M12.586 2.586A2 2 0 0 0 11.172 2H4a2 2 0 0 0-2 2v7.172a2 2 0 0 0 .586 1.414l8.704 8.704a2.426 2.426 0 0 0 3.42 0l6.58-6.58a2.426 2.426 0 0 0 0-3.42z"/><circle cx="7.5" cy="7.5" r=".5"/>""",
        ["carrito"] = """<circle cx="8" cy="21" r="1"/><circle cx="19" cy="21" r="1"/><path d="M2.05 2.05h2l2.66 12.42a2 2 0 0 0 2 1.58h9.78a2 2 0 0 0 1.95-1.57l1.65-7.43H5.12"/>""",
        ["buscar"] ="""<circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>""",
        ["imprimir"] = """<path d="M6 9V2h12v7"/><path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2"/><rect x="6" y="14" width="12" height="8"/>""",
        ["presentar"] = """<rect x="2" y="3" width="20" height="14" rx="2"/><path d="M8 21h8"/><path d="M12 17v4"/>""",
        ["izquierda"] = """<path d="m15 18-6-6 6-6"/>""",
        ["flecha"] = """<path d="M5 12h14"/><path d="m12 5 7 7-7 7"/>""",
        ["cerrar"] = """<path d="M18 6 6 18"/><path d="m6 6 12 12"/>""",
    };

    /// <param name="clase">Clases CSS extra para el <c>&lt;svg&gt;</c>.</param>
    public static IHtmlContent Svg(string nombre, string? clase = null)
    {
        var trazo = Trazos.TryGetValue(nombre, out var t) ? t : "";
        var c = clase is null ? "icono" : "icono " + clase;
        return new HtmlString(
            $"<svg class=\"{c}\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"2\" " +
            $"stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\" focusable=\"false\">{trazo}</svg>");
    }

    /// <summary>
    /// La flecha de tendencia de una variación: ▲ si «sube», ▼ si «baja» y un guion si es «igual» (mismos nombres que
    /// los iconos de tendencia). Es texto, no emoji; el color lo pone la clase de su contenedor (.sube, .baja, .mejor, .peor).
    /// </summary>
    public static IHtmlContent Tendencia(string nombre) => new HtmlString(
        "<span class=\"tend-flecha\" aria-hidden=\"true\">" + (nombre == "sube" ? "▲" : nombre == "baja" ? "▼" : "–") + "</span>");

    /// <summary>
    /// El logotipo de la web: portapapeles con visto bueno sobre un círculo. Lleva el acento
    /// (lo único con color de marca en la portada, como dice la guía).
    /// </summary>
    public static IHtmlContent Logotipo() => new HtmlString(
        "<svg class=\"logotipo\" viewBox=\"0 0 40 40\" aria-hidden=\"true\" focusable=\"false\">" +
        "<circle class=\"logotipo-fondo\" cx=\"20\" cy=\"20\" r=\"20\"/>" +
        "<g fill=\"none\" stroke-width=\"2\" stroke-linecap=\"round\" stroke-linejoin=\"round\" transform=\"translate(8 8)\">" +
        "<rect class=\"logotipo-trazo\" x=\"8\" y=\"2\" width=\"8\" height=\"4\" rx=\"1\"/>" +
        "<path class=\"logotipo-trazo\" d=\"M16 4h2a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2h2\"/>" +
        "<path class=\"logotipo-visto\" d=\"m8.5 14 2.5 2.5 4.5-5\" stroke-width=\"2.6\"/></g></svg>");
}
