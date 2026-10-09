using System.Globalization;
using ClosedXML.Excel;

namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>Un agente en formación, como viene en la hoja Oleadas del Excel.</summary>
public sealed class AgenteGaia
{
    public required string Id { get; init; }
    public string Nombre { get; init; } = "";
    public string Sector { get; init; } = "";
    public string Supervisor { get; init; } = "";
    public string Coordinador { get; init; } = "";
    public string Formador { get; init; } = "";
    public string Cargo { get; init; } = "";
    public string Oleada { get; init; } = "";

    /// <summary>Sus días de formación y qué es cada uno («1 Preconexion», «Aseguramiento 2»…).</summary>
    public SortedDictionary<DateOnly, string> Dias { get; init; } = new();
}

/// <summary>Lo leído del Excel: los agentes con ID y los avisos (filas sin ID, ID repetidos…).</summary>
public sealed class NominaGaia
{
    public List<AgenteGaia> Agentes { get; init; } = new();
    public List<string> Avisos { get; init; } = new();

    /// <summary>Fecha de modificación del Excel cuando se leyó: si cambia, hay que volver a traer.</summary>
    public DateTime ExcelModificado { get; init; }

    public int Pares => Agentes.Sum(a => a.Dias.Count);
}

/// <summary>
/// Lee la hoja Oleadas del Excel de nómina de GAIA. Hace lo que hacían el <c>Generador.py</c> de la
/// carpeta compartida y la tabla <c>Nomina</c> del PBI, pero guarda cada agente con SUS días (el PBI
/// cruzaba todas las fechas con todos los agentes).
/// </summary>
public static class LectorNominaGaia
{
    /// <summary>Columnas con los días de formación, en el orden en que se hacen.</summary>
    public static readonly string[] ColumnasDias =
    [
        "1 Preconexion", "2 Preconexion", "3 Preconexion",
        "Aseguramiento 1", "Aseguramiento 2", "Aseguramiento 3",
        "Aseguramiento 4", "Aseguramiento 5", "Aseguramiento 6",
    ];

    /// <summary>
    /// Abre el Excel en solo lectura y compartido (se puede leer aunque alguien lo tenga abierto)
    /// y lee solo <paramref name="hoja"/>.
    /// </summary>
    public static NominaGaia Leer(string ruta, string hoja)
    {
        if (!File.Exists(ruta)) throw new FileNotFoundException($"No se encuentra el Excel de nómina: {ruta}", ruta);
        var modificado = File.GetLastWriteTime(ruta);
        using var flujo = new FileStream(ruta, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var libro = new XLWorkbook(flujo);
        if (!libro.TryGetWorksheet(hoja, out var hojaExcel))
        {
            throw new InvalidDataException($"El Excel de nómina no tiene la hoja «{hoja}».");
        }
        return Leer(hojaExcel, modificado);
    }

    /// <summary>Lee una hoja ya abierta (separado para las pruebas).</summary>
    public static NominaGaia Leer(IXLWorksheet hoja, DateTime modificado)
    {
        var usado = hoja.RangeUsed() ?? throw new InvalidDataException($"La hoja «{hoja.Name}» está vacía.");
        var primera = usado.FirstRow().RowNumber();
        var ultima = usado.LastRow().RowNumber();

        // Encabezados sin espacios a los lados («ID_Agente » viene con uno al final).
        var columnas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var celda in hoja.Row(primera).CellsUsed())
        {
            var nombre = celda.GetString().Trim();
            if (nombre.Length > 0) columnas.TryAdd(nombre, celda.Address.ColumnNumber);
        }
        if (!columnas.ContainsKey("ID_Agente"))
        {
            throw new InvalidDataException("La hoja de nómina no tiene la columna «ID_Agente».");
        }

        string Texto(IXLRow fila, string columna)
            => columnas.TryGetValue(columna, out var c) ? Limpio(fila.Cell(c)) : "";

        var porId = new Dictionary<string, AgenteGaia>(StringComparer.Ordinal);
        var orden = new List<AgenteGaia>();
        int sinId = 0, repetidos = 0, fechasRaras = 0;

        for (var r = primera + 1; r <= ultima; r++)
        {
            var fila = hoja.Row(r);
            var id = Texto(fila, "ID_Agente");
            var nombre = Texto(fila, "Personal");
            if (id.Length == 0)
            {
                if (nombre.Length > 0) sinId++;
                continue;
            }

            if (!porId.TryGetValue(id, out var agente))
            {
                agente = new AgenteGaia
                {
                    Id = id,
                    Nombre = nombre,
                    Sector = Texto(fila, "Sector"),
                    Supervisor = Texto(fila, "Supervisor"),
                    Coordinador = Texto(fila, "Coordinador"),
                    Formador = Texto(fila, "Formador"),
                    Cargo = Texto(fila, "Cargo"),
                    Oleada = Texto(fila, "Oleada"),
                };
                porId[id] = agente;
                orden.Add(agente);
            }
            else
            {
                // El mismo agente en dos filas (p. ej. repitió oleada): se juntan sus días.
                repetidos++;
            }

            foreach (var col in ColumnasDias)
            {
                if (!columnas.TryGetValue(col, out var c)) continue;
                var celda = fila.Cell(c);
                if (celda.IsEmpty()) continue;
                if (Fecha(celda) is { } dia) agente.Dias.TryAdd(dia, col);
                else fechasRaras++;
            }
        }

        var avisos = new List<string>();
        if (sinId > 0) avisos.Add($"{sinId} {(sinId == 1 ? "fila del Excel no tiene" : "filas del Excel no tienen")} ID_Agente: esos agentes no salen.");
        if (repetidos > 0) avisos.Add($"{repetidos} ID_Agente {(repetidos == 1 ? "aparece" : "aparecen")} en más de una fila: se juntan sus días.");
        if (fechasRaras > 0) avisos.Add($"{fechasRaras} {(fechasRaras == 1 ? "celda de fecha no se entiende" : "celdas de fecha no se entienden")} y se ignoran.");
        var sinDias = orden.Count(a => a.Dias.Count == 0);
        if (sinDias > 0) avisos.Add($"{sinDias} {(sinDias == 1 ? "agente no tiene" : "agentes no tienen")} ninguna fecha de formación.");

        return new NominaGaia { Agentes = orden, Avisos = avisos, ExcelModificado = modificado };
    }

    private static string Limpio(IXLCell celda)
    {
        if (celda.IsEmpty()) return "";
        // Los números (Oleada, ID numéricos) sin «.0» ni separadores.
        if (celda.DataType == XLDataType.Number)
        {
            var n = celda.GetDouble();
            return n == Math.Floor(n) ? ((long)n).ToString(CultureInfo.InvariantCulture) : n.ToString(CultureInfo.InvariantCulture);
        }
        return celda.GetFormattedString().Trim();
    }

    /// <summary>La fecha de una celda: fecha de Excel, número de serie o texto dd/mm/aaaa o aaaa-mm-dd.</summary>
    internal static DateOnly? Fecha(IXLCell celda)
    {
        switch (celda.DataType)
        {
            case XLDataType.DateTime:
                return DateOnly.FromDateTime(celda.GetDateTime());
            case XLDataType.Number:
                var n = celda.GetDouble();
                return n is > 30000 and < 80000 ? DateOnly.FromDateTime(DateTime.FromOADate(n)) : null;
            default:
                var t = celda.GetString().Trim();
                string[] formatos = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy", "d/M/yy"];
                foreach (var f in formatos)
                {
                    if (DateOnly.TryParseExact(t, f, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
                }
                // Con hora («06/04/2026 0:00:00»).
                return DateTime.TryParse(t, CultureInfo.GetCultureInfo("es-ES"), DateTimeStyles.None, out var dt)
                    ? DateOnly.FromDateTime(dt) : null;
        }
    }
}
