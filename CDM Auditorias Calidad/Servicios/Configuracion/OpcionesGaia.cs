namespace CDM_Auditorias_Calidad.Servicios.Configuracion;

/// <summary>
/// Sección <c>Gaia</c> de appsettings.json: la vista «GAIA Formación» (port del PBI
/// <c>GAIA Formación.pbip</c>). Las llamadas salen de BigQuery filtradas por el Excel de nómina.
/// </summary>
public sealed class OpcionesGaia
{
    public const string Seccion = "Gaia";

    /// <summary>
    /// El Excel de nómina de la carpeta compartida (ruta UNC, no la unidad <c>Z:</c>, que es del
    /// usuario). De él salen los agentes y sus días de preconexión y aseguramiento.
    /// </summary>
    public string RutaExcel { get; set; } = @"\\172.16.232.102\Proyecto Ranking\2.GAIA\Formación\Nomina Iniciales General.xlsx";

    /// <summary>La única hoja que se lee (la otra guarda credenciales y no se abre).</summary>
    public string Hoja { get; set; } = "Oleadas";

    /// <summary>Cadena ODBC del driver Simba de BigQuery (el mismo DSN que No solución).</summary>
    public string Odbc { get; set; } = "DSN=BQCOL;";

    /// <summary>Agentes por consulta a BigQuery (con todos de golpe el driver se cortaba).</summary>
    public int AgentesPorConsulta { get; set; } = 60;

    /// <summary>Los datos ya traídos, en disco. Relativo a la carpeta de la aplicación.</summary>
    public string RutaCache { get; set; } = Path.Combine("App_Data", "cache_gaia.json");

    /// <summary>Pares de palabras de la españolización (viaja con el código).</summary>
    public string RutaPalabras { get; set; } = Path.Combine("Datos", "palabras_gaia.json");

    /// <summary>Horas tras las que se vuelven a traer aunque el Excel no cambie (la rellamada mira 3 días adelante).</summary>
    public double RefrescoHoras { get; set; } = 12;

    /// <summary>Cada cuántos minutos se mira si el Excel cambió.</summary>
    public double MinutosRevision { get; set; } = 5;
}
