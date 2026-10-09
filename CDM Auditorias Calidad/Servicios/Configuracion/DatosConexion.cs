namespace CDM_Auditorias_Calidad.Servicios.Configuracion;

/// <summary>
/// Datos de conexión al SQL Server de Reporting (el mismo del Power BI).
/// </summary>
/// <remarks>
/// Se leen del .env (<see cref="OpcionesAuditorias.RutaEnv"/>); una variable de entorno
/// con el mismo nombre tiene prioridad.
/// </remarks>
public sealed class DatosConexion
{
    public required string Host { get; init; }
    public required string Base { get; init; }
    public required string Usuario { get; init; }
    public required string Password { get; init; }
    public required bool ConfiarEnCertificado { get; init; }

    /// <summary>Para diagnóstico: nunca incluye la contraseña.</summary>
    public string Descripcion => $"{Base} en {Host} (usuario {Usuario})";

    public string CadenaDeConexion =>
        $"Server={Host};Database={Base};User ID={Usuario};Password={Password};" +
        $"Encrypt=True;TrustServerCertificate={(ConfiarEnCertificado ? "True" : "False")};" +
        "Application Name=CDM Auditorias Calidad;Connect Timeout=30;";

    public static DatosConexion Cargar(string rutaEnv)
    {
        var env = LectorDotEnv.Cargar(rutaEnv);

        string? Opcional(string clave)
        {
            var v = Environment.GetEnvironmentVariable(clave);
            if (!string.IsNullOrWhiteSpace(v)) return v;
            return env.TryGetValue(clave, out var x) && !string.IsNullOrWhiteSpace(x) ? x : null;
        }

        string Obligatorio(string clave) => Opcional(clave) ?? throw new InvalidOperationException(
            $"Falta {clave} en {Path.GetFullPath(rutaEnv)} (plantilla en env.ejemplo) o en las variables de entorno.");

        return new DatosConexion
        {
            Host = Obligatorio("AUDITORIAS_DB_HOST"),
            Base = Opcional("AUDITORIAS_DB_NAME") ?? "REPORTING",
            Usuario = Obligatorio("AUDITORIAS_DB_USER"),
            Password = Obligatorio("AUDITORIAS_DB_PASSWORD"),
            ConfiarEnCertificado = (Opcional("DB_TRUST_SERVER_CERTIFICATE") ?? "false")
                .Trim().Equals("true", StringComparison.OrdinalIgnoreCase),
        };
    }
}
