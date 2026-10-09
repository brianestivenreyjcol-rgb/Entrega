namespace CDM_Auditorias_Calidad.Servicios.Tablero;

/// <summary>
/// Una página del informe de Auditorías: «General» y «Formación &amp; Calidad» (las del Power BI) y
/// «T0 y planes de acción» (auditorías con Tolerancia 0 frente a su plan de acción).
/// </summary>
/// <param name="Clave">Ruta de la página (<c>/general</c>, <c>/formacion</c>).</param>
/// <param name="VistaInicial">Con qué agrupa los gráficos al entrar (la que dejó guardada el PBI).</param>
/// <param name="Cargos">Filtro de página sobre Cargo_Auditor; null = sin filtro.</param>
public sealed record PaginaTablero(string Clave, string Titulo, Vista VistaInicial, IReadOnlySet<string>? Cargos)
{
    public const string ClaveGeneral = "general";
    public const string ClaveFormacion = "formacion";
    public const string ClaveT0 = "t0";

    public static PaginaTablero General { get; } = new(ClaveGeneral, "General", Vista.Semana, null);

    public static PaginaTablero T0 { get; } = new(ClaveT0, "T0 y planes de acción", Vista.Semana, null);

    public static PaginaTablero Formacion(IEnumerable<string> cargos)
        => new(ClaveFormacion, "Formación & Calidad", Vista.Mes, new HashSet<string>(cargos, StringComparer.Ordinal));
}
