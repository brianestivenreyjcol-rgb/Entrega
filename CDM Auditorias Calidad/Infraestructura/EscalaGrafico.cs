namespace CDM_Auditorias_Calidad.Infraestructura;

/// <summary>Escalas «redondas» para los ejes de los gráficos.</summary>
public static class EscalaGrafico
{
    /// <summary>
    /// Eje de 0 a un tope redondo (1, 2, 2,5 o 5 × 10ⁿ por paso), con sitio encima de la
    /// columna más alta para su etiqueta.
    /// </summary>
    public static (double Tope, double Paso) DesdeCero(double maximo, int marcas = 4)
    {
        if (maximo <= 0) return (1, 1);
        var paso = PasoRedondo(maximo / marcas);
        var tope = Math.Ceiling(maximo / paso) * paso;
        if (maximo / tope > 0.88) tope += paso;
        return (tope, paso);
    }

    /// <summary>
    /// Eje de porcentaje (fracciones de 0 a 1) ajustado a los valores, con pasos de 5, 10 o 20 puntos.
    /// </summary>
    public static (double Minimo, double Maximo, double Paso) Porcentaje(IEnumerable<double> valores)
    {
        var lista = valores.ToList();
        if (lista.Count == 0) return (0, 1, 0.25);

        var lo = lista.Min();
        var hi = lista.Max();
        var rango = Math.Max(0.02, hi - lo);
        var paso = rango <= 0.25 ? 0.05 : rango <= 0.5 ? 0.1 : 0.2;
        var min = Math.Max(0, Math.Floor((lo - paso * 0.4) / paso) * paso);
        var max = Math.Min(1, Math.Ceiling((hi + paso * 0.4) / paso) * paso);
        if (max - min < paso * 2) max = Math.Min(1, min + paso * 2);
        return (min, max, paso);
    }

    private static double PasoRedondo(double bruto)
    {
        var potencia = Math.Pow(10, Math.Floor(Math.Log10(bruto)));
        var f = bruto / potencia;
        var redondo = f <= 1 ? 1 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 5 ? 5 : 10;
        return redondo * potencia;
    }
}
