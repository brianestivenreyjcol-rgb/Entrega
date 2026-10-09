namespace CDM_Auditorias_Calidad.Servicios.Comun;

/// <summary>Un parámetro de la petición no es válido (periodo mal escrito, filtro inexistente). Se responde con 400.</summary>
public sealed class PeticionInvalida : Exception
{
    public PeticionInvalida(string mensaje) : base(mensaje) { }
}

/// <summary>Lo pedido no existe (un agente, un adjunto). Se responde con 404.</summary>
public sealed class NoEncontrado : Exception
{
    public NoEncontrado(string mensaje) : base(mensaje) { }
}
