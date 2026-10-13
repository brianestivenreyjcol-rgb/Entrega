using ClosedXML.Excel;
using CDM_Auditorias_Calidad.Models;

namespace CDM_Auditorias_Calidad.Servicios.Exportacion;

/// <summary>
/// El «Descargable» del Power BI: el detalle de auditorías filtrado, en Excel.
/// </summary>
/// <remarks>
/// Mismas columnas que la tabla del PBI (Base, Fecha, Super, Team, Agente, Sector, Auditor,
/// Cargo Auditor, Respuesta), pero una fila por auditoría: la tabla del PBI juntaba las filas
/// idénticas y sumaba su nota.
/// </remarks>
public static class ExportadorExcel
{
    public const string TipoContenido = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static byte[] Generar(IReadOnlyList<Auditoria> filas, string titulo)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Auditorias");

        string[] cabeceras = ["Base", "Fecha", "Super", "Team", "Agente", "Sector", "Auditor", "Cargo Auditor", "Respuesta"];
        for (var c = 0; c < cabeceras.Length; c++) hoja.Cell(1, c + 1).Value = cabeceras[c];

        for (var i = 0; i < filas.Count; i++)
        {
            var a = filas[i];
            var r = i + 2;
            hoja.Cell(r, 1).Value = a.Base;
            hoja.Cell(r, 2).Value = a.Fecha.ToDateTime(TimeOnly.MinValue);
            hoja.Cell(r, 3).Value = a.Super;
            hoja.Cell(r, 4).Value = a.Team;
            hoja.Cell(r, 5).Value = a.Agente;
            hoja.Cell(r, 6).Value = a.Sector;
            hoja.Cell(r, 7).Value = a.NombreAuditor;
            hoja.Cell(r, 8).Value = a.CargoAuditor;
            if (a.Respuesta is { } nota) hoja.Cell(r, 9).Value = nota;
        }

        var cabecera = hoja.Range(1, 1, 1, cabeceras.Length);
        cabecera.Style.Font.Bold = true;
        // Cabecera de tabla de la marca Orange (guía de estilos, 2.1).
        cabecera.Style.Font.FontColor = XLColor.FromHtml("#7A3A00");
        cabecera.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE3CC");

        var ultima = Math.Max(2, filas.Count + 1);
        hoja.Range(2, 2, ultima, 2).Style.NumberFormat.Format = "dd/mm/yyyy";
        hoja.Range(2, 9, ultima, 9).Style.NumberFormat.Format = "0.00 %";

        // Anchos fijos: ajustarlos al contenido con miles de filas es lento.
        double[] anchos = [10, 12, 30, 30, 34, 30, 34, 22, 11];
        for (var c = 0; c < anchos.Length; c++) hoja.Column(c + 1).Width = anchos[c];

        hoja.Range(1, 1, ultima, cabeceras.Length).SetAutoFilter();
        hoja.SheetView.FreezeRows(1);
        libro.Properties.Title = titulo;

        using var flujo = new MemoryStream();
        libro.SaveAs(flujo);
        return flujo.ToArray();
    }

    /// <summary>
    /// «T0 y planes de acción»: cada auditoría con T0 con su fila de [Reporting].[pdc].[Reportar_T0] (si está), para
    /// revisar una por una las que no se reportaron o no tienen plan.
    /// </summary>
    public static byte[] GenerarT0(IReadOnlyList<AuditoriaT0> filas, string titulo)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Auditorias T0");

        // (cabecera, ancho, valor, formato); las columnas «… (Reportar_T0)» salen de la fila de Reportar_T0.
        var columnas = new (string Cabecera, double Ancho, Func<AuditoriaT0, XLCellValue> Valor, string? Formato)[]
        {
            ("Situación", 20, a => TextosT0.Texto(a.Situacion), null),
            ("Fecha auditoría", 13, a => a.Fecha.ToDateTime(TimeOnly.MinValue), "dd/mm/yyyy"),
            ("Auditor", 30, a => Texto(a.NombreAuditor), null),
            ("Legajo auditor", 12, a => Numero(a.LegajoAuditor), null),
            ("Cargo auditor", 22, a => Texto(a.CargoAuditor), null),
            ("Sector", 26, a => Texto(a.Sector), null),
            ("Super", 28, a => Texto(a.Super), null),
            ("Team", 28, a => Texto(a.Team), null),
            ("Agente", 32, a => Texto(a.Agente), null),
            ("Legajo", 10, a => Numero(a.Legajo), null),
            ("Plantilla", 26, a => Texto(a.Plantilla), null),
            ("Infracción T0", 50, a => Texto(a.Infraccion), null),
            ("Nota", 9, a => a.Nota is { } n ? n : Blank.Value, "0.00 %"),
            ("ID gestión ICEBERG", 14, a => a.IdGestion, null),
            ("ID llamada", 38, a => Texto(a.IdLlamada), null),
            ("Cómo se encontró", 22, a => Texto(a.Cruce), null),
            ("ID (Reportar_T0)", 12, a => Numero(a.Reporte?.Id), null),
            ("Fecha reporte (Reportar_T0)", 16, a => Momento(a.Reporte?.Reporte), "dd/mm/yyyy hh:mm"),
            ("Auditor (Reportar_T0)", 30, a => Texto(a.Reporte?.NombreAuditor), null),
            ("Gravedad (Reportar_T0)", 14, a => Texto(a.Reporte?.Gravedad), null),
            ("Estado (Reportar_T0)", 13, a => Texto(a.Reporte?.Estado), null),
            ("Acción (Reportar_T0)", 20, a => Texto(a.Reporte?.Accion), null),
            ("ID acción (Reportar_T0)", 12, a => Numero(a.Reporte?.IdAccion), null),
            ("Fecha gestionado (Reportar_T0)", 16, a => Momento(a.Reporte?.Gestionado), "dd/mm/yyyy hh:mm"),
            ("Días hasta gestionar", 11, a => a.Reporte?.DiasGestion is { } d ? Math.Round(d, 1) : Blank.Value, "0.0"),
            ("Gestionador (Reportar_T0)", 30, a => Texto(a.Reporte?.Gestionador), null),
            ("Cargo gestionador (Reportar_T0)", 22, a => Texto(a.Reporte?.CargoGestionador), null),
            ("Observaciones reporte (Reportar_T0)", 50, a => Texto(a.Reporte?.ObservacionesReporte), null),
            ("Observaciones respuesta (Reportar_T0)", 50, a => Texto(a.Reporte?.ObservacionesRespuesta), null),
            ("Motivo llamada (Reportar_T0)", 26, a => Texto(a.Reporte?.MotivoLlamada), null),
            ("Oportunidad de mejora (Reportar_T0)", 30, a => Texto(a.Reporte?.OportunidadMejora), null),
        };

        for (var c = 0; c < columnas.Length; c++) hoja.Cell(1, c + 1).Value = columnas[c].Cabecera;
        for (var i = 0; i < filas.Count; i++)
            for (var c = 0; c < columnas.Length; c++)
                hoja.Cell(i + 2, c + 1).Value = columnas[c].Valor(filas[i]);

        var cabecera = hoja.Range(1, 1, 1, columnas.Length);
        cabecera.Style.Font.Bold = true;
        cabecera.Style.Font.FontColor = XLColor.FromHtml("#7A3A00");
        cabecera.Style.Fill.BackgroundColor = XLColor.FromHtml("#FFE3CC");

        var ultima = Math.Max(2, filas.Count + 1);
        for (var c = 0; c < columnas.Length; c++)
        {
            hoja.Column(c + 1).Width = columnas[c].Ancho;
            if (columnas[c].Formato is { } f) hoja.Range(2, c + 1, ultima, c + 1).Style.NumberFormat.Format = f;
        }

        hoja.Range(1, 1, ultima, columnas.Length).SetAutoFilter();
        hoja.SheetView.FreezeRows(1);
        libro.Properties.Title = titulo;

        using var flujo = new MemoryStream();
        libro.SaveAs(flujo);
        return flujo.ToArray();
    }

    private static XLCellValue Texto(string? t) => t is null ? Blank.Value : t;

    private static XLCellValue Numero(long? n) => n is { } v ? v : Blank.Value;

    private static XLCellValue Momento(DateTime? f) => f is { } v ? v : Blank.Value;
}
