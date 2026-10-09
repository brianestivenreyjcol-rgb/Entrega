namespace CDM_Auditorias_Calidad.Models;

/// <summary>
/// La cabecera común (guía de estilos, 4.1): logotipo a la izquierda, título y subtítulo en
/// el centro, botón de tema a la derecha.
/// </summary>
/// <param name="Titulo">Null en la portada: allí la cabecera solo lleva el logotipo.</param>
public sealed record CabeceraModelo(string? Titulo = null, string? Subtitulo = null);
