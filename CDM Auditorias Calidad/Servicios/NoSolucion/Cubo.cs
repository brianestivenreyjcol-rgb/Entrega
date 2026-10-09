using System.Text.Json;
using System.Text.Json.Serialization;

namespace CDM_Auditorias_Calidad.Servicios.NoSolucion;

/// <summary>
/// El cubo de No solución (formato v3): un documento por día con siete
/// cortes, más tres de esta web (v3). Es el contenido de <c>cache_nosolucion.json</c>, el fichero que
/// comparten los dos backends.
/// </summary>
/// <remarks>
/// <para>
/// Todos los cortes llevan marca, servicio y agente en las tres primeras
/// columnas, para que todos los filtros se apliquen igual a todas las vistas:
/// </para>
/// <code>
/// eq   [b, s, a,                                              ll, sol, nosol]
/// tp   [b, s, a, n2, n3,                                      ll, sol, nosol]
/// net  [b, s, a, n4, n5, ticket, escbo, rell, evento, cierre, res,  n, nosol]
/// imp  [b, s, a, bit, res,                                    n]
/// rub  [b, s, a, item, valor, res,                            n]
/// cuad [b, s, a, aten, proc, res,                             n]
/// nos  [b, s, a, n2, n3, mascara, id, id_externo, n4, texto]
/// ver  [b, s, a, mascara, senales, res,                       n]   (v3, solo esta web)
/// dup  [b, filas, conversaciones, llamadas, filas_nosol, conversaciones_nosol, llamadas_nosol]   (v3)
/// causas { conversationId: [senales, fallos] }                       (v3)
/// </code>
/// <para>
/// <c>ver</c>, <c>dup</c> y <c>causas</c> no existen en el Python de referencia: los añadió esta
/// web el 05-10-2026 para decir si cada no solución de sinAccesoInternet fue de atención o de
/// proceso (<see cref="CausaNoSolucion"/>) y para comprobar que no hay llamadas repetidas. En
/// un cubo v2 vienen nulos y las vistas los omiten.
/// </para>
/// <para>
/// <c>nos</c> son todas las llamadas entrantes no solucionadas, una por fila,
/// con su id y el resumen: de ahí salen los ejemplos y el CSV.
/// </para>
/// <para>
/// El supervisor y el TL salen del agente con <c>meta.amap</c>. Las filas
/// llevan índices de <c>meta.dic</c>; los agregados los cambian por nombres
/// antes de responder.
/// </para>
/// </remarks>
public sealed class Cubo
{
    [JsonPropertyName("meta")] public MetaCubo Meta { get; set; } = new();
    [JsonPropertyName("dias")] public Dictionary<string, DocDia> Dias { get; set; } = new();
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class MetaCubo
{
    [JsonPropertyName("generado")] public string? Generado { get; set; }
    [JsonPropertyName("fuente")] public string? Fuente { get; set; }
    [JsonPropertyName("proyecto")] public string? Proyecto { get; set; }
    [JsonPropertyName("origen")] public string? Origen { get; set; }

    /// <summary>Versión del formato (ver <see cref="Ensamblador.VersionCubo"/>).</summary>
    [JsonPropertyName("version_cubo")] public int? VersionCubo { get; set; }

    [JsonPropertyName("dia_min")] public string? DiaMin { get; set; }
    [JsonPropertyName("dia_max")] public string? DiaMax { get; set; }
    [JsonPropertyName("ventana")] public int Ventana { get; set; }
    [JsonPropertyName("dias")] public List<string> Dias { get; set; } = new();
    [JsonPropertyName("dias_parciales")] public List<string> DiasParciales { get; set; } = new();
    [JsonPropertyName("dias_provisionales")] public List<string>? DiasProvisionales { get; set; }
    [JsonPropertyName("cruce")] public double? Cruce { get; set; }
    [JsonPropertyName("ctrl")] public Dictionary<string, long>? Ctrl { get; set; }
    [JsonPropertyName("por_direccion")] public Dictionary<string, Dictionary<string, long>>? PorDireccion { get; set; }
    [JsonPropertyName("suelo")] public Dictionary<string, int>? Suelo { get; set; }
    [JsonPropertyName("dic")] public Dictionary<string, List<string>> Dic { get; set; } = new();

    /// <summary>Por agente: <c>[supervisor, tl]</c>, como índices de <c>dic.u</c> y <c>dic.t</c>.</summary>
    [JsonPropertyName("amap")] public List<int[]> Amap { get; set; } = new();

    [JsonPropertyName("rub_nom")] public List<string> RubNom { get; set; } = new();
    [JsonPropertyName("imped_cats")] public List<CategoriaImpedimento>? ImpedCats { get; set; }
    [JsonPropertyName("umbral_atencion")] public int? UmbralAtencion { get; set; }
    [JsonPropertyName("umbral_base")] public int? UmbralBase { get; set; }
    [JsonPropertyName("etiquetas_nuevas")] public int? EtiquetasNuevas { get; set; }

    /// <summary>De las nuevas, cuántas se clasificaron por palabras clave en vez de ir a «Otro» (v3).</summary>
    [JsonPropertyName("etiquetas_por_palabras"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? EtiquetasPorPalabras { get; set; }
    [JsonPropertyName("cuadre")] public List<long>? Cuadre { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    private static readonly List<string> Vacia = new();

    /// <summary>Un diccionario de nombres, o vacío si no existe.</summary>
    public List<string> D(string clave) => Dic.TryGetValue(clave, out var l) ? l : Vacia;
}

public sealed class CategoriaImpedimento
{
    [JsonPropertyName("bit")] public int Bit { get; set; }
    [JsonPropertyName("nom")] public string Nom { get; set; } = "";
    [JsonPropertyName("tipo")] public string? Tipo { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class DocDia
{
    [JsonPropertyName("d")] public string D { get; set; } = "";
    [JsonPropertyName("parcial")] public bool Parcial { get; set; }
    [JsonPropertyName("extraido")] public string? Extraido { get; set; }
    [JsonPropertyName("tot")] public long[] Tot { get; set; } = new long[3];
    [JsonPropertyName("out")] public long[] Out { get; set; } = new long[3];
    [JsonPropertyName("eq")] public List<int[]> Eq { get; set; } = new();
    [JsonPropertyName("tp")] public List<int[]> Tp { get; set; } = new();
    [JsonPropertyName("net")] public List<int[]> Net { get; set; } = new();
    [JsonPropertyName("imp")] public List<int[]> Imp { get; set; } = new();
    [JsonPropertyName("rub")] public List<int[]> Rub { get; set; } = new();
    [JsonPropertyName("cuad")] public List<int[]> Cuad { get; set; } = new();

    /// <summary>Las no solucionadas del día, una a una, ordenadas por (id, id_externo).</summary>
    [JsonPropertyName("nos")] public List<FilaNos> Nos { get; set; } = new();

    /// <summary>sinAccesoInternet por combinación exacta de impedimentos y señales de atención (v3).</summary>
    [JsonPropertyName("ver"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<int[]>? Ver { get; set; }

    /// <summary>Registros de sinAccesoInternet frente a conversaciones y llamadas distintas, por marca (v3).</summary>
    [JsonPropertyName("dup"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<long[]>? Dup { get; set; }

    /// <summary>Señales de atención y ítems fallados de cada no solucionada de sinAccesoInternet, por conversationId (v3).</summary>
    [JsonPropertyName("causas"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, int[]>? Causas { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }

    /// <summary>Un corte de enteros por nombre, como <c>doc.get(campo) or ()</c>.</summary>
    public List<int[]> Corte(string campo) => campo switch
    {
        "eq" => Eq,
        "tp" => Tp,
        "net" => Net,
        "imp" => Imp,
        "rub" => Rub,
        "cuad" => Cuad,
        "ver" => Ver ?? new List<int[]>(),
        _ => throw new ArgumentException("Corte desconocido: " + campo, nameof(campo)),
    };
}

/// <summary>
/// Una llamada entrante no solucionada:
/// <c>[b, s, a, n2, n3, mascara, id, id_externo, n4, texto]</c>.
/// </summary>
/// <remarks>
/// En el JSON es una lista mixta (seis enteros y cuatro textos), así que va
/// con su propio conversor. <c>Id</c> es el <c>conversationId</c>, el único
/// id que se enseña y se exporta; <c>IdExterno</c> solo desempata el orden.
/// </remarks>
[JsonConverter(typeof(ConvertidorFilaNos))]
public sealed record FilaNos(
    int B, int S, int A, int N2, int N3, int Mascara,
    string Id, string IdExterno, string N4, string Texto);

internal sealed class ConvertidorFilaNos : JsonConverter<FilaNos>
{
    public override FilaNos Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            throw new JsonException("Una fila de 'nos' tiene que ser una lista.");
        }

        var enteros = new int[6];
        var textos = new string[4];
        var i = 0;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            if (i < 6)
            {
                enteros[i] = reader.GetInt32();
            }
            else if (i < 10)
            {
                // Un nulo se lee como texto vacío.
                textos[i - 6] = reader.TokenType == JsonTokenType.Null ? "" : reader.GetString() ?? "";
            }
            else
            {
                reader.Skip();
            }
            i++;
        }
        if (i < 10) throw new JsonException($"Una fila de 'nos' tiene {i} campos y hacen falta 10.");
        return new FilaNos(enteros[0], enteros[1], enteros[2], enteros[3], enteros[4], enteros[5],
                           textos[0], textos[1], textos[2], textos[3]);
    }

    public override void Write(Utf8JsonWriter writer, FilaNos f, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(f.B);
        writer.WriteNumberValue(f.S);
        writer.WriteNumberValue(f.A);
        writer.WriteNumberValue(f.N2);
        writer.WriteNumberValue(f.N3);
        writer.WriteNumberValue(f.Mascara);
        writer.WriteStringValue(f.Id);
        writer.WriteStringValue(f.IdExterno);
        writer.WriteStringValue(f.N4);
        writer.WriteStringValue(f.Texto);
        writer.WriteEndArray();
    }
}
