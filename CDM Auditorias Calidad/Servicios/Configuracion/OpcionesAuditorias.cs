namespace CDM_Auditorias_Calidad.Servicios.Configuracion;

/// <summary>
/// Sección <c>Auditorias</c> de appsettings.json.
/// </summary>
public sealed class OpcionesAuditorias
{
    public const string Seccion = "Auditorias";

    /// <summary>
    /// Fichero .env con las credenciales de SQL Server. Relativo a la carpeta de la
    /// aplicación (en desarrollo, la del proyecto).
    /// </summary>
    public string RutaEnv { get; set; } = ".env";

    /// <summary>Cada cuántos minutos se vuelve a ejecutar la consulta.</summary>
    public int MinutosRecarga { get; set; } = 30;

    /// <summary>Tiempo máximo de la consulta a SQL Server.</summary>
    public int SegundosConsulta { get; set; } = 300;

    /// <summary>
    /// Medida «Meta de Calidad» del Power BI: la línea discontinua del gráfico de nota.
    /// </summary>
    public double MetaCalidad { get; set; } = 0.5;

    /// <summary>
    /// Filtro de la página «Formación &amp; Calidad» del Power BI (Cargo_Auditor).
    /// </summary>
    public string[] CargosFormacion { get; set; } =
        ["Formador", "Formador PP", "Técnico de Calidad", "Técnico de Calidad PP"];

    /// <summary>
    /// Cargos de la nómina (<c>car_descrip</c>) que cuentan como agente en la tarjeta «Total
    /// agentes». Lo decidió el usuario el 02-10-2026. Se comparan sin mayúsculas ni espacios.
    /// </summary>
    public string[] CargosAgente { get; set; } =
        ["Agente", "Agente en Capacitacion", "Aprendiz Sena Etapa Productiva"];
}
