using System.Diagnostics;
using System.Globalization;
using CDM_Auditorias_Calidad.Models;
using CDM_Auditorias_Calidad.Servicios.Configuracion;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace CDM_Auditorias_Calidad.Servicios.Datos;

/// <summary>
/// Ejecuta la consulta del Power BI (<c>Consultas/Auditorias.sql</c>) y devuelve sus filas.
/// </summary>
public sealed class RepositorioAuditorias
{
    private readonly IOptions<OpcionesAuditorias> _opciones;
    private readonly IWebHostEnvironment _entorno;

    public RepositorioAuditorias(IOptions<OpcionesAuditorias> opciones, IWebHostEnvironment entorno)
    {
        _opciones = opciones;
        _entorno = entorno;
    }

    // Las consultas se leen de la carpeta de la aplicación (en desarrollo, la del proyecto; en
    // producción, la publicada) y en cada carga: un cambio en el .sql vale con pulsar
    // «Actualizar», sin recompilar. Antes se leía la copia de bin, que no siempre se refrescaba
    // al compilar (02-10-2026: el «- 3» de ICEBERG no llegaba a la web).
    public string RutaConsulta => Path.Combine(_entorno.ContentRootPath, "Consultas", "Auditorias.sql");

    public async Task<InstantaneaAuditorias> CargarAsync(CancellationToken ct)
    {
        var op = _opciones.Value;
        // El .env se lee en cada carga: así un cambio de contraseña no obliga a reiniciar.
        var conexion = DatosConexion.Cargar(Path.Combine(_entorno.ContentRootPath, op.RutaEnv));
        var sql = await File.ReadAllTextAsync(RutaConsulta, ct);

        var reloj = Stopwatch.StartNew();
        var filas = new List<Auditoria>(20_000);

        await using var cn = new SqlConnection(conexion.CadenaDeConexion);
        await cn.OpenAsync(ct);
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = op.SegundosConsulta };
        await using var rd = await cmd.ExecuteReaderAsync(ct);

        int iFecha = rd.GetOrdinal("Fecha"), iLegajo = rd.GetOrdinal("legajo"), iSector = rd.GetOrdinal("Sector"),
            iSuper = rd.GetOrdinal("Super"), iTeam = rd.GetOrdinal("Team"), iAgente = rd.GetOrdinal("Agente"),
            iLlamada = rd.GetOrdinal("ID_Llamada"), iCorreo = rd.GetOrdinal("CorreoAuditor"),
            iAuditor = rd.GetOrdinal("Nombre_Auditor"), iCargo = rd.GetOrdinal("Cargo_Auditor"),
            iRespuesta = rd.GetOrdinal("Respuesta"), iBase = rd.GetOrdinal("Base");

        while (await rd.ReadAsync(ct))
        {
            // Sin fecha la fila no entra en ningún filtro ni gráfico (en el PBI queda fuera del calendario).
            if (rd.IsDBNull(iFecha)) continue;

            filas.Add(new Auditoria(
                DateOnly.FromDateTime(rd.GetDateTime(iFecha)),
                Legajo(rd, iLegajo),
                Texto(rd, iSector),
                Texto(rd, iSuper),
                Texto(rd, iTeam),
                Texto(rd, iAgente),
                Texto(rd, iLlamada),
                Texto(rd, iCorreo),
                Texto(rd, iAuditor),
                Texto(rd, iCargo),
                rd.IsDBNull(iRespuesta) ? null : Convert.ToDouble(rd.GetValue(iRespuesta), CultureInfo.InvariantCulture),
                Texto(rd, iBase)));
        }

        await rd.CloseAsync();

        // La nómina va aparte: si falla, el informe sigue con las auditorías y la tarjeta
        // «Total agentes» sale sin dato.
        IReadOnlyList<RegistroNomina>? nomina = null;
        string? errorNomina = null;
        if (filas.Count > 0)
        {
            try
            {
                nomina = await CargarNominaAsync(cn, filas.Min(f => f.Fecha), filas.Max(f => f.Fecha), op.SegundosConsulta, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errorNomina = ex.Message;
            }
        }

        // Las T0 también van aparte: si Reportar_T0 no responde, solo esa pestaña sale sin datos.
        IReadOnlyList<AuditoriaT0>? t0 = null;
        IReadOnlyList<AlertaT0>? reportesT0 = null;
        string? errorT0 = null;
        try
        {
            (t0, reportesT0) = await CargarT0Async(cn, op.SegundosConsulta, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errorT0 = ex.Message;
        }

        return new InstantaneaAuditorias(filas, DateTime.Now, reloj.Elapsed, nomina, errorNomina, t0, reportesT0, errorT0);
    }

    public string RutaConsultaT0 => Path.Combine(_entorno.ContentRootPath, "Consultas", "AuditoriasT0.sql");

    /// <summary>
    /// Las auditorías con T0 (primer resultado) y las filas de <c>[Reporting].[pdc].[Reportar_T0]</c> (segundo); cada
    /// auditoría queda enlazada con su fila por <c>IdReporte</c>.
    /// </summary>
    private async Task<(IReadOnlyList<AuditoriaT0>, IReadOnlyList<AlertaT0>)> CargarT0Async(SqlConnection cn, int segundos, CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(RutaConsultaT0, ct);
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = segundos };
        await using var rd = await cmd.ExecuteReaderAsync(ct);

        DateTime? Momento(int i) => rd.IsDBNull(i) ? null : rd.GetDateTime(i);
        long? Numero(int i) => rd.IsDBNull(i) ? null : Convert.ToInt64(rd.GetValue(i), CultureInfo.InvariantCulture);

        var auditorias = new List<AuditoriaT0>(4_000);
        {
            int A(string nombre) => rd.GetOrdinal(nombre);
            int iFecha = A("Fecha"), iLegajo = A("legajo"), iSector = A("Sector"), iSuper = A("Super"), iTeam = A("Team"),
                iAgente = A("Agente"), iGestion = A("IdGestion"), iLlamada = A("ID_Llamada"), iLegAuditor = A("LegajoAuditor"),
                iAuditor = A("Nombre_Auditor"), iCargo = A("Cargo_Auditor"), iPlantilla = A("Plantilla"), iInfraccion = A("Infraccion"),
                iNota = A("Respuesta"), iReporte = A("IdReporte"), iCruce = A("Cruce");
            while (await rd.ReadAsync(ct))
            {
                if (rd.IsDBNull(iFecha) || Numero(iGestion) is not { } gestion) continue;
                auditorias.Add(new AuditoriaT0(
                    DateOnly.FromDateTime(rd.GetDateTime(iFecha)),
                    Legajo(rd, iLegajo),
                    Texto(rd, iSector), Texto(rd, iSuper), Texto(rd, iTeam), Texto(rd, iAgente),
                    gestion, Texto(rd, iLlamada), Numero(iLegAuditor), Texto(rd, iAuditor), Texto(rd, iCargo),
                    Texto(rd, iPlantilla), Texto(rd, iInfraccion),
                    rd.IsDBNull(iNota) ? null : Convert.ToDouble(rd.GetValue(iNota), CultureInfo.InvariantCulture),
                    Numero(iReporte), Texto(rd, iCruce)));
            }
        }

        if (!await rd.NextResultAsync(ct)) throw new InvalidOperationException("La consulta de T0 no devolvió las filas de Reportar_T0 (segundo resultado).");

        var reportes = new List<AlertaT0>(10_000);
        {
            int O(string nombre) => rd.GetOrdinal(nombre);
            int iId = O("ID"), iReporte = O("Fecha_reporte_Alerta"), iFalta = O("Fecha_Falta"), iLogin = O("Login"), iLegajo = O("Legajo_AG"),
                iSector = O("Sector"), iSuper = O("Supervisor"), iTeam = O("Team"), iAgente = O("Agente"), iLlamada = O("ID_Llamada"),
                iInfraccion = O("Infraccion"), iGravedad = O("Gravedad"), iObsReporte = O("Observaciones_Reporte"),
                iLegAuditor = O("Auditor_Legajo"), iAuditor = O("Nombre_Auditor"), iCargo = O("Cargo_Auditor"), iEstado = O("Estado"),
                iGestionado = O("Fecha_Gestionado"), iAccion = O("Accion"), iIdAccion = O("ID_Accion"), iObsRespuesta = O("Observaciones_Respuesta"),
                iGestionador = O("Nombre_Gestionador"), iCargoGest = O("Cargo_Gestionador"), iCompromiso = O("compromiso_nocumplido"),
                iExpediente = O("expediente"), iMotivo = O("MotivoLlamada"), iOportunidad = O("OportunidadMejora");

            while (await rd.ReadAsync(ct))
            {
                if (Momento(iReporte) is not { } reporte || Numero(iId) is not { } id) continue;
                reportes.Add(new AlertaT0(
                    id,
                    DateOnly.FromDateTime(reporte),
                    reporte,
                    Momento(iFalta) is { } falta ? DateOnly.FromDateTime(falta) : null,
                    Numero(iLogin), Numero(iLegajo),
                    Texto(rd, iSector), Texto(rd, iSuper), Texto(rd, iTeam), Texto(rd, iAgente),
                    Texto(rd, iLlamada), Texto(rd, iInfraccion), Texto(rd, iGravedad), Texto(rd, iObsReporte),
                    Numero(iLegAuditor), Texto(rd, iAuditor), Texto(rd, iCargo),
                    Texto(rd, iEstado), Momento(iGestionado), Texto(rd, iAccion), Numero(iIdAccion), Texto(rd, iObsRespuesta),
                    Texto(rd, iGestionador), Texto(rd, iCargoGest),
                    !rd.IsDBNull(iCompromiso) && Convert.ToBoolean(rd.GetValue(iCompromiso), CultureInfo.InvariantCulture),
                    Numero(iExpediente), Texto(rd, iMotivo), Texto(rd, iOportunidad)));
            }
        }

        var porId = reportes.GroupBy(r => r.Id).ToDictionary(g => g.Key, g => g.First());
        var enlazadas = auditorias
            .Select(a => a.IdReporte is { } id && porId.TryGetValue(id, out var r) ? a with { Reporte = r } : a)
            .ToList();
        return (enlazadas, reportes);
    }

    public string RutaConsultaNomina => Path.Combine(_entorno.ContentRootPath, "Consultas", "Nomina.sql");

    private async Task<IReadOnlyList<RegistroNomina>> CargarNominaAsync(SqlConnection cn, DateOnly desde, DateOnly hasta, int segundos, CancellationToken ct)
    {
        var sql = await File.ReadAllTextAsync(RutaConsultaNomina, ct);
        await using var cmd = new SqlCommand(sql, cn) { CommandTimeout = segundos };
        cmd.Parameters.Add("@Desde", System.Data.SqlDbType.Date).Value = desde.ToDateTime(TimeOnly.MinValue);
        cmd.Parameters.Add("@Hasta", System.Data.SqlDbType.Date).Value = hasta.ToDateTime(TimeOnly.MinValue);
        await using var rd = await cmd.ExecuteReaderAsync(ct);

        int iFecha = rd.GetOrdinal("Fecha"), iLegajo = rd.GetOrdinal("legajo"), iSector = rd.GetOrdinal("Sector"),
            iSuper = rd.GetOrdinal("Super"), iTeam = rd.GetOrdinal("Team"), iCargo = rd.GetOrdinal("Cargo");

        // Unas 200.000 filas con muy pocos textos distintos: se comparten las cadenas.
        var textos = new Dictionary<string, string>(StringComparer.Ordinal);
        string? Compartido(SqlDataReader r, int i)
        {
            var s = Texto(r, i)?.Trim();
            if (s is null) return null;
            if (!textos.TryGetValue(s, out var c)) textos[s] = c = s;
            return c;
        }

        var nomina = new List<RegistroNomina>(250_000);
        while (await rd.ReadAsync(ct))
        {
            if (rd.IsDBNull(iFecha) || Legajo(rd, iLegajo) is not { } legajo) continue;
            nomina.Add(new RegistroNomina(
                DateOnly.FromDateTime(rd.GetDateTime(iFecha)),
                legajo,
                Compartido(rd, iSector),
                Compartido(rd, iSuper),
                Compartido(rd, iTeam),
                Compartido(rd, iCargo)));
        }
        return nomina;
    }

    private static string? Texto(SqlDataReader rd, int i)
        => rd.IsDBNull(i) ? null : Convert.ToString(rd.GetValue(i), CultureInfo.InvariantCulture);

    /// <summary>
    /// El legajo llega como número en WEB y puede llegar como texto en ICEBERG.
    /// </summary>
    private static long? Legajo(SqlDataReader rd, int i)
    {
        if (rd.IsDBNull(i)) return null;
        var v = rd.GetValue(i);
        if (v is string s)
            return long.TryParse(s.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
        return Convert.ToInt64(v, CultureInfo.InvariantCulture);
    }
}
