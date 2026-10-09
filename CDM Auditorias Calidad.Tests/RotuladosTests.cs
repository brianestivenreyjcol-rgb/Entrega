using CDM_Auditorias_Calidad.Views.Gaia;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>
/// Qué puntos de una gráfica llevan su pastilla con el valor (<see cref="AyudasGaia.Rotulados"/>): sin pisarse sobre el plano
/// estimado, contando con que la primera y la última se corren hacia dentro, y siempre la primera y la última.
/// </summary>
public class RotuladosTests
{
    private static AyudasGaia.GraficoGaia Grafico(int n, int ancho, bool entero = false, bool dia = false)
    {
        var etiquetas = Enumerable.Range(1, n).Select(i => i.ToString()).ToList();
        return new AyudasGaia.GraficoGaia(etiquetas, etiquetas, Enumerable.Repeat(1, n).ToList(), [])
        {
            AnchoEstimado = ancho, Entero = entero, Dia = dia,
        };
    }

    /// <summary>Lo que ocupa cada pastilla con las mismas cuentas que el CSS (centrada; .al-inicio y .al-final, el 84 % hacia dentro).</summary>
    private static (double Izq, double Der) Sitio(AyudasGaia.GraficoGaia g, int i, double ancho)
    {
        var n = g.Etiquetas.Count;
        var x = (i + 0.5) / n * g.AnchoEstimado;
        var aLaIzquierda = i == 0 ? 0.16 : i == n - 1 ? 0.84 : 0.5;
        return (x - ancho * aLaIzquierda, x + ancho * (1 - aLaIzquierda));
    }

    [Theory]
    [InlineData(4, 290, true)]
    [InlineData(15, 290, true)]
    [InlineData(15, 290, false)]
    [InlineData(101, 290, true)]
    [InlineData(101, 290, false)]
    [InlineData(31, 900, false)]
    [InlineData(2, 290, false)]
    public void Nunca_se_pisan_y_estan_la_primera_y_la_ultima(int n, int ancho, bool entero)
    {
        var g = Grafico(n, ancho, entero, dia: n > 20);
        var r = AyudasGaia.Rotulados(g).Order().ToList();

        Assert.Equal(0, r[0]);
        Assert.Equal(n - 1, r[^1]);
        var pastilla = entero ? 42 : 58;
        for (var k = 1; k < r.Count; k++)
            Assert.True(Sitio(g, r[k], pastilla).Izq >= Sitio(g, r[k - 1], pastilla).Der, $"se pisan {r[k - 1]} y {r[k]}");
    }

    [Fact]
    public void Si_caben_todas_van_todas()
    {
        Assert.Equal(Enumerable.Range(0, 4), AyudasGaia.Rotulados(Grafico(4, 900)).Order());
    }

    [Fact]
    public void Con_muchos_dias_como_mucho_una_de_cada_dos()
    {
        var r = AyudasGaia.Rotulados(Grafico(30, 5000, entero: true, dia: true)).Order().ToList();
        Assert.All(r.Zip(r.Skip(1)), par => Assert.True(par.Second - par.First >= 2));
    }
}
