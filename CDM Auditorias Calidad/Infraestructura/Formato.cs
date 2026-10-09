using System.Globalization;

namespace CDM_Auditorias_Calidad.Infraestructura;

/// <summary>
/// Formatos de número y fecha (cultura es-ES), según la guía de estilos: porcentajes con dos
/// decimales y el signo separado por un espacio fino («41,58 %»).
/// </summary>
public static class Formato
{
    public static readonly CultureInfo Es = CultureInfo.GetCultureInfo("es-ES");

    /// <summary>Espacio fino que no se parte: entre la cifra y «%» o «pp».</summary>
    public const string EspacioFino = " ";

    /// <summary><c>#,##0</c> → 23.419</summary>
    public static string Entero(int valor) => valor.ToString("#,##0", Es);

    /// <summary>Una fracción como porcentaje con dos decimales → 50,20 %. Vacío → «—».</summary>
    public static string Porcentaje(double? fraccion)
        => fraccion is { } f ? (f * 100).ToString("#,##0.00", Es) + EspacioFino + "%" : "—";

    /// <summary>Un porcentaje que ya viene multiplicado por 100 y sin decimales → 50 %.</summary>
    public static string PorcentajeEntero(double porcentaje) => porcentaje.ToString("0", Es) + EspacioFino + "%";

    /// <summary>Dos decimales → 2,20</summary>
    public static string Decimal2(double valor) => valor.ToString("#,##0.00", Es);

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", Es);

    public static string FechaCorta(DateOnly fecha) => fecha.ToString("dd/MM", Es);

    public static string FechaHora(DateTime fecha) => fecha.ToString("dd/MM/yyyy HH:mm", Es);

    /// <summary>«septiembre 2026» → «Septiembre 2026».</summary>
    public static string MesLargo(int año, int mes) => Mayuscula(new DateOnly(año, mes, 1).ToString("MMMM yyyy", Es));

    /// <summary>Columna <c>Calendario[Mes]</c> (<c>FORMAT(fecha, "MMM")</c>): «sept», «oct».</summary>
    public static string MesCorto(int año, int mes) => new DateOnly(año, mes, 1).ToString("MMM", Es).TrimEnd('.');

    public static string Mayuscula(string texto) => texto.Length == 0 ? texto : char.ToUpper(texto[0], Es) + texto[1..];

    /// <summary>Coordenadas de SVG, porcentajes de CSS y atributos <c>data-valor</c>: siempre con punto decimal.</summary>
    public static string Coord(double valor) => Math.Round(valor, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Valor crudo para <c>data-valor</c> (ordenar tablas, contar cifras).</summary>
    public static string Crudo(double? valor) => valor is { } v ? v.ToString("0.######", CultureInfo.InvariantCulture) : "";
}
