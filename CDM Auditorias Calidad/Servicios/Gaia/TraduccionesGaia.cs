namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>
/// Las traducciones al español que hacía Power Query en la tabla <c>DatosBQ</c> del PBI (pasos
/// «… traducido»). Un valor que no está en la lista se deja como viene, igual que en el PBI.
/// </summary>
public static class TraduccionesGaia
{
    private static Dictionary<string, string> D(params (string, string)[] pares)
        => pares.ToDictionary(p => p.Item1, p => p.Item2, StringComparer.Ordinal);

    public static readonly IReadOnlyDictionary<string, string> RiesgoChurn = D(
        ("churned", "Cancelado"), ("high", "Alto"), ("moderate", "Moderado"), ("NA", "No disponible"),
        ("noRisk", "Sin riesgo"), ("Not applicable", "No aplica"));

    public static readonly IReadOnlyDictionary<string, string> Sentimiento = D(
        ("positive", "Positivo"), ("neutral", "Neutro"), ("negative", "Negativo"), ("mixed", "Mixto"),
        ("NA", "No disponible"));

    public static readonly IReadOnlyDictionary<string, string> Contexto = D(
        ("sufficient", "Suficiente"), ("insufficient", "Insuficiente"));

    public static readonly IReadOnlyDictionary<string, string> TipoProblema = D(
        ("accountManagement", "Gestión de cuenta"), ("billingAndPayment", "Facturación y pagos"),
        ("customerRetentionAndOnboarding", "Retención y bienvenida de clientes"),
        ("deviceAndTechnicalSupport", "Soporte técnico y de dispositivos"), ("fraudAndSecurity", "Fraude y seguridad"),
        ("internationalAndRoamingServices", "Servicios internacionales y roaming"), ("networkAndCoverage", "Red y cobertura"),
        ("numberPortabilityAndTransfer", "Portabilidad y transferencia de número"), ("other", "Otro"),
        ("policyAndService", "Políticas y servicio"), ("promotionsAndOffers", "Promociones y ofertas"),
        ("webAppTechnicalSupport", "Soporte técnico web/app"), ("NA", "No aplica"));

    public static readonly IReadOnlyDictionary<string, string> RazonNivel1 = D(
        ("complaint", "Queja"), ("inquiry", "Consulta"), ("issue_resolution", "Resolución de incidencia"),
        ("technical_support", "Soporte técnico"), ("NA", "No aplica"));

    public static readonly IReadOnlyDictionary<string, string> ResultadoVenta = D(
        ("acceptedOffer", "Oferta aceptada"), ("consideringOffer", "Oferta en consideración"),
        ("declinedOffer", "Oferta rechazada"), ("noResponse", "Sin respuesta"), ("notApplicable", "No aplicable"),
        ("NA", "No aplica"));

    public static readonly IReadOnlyDictionary<string, string> CategoriaOferta = D(
        ("additionalMobileLines", "Líneas móviles adicionales"), ("deviceUpgrade", "Renovación de dispositivo"),
        ("discounts", "Descuentos"), ("fixedLineSales", "Venta de línea fija"), ("tariffChanges", "Cambios de tarifa"),
        ("valueAddedService", "Servicio de valor añadido"), ("other", "Otro"), ("Not applicable", "No aplica"),
        ("NA", "No aplica"));

    /// <summary>«accounthManagement» viene así de DataOrb (con la hache de más).</summary>
    public static readonly IReadOnlyDictionary<string, string> TipoConsulta = D(
        ("accounthManagement", "Gestión de cuenta"), ("billingAndPayment", "Facturación y pagos"),
        ("deviceSupportAndManagement", "Soporte y gestión de dispositivos"), ("networkCoverage", "Cobertura de red"),
        ("onboardingAndWelcome", "Bienvenida y activación"), ("promotionsAndOffers", "Promociones y ofertas"),
        ("roamingServices", "Servicios de roaming"), ("serviceActivation", "Activación de servicio"),
        ("serviceAndPlanManagement", "Gestión de servicios y planes"),
        ("serviceDiscontinuation", "Desactivación del servicio"), ("other", "Otro"), ("NA", "No aplica"));

    public static readonly IReadOnlyDictionary<string, string> EstadoResolucion = D(
        ("cannot_fulfill", "No se puede cumplir"), ("pending", "Pendiente"), ("resolved", "Resuelto"),
        ("NA", "No disponible"));

    /// <summary>La traducción de <paramref name="valor"/> o el propio valor si no tiene.</summary>
    public static string Traducir(IReadOnlyDictionary<string, string> tabla, string valor)
        => tabla.TryGetValue(valor, out var t) ? t : valor;
}
