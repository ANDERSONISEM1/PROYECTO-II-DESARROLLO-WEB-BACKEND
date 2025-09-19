namespace Api.Models;

public class AjusteTiempoMuertoDto
{
    public int EquipoId { get; set; }
    /// <summary>"corto" | "largo"</summary>
    public string Tipo { get; set; } = "corto";
    /// <summary>Si lo tienes directamente.</summary>
    public int? CuartoId { get; set; }
    /// <summary>Alternativa: dime el número de cuarto actual (1..N) y si es prórroga.</summary>
    public int? NumeroCuarto { get; set; }
    public bool? EsProrroga { get; set; }

    /// <summary>+1 para agregar, -1 para deshacer el último de ese tipo</summary>
    public int Delta { get; set; }
}

public class TeamTimeoutsDto
{
    public int    EquipoId      { get; set; }
    public string? EquipoNombre { get; set; }
    public int    Cortos        { get; set; }
    public int    Largos        { get; set; }
    public int    Total         { get; set; }
}

public class TiemposMuertosResumenDto
{
    public int PartidoId       { get; set; }
    public TeamTimeoutsDto Local     { get; set; } = new TeamTimeoutsDto();
    public TeamTimeoutsDto Visitante { get; set; } = new TeamTimeoutsDto();
}
