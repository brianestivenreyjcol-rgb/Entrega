namespace CDM_Auditorias_Calidad.Servicios.Configuracion;

/// <summary>
/// Lector mínimo de ficheros <c>.env</c>: líneas <c>CLAVE=valor</c>, comentarios con
/// almohadilla y líneas vacías.
/// </summary>
/// <remarks>
/// Solo el primer igual separa (una contraseña puede llevar más) y se quitan las comillas
/// envolventes del valor. Si el fichero no existe, devuelve un diccionario vacío.
/// </remarks>
public static class LectorDotEnv
{
    public static Dictionary<string, string> Cargar(string ruta)
    {
        var valores = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!File.Exists(ruta)) return valores;

        foreach (var lineaCruda in File.ReadLines(ruta))
        {
            var linea = lineaCruda.Trim();

            if (linea.Length == 0 || linea.StartsWith('#')) continue;

            var corte = linea.IndexOf('=');
            if (corte <= 0) continue;

            var clave = linea[..corte].Trim();
            var valor = linea[(corte + 1)..].Trim();

            if (valor.Length >= 2 &&
                ((valor[0] == '"' && valor[^1] == '"') ||
                 (valor[0] == '\'' && valor[^1] == '\'')))
            {
                valor = valor[1..^1];
            }

            valores[clave] = valor;
        }

        return valores;
    }
}
