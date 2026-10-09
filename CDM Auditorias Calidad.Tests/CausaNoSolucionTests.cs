using CDM_Auditorias_Calidad.Servicios.NoSolucion;
using Xunit;

namespace CDM_Auditorias_Calidad.Tests;

/// <summary>
/// La causa de cada no solución de sinAccesoInternet (atención o proceso) y el control de llamadas
/// repetidas: extensiones de esta web (cubo v3) que no existen en el Python de referencia, así que
/// se prueban con un día hecho a mano en el que se sabe qué le toca a cada llamada.
/// </summary>
public sealed class CausaNoSolucionTests
{
    private const string Internet = "sinAccesoInternet";

    private static readonly Dictionary<string, int> Tabla = new()
    {
        ["tecnico"] = 1, ["agente"] = 32, ["cliente"] = 64, ["otro"] = 512,
    };

    private static List<int> Rubrica(int fallos)
        => Enumerable.Range(0, Ensamblador.ItemsRubrica + Ensamblador.ItemsCirculares).Select(i => i < fallos ? 0 : 1).ToList();

    private static FilaInternet Llamada(string id, string llamada, int res, string[] etiquetas, string cierre = "completed", int fallos = 0)
        => new("YOIGO", "AVERIAS", "Sup", "Tl", "Agente", "N4", "N5", res, 0, 0, 0, "NA", cierre,
               etiquetas.ToList(), Rubrica(fallos), id, llamada);

    /// <summary>
    /// Siete conversaciones de un día: c6 es otro tramo de la misma llamada física que c5 (y la única
    /// solucionada, así que la mediana de ítems fallados es 0).
    /// </summary>
    private static Cubo CuboDePrueba()
    {
        var internet = new List<FilaInternet>
        {
            Llamada("c1", "L1", 2, ["tecnico"]),                         // proceso
            Llamada("c2", "L2", 2, ["tecnico", "agente"]),               // proceso con fallos de atención
            Llamada("c3", "L3", 2, [], cierre: "abruptlyEnded"),         // atención (cierre abrupto)
            Llamada("c4", "L4", 2, ["cliente"]),                         // cliente
            Llamada("c5", "L5", 2, ["otro"]),                            // sin causa
            Llamada("c6", "L5", 1, []),                                  // solucionada, tramo de L5
            Llamada("c7", "L7", 2, [], fallos: 1),                       // atención (rúbrica)
        };
        var doc = new DocCrudo
        {
            Dia = "2026-09-01",
            Extraido = "2026-09-02T06:00:00",
            Base = [new FilaBase("YOIGO", "Inbound", "AVERIAS", "Sup", "Tl", "Agente", "N2", Internet, 1, 7, 1, 6)],
            Internet = internet,
            Llamadas = internet.Where(r => r.Res == 2)
                .Select(r => new FilaLlamada("YOIGO", "AVERIAS", "Sup", "Tl", "Agente", "N2", Internet, "N4",
                                             r.Etiquetas, r.Id, r.Llamada + "_1", "Texto de " + r.Id))
                .ToList(),
        };
        return Ensamblador.Ensamblar([doc], Tabla, diasProvisionales: 0);
    }

    [Theory]
    [InlineData(1, 0, CausaNoSolucion.Proceso)]
    [InlineData(1 | 32, 0, CausaNoSolucion.ProcesoYAtencion)]
    [InlineData(4, CausaNoSolucion.SenalRubrica, CausaNoSolucion.ProcesoYAtencion)]
    [InlineData(0, CausaNoSolucion.SenalCierreAbrupto, CausaNoSolucion.Atencion)]
    [InlineData(32, 0, CausaNoSolucion.Atencion)]
    [InlineData(64 | 32, 0, CausaNoSolucion.Atencion)]
    [InlineData(64, 0, CausaNoSolucion.Cliente)]
    [InlineData(128 | 512, 0, CausaNoSolucion.Cliente)]
    [InlineData(512, 0, CausaNoSolucion.SinCausa)]
    [InlineData(0, 0, CausaNoSolucion.SinCausa)]
    public void Cada_llamada_va_a_una_sola_causa(int mascara, int senales, string esperada)
        => Assert.Equal(esperada, CausaNoSolucion.Clave(mascara, senales));

    /// <summary>Etiquetas reales que la tabla del 10/09 no conocía (BigQuery, 05-09 a 04-10-2026).</summary>
    [Theory]
    [InlineData("Fallo De La Infraestructura De Red Externa", 2)]
    [InlineData("Avería Externa Requiere Reparación", 2)]
    [InlineData("Resolver El Corte Generalizado en La Zona Del Cliente", 2)]
    [InlineData("Escalar Al Departamento Especializado", 4)]
    [InlineData("Requiere Investigación De Segundo Nivel", 4)]
    [InlineData("Se Requiere Equipo Especializado", 4)]
    [InlineData("Programar Una Cita Con El Técnico", 1)]
    [InlineData("Visita De Técnico Requerida", 1)]
    [InlineData("Asegurar La Disponibilidad Del Cliente Para El Técnico", 1)]
    [InlineData("Plazo De Entrega Del Equipo", 16)]
    [InlineData("Diagnosticar La Causa Raíz Del Problema Del Router", 512)]   // diagnosticar es de N1: no se adivina
    [InlineData("Coordinar La Entrega Del Router De Reemplazo", 16)]
    [InlineData("Hardware Defectuoso Del Router", 16)]
    [InlineData("Incidente Masivo en Investigación", 2)]
    [InlineData("Requiere Visita a Tienda Física", 512)]                       // tienda no es técnico
    [InlineData("El Cliente Rechazó La Visita Del Técnico", 64)]
    [InlineData("El Cliente No Está Físicamente Presente en La Ubicación Para Completar La Configuración", 64)]
    [InlineData("El Cliente Aplazó La Resolución Del Problema", 64)]
    [InlineData("Cliente Dependiente De Movistar", 512)]                      // «dependiente» no es «pendiente»
    [InlineData("Falta De Autoridad Del Agente", 4)]                          // permisos: proceso, no atención
    [InlineData("Devolución De Llamada Pendiente Del Agente", 32)]
    [InlineData("Preocupación Por Comunicación Engañosa", 128)]
    [InlineData("Completar Instalación De Fibra", 8)]
    [InlineData("Daño en El Cable De Fibra Externo", 2)]
    [InlineData("Retraso en La Entrega De SMS", 512)]
    [InlineData("Retraso en La Reparación Técnica", 8)]
    [InlineData("Falta De Un Cronograma Definitivo O Un Plan De Acción Para La Resolución Del Problema", 8)]
    [InlineData("Información Contradictoria Proporcionada", 32)]
    [InlineData("Proceso De Solución De Problemas Incompleto", 32)]
    [InlineData("Falta De Comprensión Del Agente Sobre El Proceso De Cancelación", 32)]
    [InlineData("Cliente No en Casa", 64)]
    [InlineData("Insatisfacción Con El Servicio Al Cliente", 128)]
    [InlineData("El Cliente Necesita Servicio Inmediato", 512)]   // «necesita» no es «cita»
    [InlineData("Falta De Acceso Para Modificar Los Detalles De La Cuenta", 512)]
    public void Una_etiqueta_desconocida_va_por_palabras_clave(string etiqueta, int bit)
        => Assert.Equal(bit, ClasificadorEtiquetas.Bit(etiqueta));

    [Fact]
    public void La_tabla_manda_sobre_las_palabras_clave()
    {
        var doc = new DocCrudo
        {
            Dia = "2026-09-01",
            Base = [new FilaBase("YOIGO", "Inbound", "AVERIAS", "Sup", "Tl", "Agente", "N2", Internet, 1, 2, 0, 2)],
            Internet =
            [
                Llamada("c1", "L1", 2, ["Visita de técnico conocida"]),   // en la tabla: «otro»
                Llamada("c2", "L2", 2, ["Visita De Técnico Requerida"]), // nueva: técnico por palabras
            ],
        };
        var tabla = new Dictionary<string, int>(Tabla) { ["Visita de técnico conocida"] = 512 };
        var cubo = Ensamblador.Ensamblar([doc], tabla, diasProvisionales: 0);

        var masks = cubo.Dias["2026-09-01"].Ver!.ToDictionary(r => r[3], r => r[6]);
        Assert.Equal(1, masks[512]);
        Assert.Equal(1, masks[1]);
        Assert.Equal(1, cubo.Meta.EtiquetasNuevas);
        Assert.Equal(1, cubo.Meta.EtiquetasPorPalabras);
    }

    [Fact]
    public void El_cubo_v3_guarda_senales_causas_y_repetidas()
    {
        var cubo = CuboDePrueba();
        var dia = cubo.Dias["2026-09-01"];

        Assert.Equal(3, cubo.Meta.VersionCubo);
        Assert.Equal(6, dia.Causas!.Count);   // solo las no solucionadas
        Assert.Equal(new[] { CausaNoSolucion.SenalCierreAbrupto, 0 }, dia.Causas["c3"]);
        Assert.Equal(new[] { CausaNoSolucion.SenalRubrica, 1 }, dia.Causas["c7"]);
        // [b, registros, conversaciones, llamadas, registros_nosol, conversaciones_nosol, llamadas_nosol]
        Assert.Equal(new long[] { 0, 7, 7, 6, 6, 6, 6 }, Assert.Single(dia.Dup!));
        Assert.Equal(7, dia.Ver!.Sum(r => r[6]));
    }

    [Fact]
    public void Las_causas_suman_las_no_solucionadas_y_cuadran_con_los_cuadrantes()
    {
        var r = Agregados.Internet(CuboDePrueba(), new Filtros(90));
        var causas = r.Causas!.ToDictionary(c => c.Clave, c => c.Nosol);

        Assert.Equal(1, causas[CausaNoSolucion.Proceso]);
        Assert.Equal(1, causas[CausaNoSolucion.ProcesoYAtencion]);
        Assert.Equal(2, causas[CausaNoSolucion.Atencion]);
        Assert.Equal(1, causas[CausaNoSolucion.Cliente]);
        Assert.Equal(1, causas[CausaNoSolucion.SinCausa]);
        Assert.Equal(r.Totales.Nosol, causas.Values.Sum());

        // «Atención» + «las dos» son las no solucionadas con señal de atención de los cuadrantes.
        var conAtencion = r.Cuadrantes.Where(q => q.Clave[0] == '1').Sum(q => q.Nosol);
        Assert.Equal(conAtencion, causas[CausaNoSolucion.Atencion] + causas[CausaNoSolucion.ProcesoYAtencion]);

        var detalleMixta = r.Causas!.First(c => c.Clave == CausaNoSolucion.ProcesoYAtencion).Detalle;
        Assert.Contains(detalleMixta, d => d.Grupo == "proceso" && d.Nombre == "Visita o envío de técnico");
        Assert.Contains(detalleMixta, d => d.Grupo == "atencion" && d.Nombre == "Impedimento atribuido al agente");
    }

    [Fact]
    public void Que_frena_el_proceso_cuadra_las_llamadas_distintas()
    {
        var r = Agregados.Internet(CuboDePrueba(), new Filtros(90));
        var c = r.CuadreImpedimentos!;

        Assert.Equal(6, c.Nosol);
        Assert.Equal(4, c.ConImpedimento);          // c1, c2, c4, c5
        Assert.Equal(2, c.SinImpedimento);          // c3, c7
        Assert.Equal(1, c.ConVarios);               // c2: técnico y agente
        Assert.Equal(5, c.SumaFilas);               // técnico 2 + agente 1 + cliente 1 + otro 1
        Assert.Equal(c.SumaFilas, r.Impedimentos.Sum(x => x.Nosol));

        var tecnico = r.Impedimentos.First(x => x.Bit == 1);
        Assert.Equal(1, tecnico.SoloEste);
        Assert.Equal("Atención o seguimiento del agente", Assert.Single(tecnico.Acompanan!).Nombre);

        var rep = r.Repetidas!;
        Assert.Equal((7, 7, 6), (rep.Registros, rep.Conversaciones, rep.Llamadas));
        Assert.Equal((6, 6, 6), (rep.RegistrosNosol, rep.ConversacionesNosol, rep.LlamadasNosol));
    }

    [Fact]
    public void Ejemplos_y_csv_por_causa()
    {
        var cubo = CuboDePrueba();
        var f = new Filtros(90);

        var atencion = Agregados.MuestrasCausa(cubo, CausaNoSolucion.Atencion, f);
        Assert.Equal(2, atencion.Total);
        Assert.All(atencion.Muestras, m => Assert.Equal("Atención", m.Causa));

        var (nombre, texto) = Agregados.CsvCausas(cubo, f);
        Assert.StartsWith("nosolucion_internet_causas_", nombre);
        var lineas = texto.TrimEnd().Split("\r\n");
        Assert.Equal(1 + 6, lineas.Length);
        Assert.Contains(lineas, l => l.StartsWith("c7;") && l.Contains("rúbrica: 1 de 7 ítems fallados"));
        Assert.Contains(lineas, l => l.StartsWith("c3;") && l.Contains("cierre abrupto"));

        Assert.Throws<FiltroInvalido>(() => Agregados.MuestrasCausa(cubo, "inventada", f));
    }

    [Fact]
    public void Con_un_cubo_v2_no_hay_causas_ni_repetidas()
    {
        var cubo = CuboDePrueba();
        foreach (var d in cubo.Dias.Values)
        {
            d.Ver = null;
            d.Dup = null;
            d.Causas = null;
        }
        var r = Agregados.Internet(cubo, new Filtros(90));
        Assert.Null(r.Causas);
        Assert.Null(r.CuadreImpedimentos);
        Assert.Null(r.Repetidas);
        Assert.All(r.Impedimentos, x => Assert.Null(x.SoloEste));
    }
}
