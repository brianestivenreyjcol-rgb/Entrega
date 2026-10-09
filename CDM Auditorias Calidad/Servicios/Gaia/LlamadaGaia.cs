namespace CDM_Auditorias_Calidad.Servicios.Gaia;

/// <summary>
/// Una llamada de un agente en formación (una fila de <c>DatosBQ</c> en el PBI), ya traducida y con
/// los datos del agente sacados del Excel. Se guarda tal cual en la caché JSON.
/// </summary>
public sealed class LlamadaGaia
{
    // --- Identificación y momento ---
    public string IdConversacion { get; set; } = "";
    public string IdExterno { get; set; } = "";
    public DateOnly Fecha { get; set; }
    /// <summary>Hora de Bogotá.</summary>
    public DateTime FechaHora { get; set; }
    public string IdAgente { get; set; } = "";
    public string IdCliente { get; set; } = "";
    public string Marca { get; set; } = "";

    // --- Del Excel de nómina ---
    public string Agente { get; set; } = "";
    public string Sector { get; set; } = "";
    public string Supervisor { get; set; } = "";
    public string Coordinador { get; set; } = "";
    public string Formador { get; set; } = "";
    public string Oleada { get; set; } = "";
    /// <summary>«1 Preconexion» … «Aseguramiento 6»: qué día de formación era.</summary>
    public string TipoConexion { get; set; } = "";

    // --- Tiempos de la conversación ---
    public double? DuracionSegundos { get; set; }
    public double? TiempoNoHablado { get; set; }
    public double? PorcentajeHabla { get; set; }
    public double? VecesSePisaron { get; set; }

    // --- Clasificación de DataOrb (traducida) ---
    public string Contexto { get; set; } = "";
    public string RiesgoChurn { get; set; } = "";
    public string RazonNivel1 { get; set; } = "";
    public string TipoProblema { get; set; } = "";
    public string TipoConsulta { get; set; } = "";
    public string TemaContacto { get; set; } = "";
    public string GrupoResolucion { get; set; } = "";
    public string Motivo1 { get; set; } = "";
    public string Motivo2 { get; set; } = "";
    public string Motivo3 { get; set; } = "";
    public string SentimientoInicial { get; set; } = "";
    public string SentimientoFinal { get; set; } = "";
    public string EstadoResolucion { get; set; } = "";
    public string Obstaculos { get; set; } = "";
    public bool? ProblemaResuelto { get; set; }
    public string ResumenContacto { get; set; } = "";
    public string ResumenResolucion { get; set; } = "";

    // --- Calificaciones del estilo («yes», «no», «NA», «notApplicable»; sin traducir) ---
    public string CalificacionSaludo { get; set; } = "";
    public string CalificacionSolucion { get; set; } = "";
    public string CalificacionResumen { get; set; } = "";
    public string CalificacionCierre { get; set; } = "";
    public string CalificacionConfirmacion { get; set; } = "";
    public string CalificacionLenguajeClaro { get; set; } = "";
    public string CalificacionReconocimiento { get; set; } = "";

    // --- Indicadores corporativos ---
    public int? Transferencia { get; set; }
    public int? Rellamada72h { get; set; }
    /// <summary>Minutos hasta la siguiente llamada del cliente (<c>enh_minutos_posterior_callid</c>).</summary>
    public long? MinutosSiguienteLlamada { get; set; }
    /// <summary>Encuesta de solución: 1 resuelto, 2 no resuelto (0 o vacío, sin respuesta).</summary>
    public int? EncuestaSolucion { get; set; }
    public bool EncuestaEnviada { get; set; }

    // --- Comercial ---
    public bool? IntentoVenta { get; set; }
    public bool? PosibleVentaEntrante { get; set; }
    public string ResultadoVenta { get; set; } = "";
    public string CategoriaOferta { get; set; } = "";
    /// <summary>«true» / «false» tal como llega de DataOrb.</summary>
    public string AlineacionOferta { get; set; } = "";
    public bool? TieneVenta { get; set; }
    public string TipoServicioVenta { get; set; } = "";

    // --- Españolización ---
    /// <summary>La llamada está en la tabla de transcripciones (smartops).</summary>
    public bool TieneTranscripcion { get; set; }
    /// <summary>
    /// Índices de <see cref="PalabrasGaia.Palabras"/> que dijo el agente. Nulo si no hay transcripción o
    /// no se pudo saber qué hablante era el agente: esas llamadas no cuentan en la españolización.
    /// </summary>
    public List<int>? Palabras { get; set; }
}
