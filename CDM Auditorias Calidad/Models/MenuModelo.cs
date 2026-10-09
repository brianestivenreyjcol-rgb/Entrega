namespace CDM_Auditorias_Calidad.Models;

/// <summary>Cuándo se trajeron los datos de un informe y si se están trayendo ahora.</summary>
public sealed record EstadoInforme(DateTime? Cargado, bool EnCurso);

/// <summary>
/// La portada: si hay datos de cada informe y de cuándo son. Auditorías trae su instantánea entera (para el
/// número de auditorías y el rango); No solución y GAIA, solo su estado.
/// </summary>
public sealed record MenuModelo(InstantaneaAuditorias? Datos, string? Error, EstadoInforme NoSolucion, EstadoInforme Gaia);
