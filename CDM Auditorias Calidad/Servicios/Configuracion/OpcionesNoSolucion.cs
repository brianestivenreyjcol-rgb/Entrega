namespace CDM_Auditorias_Calidad.Servicios.Configuracion;

/// <summary>
/// Sección <c>NoSolucion</c> de appsettings.json: la pantalla «CDM No solución», traída de
/// ranking-mvc (allí eran las variables <c>NOSOL_*</c> del .env, con los mismos valores).
/// </summary>
public sealed class OpcionesNoSolucion
{
    public const string Seccion = "NoSolucion";

    /// <summary>Cadena ODBC del driver Simba de BigQuery. El DSN <c>BQCOL</c> es de usuario (HKCU).</summary>
    public string Odbc { get; set; } = "DSN=BQCOL;";

    /// <summary>
    /// El cubo ya construido (unos 60 MB). Relativo a la carpeta de la aplicación: en desarrollo,
    /// <c>App_Data</c> del proyecto (no se versiona ni se publica); en producción,
    /// <c>publicacion\datos</c> (appsettings.Production.json).
    /// </summary>
    public string RutaCache { get; set; } = Path.Combine("App_Data", "cache_nosolucion.json");

    /// <summary>Días que se traen, hacia atrás desde ayer.</summary>
    public int Ventana { get; set; } = 90;

    /// <summary>Últimos días que se marcan como provisionales (las encuestas llegan tarde).</summary>
    public int DiasProvisionales { get; set; } = 5;

    /// <summary>Días por consulta a BigQuery (con 90 de golpe el driver fallaba).</summary>
    public int Tramo { get; set; } = 45;

    /// <summary>Horas tras las que el cubo se vuelve a traer al entrar alguien.</summary>
    public double RefrescoHoras { get; set; } = 12;

    /// <summary>Tabla de etiquetas de impedimento a categorías (fija, viaja con el código).</summary>
    public string RutaTablaImpedimentos { get; set; } = Path.Combine("Datos", "impedimentos_tabla.json");
}
