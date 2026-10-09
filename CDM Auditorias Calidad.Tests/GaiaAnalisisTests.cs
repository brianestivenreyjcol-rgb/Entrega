using CDM_Auditorias_Calidad.Servicios.Gaia;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>GAIA Formación, fases 3 y 4: repartos, motivos, obstáculos, semanas y españolización.</summary>
public class GaiaAnalisisTests
{
    private static LlamadaGaia L(Action<LlamadaGaia>? ajuste = null)
    {
        var l = new LlamadaGaia { IdConversacion = Guid.NewGuid().ToString(), IdAgente = "A1", Agente = "Ana", Marca = "YOIGO", Fecha = new DateOnly(2026, 9, 1) };
        ajuste?.Invoke(l);
        return l;
    }

    private static string RutaPalabras(string json)
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"palabras_{Guid.NewGuid():N}.json");
        File.WriteAllText(ruta, json);
        return ruta;
    }

    private const string Json = """
        {
          "niveles": ["Novato", "Aficionado", "Experto"],
          "pares": [
            { "espana": ["móvil"], "colombia": ["celular"], "nivel": "Novato" },
            { "espana": ["dar de baja"], "colombia": ["cancelación"], "nivel": "Aficionado" },
            { "espana": ["vale"], "colombia": ["dale"], "nivel": null },
            { "espana": [], "colombia": ["regalar"], "nivel": null }
          ]
        }
        """;

    [Fact]
    public void Palabras_se_leen_con_indices_niveles_y_sin_tildes()
    {
        var p = PalabrasGaia.Leer(RutaPalabras(Json));
        Assert.Equal(7, p.Palabras.Count);
        Assert.Equal("movil", p.Palabras[0].Patron);
        Assert.Equal("cancelacion", p.Palabras[3].Patron);
        Assert.True(p.Palabras[0].EsEspana);
        Assert.False(p.Palabras[1].EsEspana);
        Assert.Equal(1, p.Pares[0].Nivel);
        Assert.Null(p.Pares[2].Nivel);
        // Los niveles se acumulan; 0 = todos.
        Assert.Single(p.ParesDelNivel(1));
        Assert.Equal(2, p.ParesDelNivel(3).Count());
        Assert.Equal(4, p.ParesDelNivel(0).Count());
        Assert.Equal("—", p.Pares[3].TextoEspana);
    }

    [Fact]
    public void Palabras_expresion_busca_palabras_enteras()
    {
        var sql = PalabrasGaia.Leer(RutaPalabras(Json)).ExpresionSql();
        Assert.StartsWith("CONCAT(", sql);
        Assert.Contains(@"r'\bmovil\b'), '0,'", sql);
        Assert.Contains(@"r'\bdar de baja\b'), '2,'", sql);
        Assert.Equal([0, 3, 5], PalabrasGaia.Indices("5,0,3,3,"));
        Assert.Empty(PalabrasGaia.Indices(""));
        Assert.Equal("que pena", PalabrasGaia.Normalizar("  Qué   pena "));
        Assert.Equal("año", PalabrasGaia.Normalizar("Año"));
    }

    [Fact]
    public void Espanolizacion_cuenta_sobre_llamadas_con_agente_identificado()
    {
        var p = PalabrasGaia.Leer(RutaPalabras(Json));
        var ll = new[]
        {
            L(l => { l.TieneTranscripcion = true; l.Palabras = [0]; }),        // móvil (España, Novato)
            L(l => { l.TieneTranscripcion = true; l.Palabras = [1, 4]; }),     // celular + vale
            L(l => { l.TieneTranscripcion = true; l.Palabras = [2]; }),        // dar de baja (Aficionado)
            L(l => { l.TieneTranscripcion = true; l.Palabras = []; }),         // nada
            L(l => { l.TieneTranscripcion = true; l.Palabras = null; }),       // sin agente identificado
            L(),                                                                // sin transcripción
        };
        var novato = CalculadoraGaia.Espanolizacion(ll, p, 1);
        Assert.Equal(6, novato.Llamadas);
        Assert.Equal(5, novato.ConTranscripcion);
        Assert.Equal(4, novato.Base);
        Assert.Equal(0.25, novato.Espana);
        Assert.Equal(0.25, novato.Colombia);

        var aficionado = CalculadoraGaia.Espanolizacion(ll, p, 2);
        Assert.Equal(0.5, aficionado.Espana);

        var todas = CalculadoraGaia.Espanolizacion(ll, p, 0);
        Assert.Equal(0.75, todas.Espana); // móvil, vale, dar de baja

        var pares = CalculadoraGaia.ParesEspanolizacion(ll, p, 0);
        Assert.Equal(1, pares[0].LlamadasEspana);
        Assert.Equal(1, pares[0].LlamadasColombia);
        Assert.Equal(4, pares[0].Base);

        var palabras = CalculadoraGaia.PalabrasEspanolizacion(ll, p, 1);
        Assert.Equal(2, palabras.Count);
        Assert.All(palabras, f => Assert.Equal(1, f.Llamadas));
    }

    [Fact]
    public void Reparto_quita_valores_vacios_y_junta_el_resto()
    {
        var ll = Enumerable.Range(0, 5).Select(_ => L(l => l.ResultadoVenta = "Oferta rechazada"))
            .Concat(Enumerable.Range(0, 3).Select(_ => L(l => l.ResultadoVenta = "Oferta aceptada")))
            .Append(L(l => l.ResultadoVenta = "Sin respuesta"))
            .Append(L(l => l.ResultadoVenta = "No aplicable"))
            .Append(L(l => l.ResultadoVenta = "NA"))
            .ToList();
        var r = CalculadoraGaia.Reparto(ll, l => l.ResultadoVenta, maximo: 2);
        Assert.Equal(["Oferta rechazada", "Oferta aceptada", "Otros"], r.Select(x => x.Texto));
        Assert.Equal(5.0 / 9, r[0].Fraccion, 6);
        Assert.Equal(1, r[2].Cantidad);
    }

    [Fact]
    public void Obstaculos_separados_por_barra_y_sin_basura()
    {
        var ll = new[]
        {
            L(l => l.Obstaculos = "El cliente, molesto, no acepta | Acceso al sistema restringido"),
            L(l => l.Obstaculos = "Acceso al sistema restringido. | String"),
            L(l => l.Obstaculos = "Not applicable"),
            L(),
        };
        var o = CalculadoraGaia.Obstaculos(ll);
        Assert.Equal(2, o.Count);
        Assert.Equal("Acceso al sistema restringido", o[0].Texto);
        Assert.Equal(2, o[0].Cantidad);
        Assert.Equal(0.5, o[0].Fraccion);
        Assert.Equal("El cliente, molesto, no acepta", o[1].Texto);
    }

    [Fact]
    public void Arbol_de_motivos_con_participacion_por_nivel()
    {
        var ll = new[]
        {
            L(l => { l.Motivo1 = "solicitud"; l.Motivo2 = "baja"; l.Motivo3 = "bajaVoluntaria"; }),
            L(l => { l.Motivo1 = "solicitud"; l.Motivo2 = "baja"; l.Motivo3 = "portabilidad"; }),
            L(l => { l.Motivo1 = "solicitud"; l.Motivo2 = "comercial"; l.Motivo3 = "oferta"; }),
            L(l => { l.Motivo1 = "incidenciaOrReclamación"; l.Motivo2 = "facturacionOrCobros"; }),
            L(),
        };
        var arbol = CalculadoraGaia.ArbolMotivos(ll);
        Assert.Equal(["solicitud", "incidenciaOrReclamación", "Sin motivo"], arbol.Select(n => n.Clave));
        Assert.Equal(0.6, arbol[0].Participacion, 6);
        var baja = arbol[0].Hijos[0];
        Assert.Equal("baja", baja.Clave);
        Assert.Equal(2.0 / 3, baja.Participacion, 6);
        Assert.Equal(2, baja.Hijos.Count);
        Assert.Empty(arbol[2].Hijos);

        var mapa = CalculadoraGaia.PorMotivo(ll, 2);
        Assert.Equal("baja", mapa[0].Clave);
        Assert.Equal(3, mapa.Count);
    }

    [Fact]
    public void Semanas_iso_empiezan_en_lunes()
    {
        var s = CalculadoraGaia.PorSemana([
            L(l => l.Fecha = new DateOnly(2026, 9, 7)),   // lunes, semana 37
            L(l => l.Fecha = new DateOnly(2026, 9, 13)),  // domingo, semana 37
            L(l => l.Fecha = new DateOnly(2026, 9, 14)),  // lunes, semana 38
        ]);
        Assert.Equal(2, s.Count);
        Assert.Equal("2026-W37", s[0].Clave);
        Assert.Equal("Sem 37 · 07/09", s[0].Texto);
        Assert.Equal(2, s[0].Indicadores.Llamadas);
    }
}
