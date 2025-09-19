using System.Collections.Generic;

namespace Api.Models;

public class PlayerFoulsDto
{
    public int JugadorId { get; set; }
    public int? Dorsal { get; set; }
    public string Nombre { get; set; } = "";
    public string? Posicion { get; set; }
    public int Faltas { get; set; }
}

public class TeamFoulsDto
{
    public int EquipoId { get; set; }
    public string EquipoNombre { get; set; } = "";
    public List<PlayerFoulsDto> Jugadores { get; set; } = new();
    public List<PlayerFoulsDto> Fuera5 { get; set; } = new();
    public int TotalEquipo { get; set; }
}

public class FaltasResumenDto
{
    public int PartidoId { get; set; }
    public TeamFoulsDto Local { get; set; } = new();
    public TeamFoulsDto Visitante { get; set; } = new();
}

public class AjusteFaltaDto
{
    public int EquipoId { get; set; }
    public int JugadorId { get; set; }
    /// <summary>+1 o -1</summary>
    public int Delta { get; set; }

    /// <summary>Si ya conoces el ID del cuarto.</summary>
    public int? CuartoId { get; set; }

    /// <summary>Alternativa si no tienes CuartoId: número de cuarto y si es prórroga.</summary>
    public int? NumeroCuarto { get; set; }
    public bool? EsProrroga { get; set; }
}
