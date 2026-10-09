using System.Text;

namespace CDM_Auditorias_Calidad.Servicios.Comun;

/// <summary>Una columna de un CSV: su título y el texto de cada fila.</summary>
public sealed record ColumnaCsv<T>(string Titulo, Func<T, string?> Valor);

/// <summary>
/// Ficheros CSV para descargar, con el formato de la aplicación anterior
/// (<c>exportToCSV</c> de frontend/src/utils/export.js): separador <c>;</c>,
/// cabecera sin comillas, cada valor entre comillas, BOM UTF-8 y saltos de
/// línea CRLF, para que Excel lo abra bien a la primera.
/// </summary>
public static class ExportacionCsv
{
    public const string TipoContenido = "text/csv; charset=utf-8";

    /// <summary>El texto del CSV (sin el BOM).</summary>
    public static string Texto(IEnumerable<string> cabeceras, IEnumerable<IEnumerable<string?>> filas)
    {
        static string Celda(string? valor) => "\"" + (valor ?? "").Replace("\"", "\"\"") + "\"";

        var lineas = new List<string> { string.Join(";", cabeceras) };
        lineas.AddRange(filas.Select(f => string.Join(";", f.Select(Celda))));
        return string.Join("\r\n", lineas);
    }

    /// <summary>El fichero listo para descargar: el texto con el BOM delante.</summary>
    public static byte[] Bytes(IEnumerable<string> cabeceras, IEnumerable<IEnumerable<string?>> filas)
        => Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Texto(cabeceras, filas))).ToArray();

    /// <summary>Un CSV con una fila por elemento y las columnas dadas.</summary>
    public static byte[] Generar<T>(IEnumerable<T> filas, IReadOnlyList<ColumnaCsv<T>> columnas)
        => Bytes(columnas.Select(c => c.Titulo), filas.Select(f => columnas.Select(c => c.Valor(f))));
}
