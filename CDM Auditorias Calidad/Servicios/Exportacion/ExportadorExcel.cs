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
    /// «T0 y planes de acción»: cada auditoría con Tolerancia 0 con su alerta T0 y su plan, para revisar
    /// una por una las que no cumplen la regla.
    /// </summary>
    public static byte[] GenerarT0(IReadOnlyList<AuditoriaT0> filas, string titulo)
    {
        using var libro = new XLWorkbook();
        var hoja = libro.AddWorksheet("Auditorias T0");

        // (cabecera, ancho, valor, formato)
        var columnas = new (string Cabecera, double Ancho, Func<AuditoriaT0, XLCellValue> Valor, string? Formato)[]
        {
            ("Situación", 18, a => TextosT0.Texto(a.Situacion), null),
            ("Fecha auditoría", 13, a => a.Fecha.ToDateTime(TimeOnly.MinValue), "dd/mm/yyyy"),
            ("Sector", 26, a => Texto(a.Sector), null),
            ("Super", 28, a => Texto(a.Super), null),
            ("Team", 28, a => Texto(a.Team), null),
            ("Agente", 32, a => Texto(a.Agente), null),
            ("Legajo", 10, a => Numero(a.Legajo), null),
            ("Plantilla", 26, a => Texto(a.Plantilla), null),
            ("Infracción T0", 50, a => Texto(a.Infraccion), null),
            ("Nota", 9, a => a.Nota is { } n ? n : Blank.Value, "0.00 %"),
            ("Auditor", 30, a => Texto(a.NombreAuditor), null),
            ("Cargo auditor", 22, a => Texto(a.CargoAuditor), null),
            ("ID gestión ICEBERG", 14, a => a.IdGestion, null),
            ("ID llamada", 38, a => Texto(a.IdLlamada), null),
            ("Cómo se encontró la alerta", 22, a => Texto(a.Cruce), null),
            ("ID alerta T0", 11, a => Numero(a.IdAlerta), null),
            ("Fecha reporte T0", 16, a => Momento(a.FechaReporteT0), "dd/mm/yyyy hh:mm"),
            ("Gravedad", 13, a => Texto(a.Gravedad), null),
            ("Estado alerta", 13, a => Texto(a.EstadoAlerta), null),
            ("Acción", 20, a => Texto(a.Accion), null),
            ("ID acción", 11, a => Numero(a.IdAccion), null),
            ("Fecha gestionado", 16, a => Momento(a.FechaGestionado), "dd/mm/yyyy hh:mm"),
            ("Gestionador", 30, a => Texto(a.Gestionador), null),
            ("Cargo gestionador", 22, a => Texto(a.CargoGestionador), null),
            ("ID plan (PlanAccion)", 13, a => Numero(a.IdPlan), null),
            ("Fecha plan", 16, a => Momento(a.FechaPlan), "dd/mm/yyyy hh:mm"),
            ("Estado plan", 12, a => Texto(a.EstadoPlan), null),
            ("Motivo plan", 26, a => Texto(a.MotivoPlan), null),
            ("Plan «Acción de Calidad» 0-15 días", 16, a => Numero(a.IdPlanCalidad), null),
            ("Fecha plan «Acción de Calidad»", 16, a => Momento(a.FechaPlanCalidad), "dd/mm/yyyy hh:mm"),
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
