using System.Globalization;

namespace CDM_Auditorias_Calidad.Servicios.Comun;

/// <summary>
/// Convierte valores a texto (y redondea, ordena y hace <c>repr</c>) igual
/// que el backend en Python, para que las respuestas coincidan.
/// </summary>
public static class FormatoPython
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// <c>str(datetime)</c>: <c>AAAA-MM-DD HH:MM:SS[.ffffff]</c>.
    /// </summary>
    public static string StrDatetime(DateTime d) => Fecha(d, ' ');

    /// <summary>
    /// <c>datetime.isoformat()</c>: igual, con una <c>T</c> entre la fecha y la hora.
    /// </summary>
    public static string Isoformat(DateTime d) => Fecha(d, 'T');

    private static string Fecha(DateTime d, char separador)
    {
        // Microsegundos (seis cifras), como Python; .NET guarda siete.
        var microsegundos = (d.Ticks % TimeSpan.TicksPerSecond) / 10;
        var texto = d.ToString("yyyy-MM-dd", Inv) + separador + d.ToString("HH:mm:ss", Inv);
        return microsegundos == 0 ? texto : texto + "." + microsegundos.ToString("D6", Inv);
    }

    /// <summary>
    /// Un legajo que llega de SQL como <c>float</c>, tal y como lo deja el
    /// original: <c>str(2023920.0)</c> es <c>'2023920.0'</c> y después se le
    /// recorta el <c>.0</c> (ver <c>normalizar_legajo</c> en absentismo.py).
    /// </summary>
    public static string LegajoDesdeFloat(double valor)
    {
        if (Math.Abs(valor) < 1e16 && valor == Math.Truncate(valor))
        {
            return ((long)valor).ToString(Inv);
        }
        return valor.ToString("R", Inv);
    }

    /// <summary>
    /// <c>str(int(valor))</c>: truncar hacia cero y escribir el entero.
    /// </summary>
    public static string StrInt(double valor) => ((long)Math.Truncate(valor)).ToString(Inv);

    /// <summary>
    /// <c>int(valor)</c> sobre un número que puede venir como entero,
    /// decimal o flotante de SQL.
    /// </summary>
    public static int Int(object? valor)
    {
        if (valor is null || valor is DBNull) return 0;
        return (int)Math.Truncate(Convert.ToDouble(valor, Inv));
    }

    /// <summary>
    /// <c>float(valor) if valor is not None else 0</c>.
    /// </summary>
    public static double Float(object? valor)
    {
        if (valor is null || valor is DBNull) return 0;
        return Convert.ToDouble(valor, Inv);
    }

    /// <summary>
    /// <c>round(x, digitos)</c> de Python: redondea el valor binario
    /// <b>exacto</b> a la par (half-even).
    /// </summary>
    /// <remarks>
    /// Descompone el double en mantisa y exponente y redondea con enteros
    /// grandes, así que en los empates exactos (0,03125 a cuatro cifras)
    /// redondea a la par, donde <c>Math.Round</c> redondearía hacia arriba.
    /// </remarks>
    public static double Round(double x, int digitos)
    {
        if (double.IsNaN(x) || double.IsInfinity(x) || x == 0) return x;

        var bits = BitConverter.DoubleToInt64Bits(x);
        var negativo = bits < 0;
        var exponente = (int)((bits >> 52) & 0x7FF);
        var mantisa = bits & 0xFFFFFFFFFFFFFL;
        if (exponente == 0) exponente++; else mantisa |= 1L << 52;
        exponente -= 1075; // |x| = mantisa * 2^exponente, exacto

        var numerador = new System.Numerics.BigInteger(mantisa) * System.Numerics.BigInteger.Pow(10, digitos);
        var denominador = System.Numerics.BigInteger.One;
        if (exponente > 0) numerador <<= exponente; else denominador <<= -exponente;

        var cociente = System.Numerics.BigInteger.DivRem(numerador, denominador, out var resto);
        var comparacion = (resto * 2).CompareTo(denominador);
        if (comparacion > 0 || (comparacion == 0 && !cociente.IsEven)) cociente += 1;

        var digitosTexto = cociente.ToString(Inv).PadLeft(digitos + 1, '0');
        var texto = digitos == 0
            ? digitosTexto
            : digitosTexto[..^digitos] + "." + digitosTexto[^digitos..];
        var valor = double.Parse(texto, Inv);
        return negativo ? -valor : valor;
    }

    /// <summary>
    /// El orden de <c>sorted()</c> de Python sobre textos: ordinal, sin reglas
    /// de idioma.
    /// </summary>
    public static readonly StringComparer Ordinal = StringComparer.Ordinal;

    /// <summary>
    /// <c>str.lower()</c>: suficiente para nombres en español.
    /// </summary>
    public static string Lower(string texto) => texto.ToLowerInvariant();

    /// <summary>
    /// <c>str.upper()</c>.
    /// </summary>
    public static string Upper(string texto) => texto.ToUpperInvariant();

    /// <summary>
    /// <c>repr()</c> de un texto, carácter a carácter como CPython.
    /// </summary>
    /// <remarks>
    /// Comillas simples, salvo que el texto lleve una simple y ninguna
    /// doble. Se escapan la barra, la comilla elegida, <c>\t \n \r</c> y los
    /// caracteres no imprimibles (<c>str.isprintable</c>: categorías Cc, Cf,
    /// Cs, Co, Cn, Zl, Zp y Zs salvo el espacio), como <c>\xa0</c> o
    /// <c>\u200b</c>. Los acentos y la eñe quedan tal cual. Se usa en las
    /// claves de caché y el ETag de No solución.
    /// </remarks>
    public static string Repr(string? texto)
    {
        if (texto is null) return "None";
        var comilla = texto.Contains('\'') && !texto.Contains('"') ? '"' : '\'';
        var sb = new System.Text.StringBuilder(texto.Length + 2);
        sb.Append(comilla);
        foreach (var r in texto.EnumerateRunes())
        {
            var v = r.Value;
            if (v == comilla || v == '\\') sb.Append('\\').Append((char)v);
            else if (v == '\t') sb.Append("\\t");
            else if (v == '\n') sb.Append("\\n");
            else if (v == '\r') sb.Append("\\r");
            else if (v < 0x20 || v == 0x7F) sb.Append("\\x").Append(v.ToString("x2", Inv));
            else if (v < 0x7F) sb.Append((char)v);
            else if (EsImprimible(r)) sb.Append(r.ToString());
            else if (v <= 0xFF) sb.Append("\\x").Append(v.ToString("x2", Inv));
            else if (v <= 0xFFFF) sb.Append("\\u").Append(v.ToString("x4", Inv));
            else sb.Append("\\U").Append(v.ToString("x8", Inv));
        }
        sb.Append(comilla);
        return sb.ToString();
    }

    private static bool EsImprimible(System.Text.Rune r) => System.Text.Rune.GetUnicodeCategory(r) switch
    {
        UnicodeCategory.Control or UnicodeCategory.Format or UnicodeCategory.Surrogate
            or UnicodeCategory.PrivateUse or UnicodeCategory.OtherNotAssigned
            or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator
            or UnicodeCategory.SpaceSeparator => false,
        _ => true,
    };

    /// <summary>
    /// <c>repr()</c> de una tupla, que es lo que el backend en Python usa
    /// como llave de caché y como base del ETag.
    /// </summary>
    /// <remarks>
    /// Cada parte puede ser <c>null</c> (<c>None</c>), un texto, un entero,
    /// un booleano (<c>True</c>/<c>False</c>) o una lista de textos, que se
    /// escribe como tupla anidada. La tupla de un solo elemento lleva la coma
    /// final: <c>('YOIGO',)</c>.
    /// </remarks>
    public static string ReprTupla(IEnumerable<object?> partes)
    {
        var textos = partes.Select(ReprValor).ToList();
        return textos.Count == 1 ? "(" + textos[0] + ",)" : "(" + string.Join(", ", textos) + ")";
    }

    private static string ReprValor(object? v) => v switch
    {
        null => "None",
        string s => Repr(s),
        bool b => b ? "True" : "False",
        int i => i.ToString(Inv),
        long l => l.ToString(Inv),
        IEnumerable<string> lista => ReprTupla(lista),
        _ => throw new ArgumentException("Sin repr de Python para " + v.GetType().Name, nameof(v)),
    };

    /// <summary>
    /// Un <c>float</c> tal y como lo escribe <c>json.dumps</c>: <c>12.0</c>,
    /// no <c>12</c>.
    /// </summary>
    /// <remarks>
    /// System.Text.Json escribe un double entero sin decimales; envuelto en
    /// este tipo sale como en Python.
    /// </remarks>
    [System.Text.Json.Serialization.JsonConverter(typeof(ConvertidorFloatPython))]
    public readonly record struct FloatPython(double Valor);

    private sealed class ConvertidorFloatPython : System.Text.Json.Serialization.JsonConverter<FloatPython>
    {
        public override FloatPython Read(ref System.Text.Json.Utf8JsonReader reader, Type typeToConvert, System.Text.Json.JsonSerializerOptions options)
            => new(reader.GetDouble());

        public override void Write(System.Text.Json.Utf8JsonWriter writer, FloatPython value, System.Text.Json.JsonSerializerOptions options)
        {
            var v = value.Valor;
            if (!double.IsFinite(v))
            {
                writer.WriteNumberValue(v);   // NaN e infinito: el error de siempre del serializador
                return;
            }
            var texto = v.ToString("R", Inv);
            if (!texto.Contains('.') && !texto.Contains('E')) texto += ".0";
            writer.WriteRawValue(texto.Replace('E', 'e'));
        }
    }
}
