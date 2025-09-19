namespace Api.Models;

public record StartPartidoRequest
{
    public int  EquipoLocalId     { get; init; }
    public int  EquipoVisitanteId { get; init; }
    public int? MinutosPorCuarto  { get; init; } = 10;
    public int? CuartosTotales    { get; init; } = 4;
    public bool? LlenarRoster     { get; init; } = true;
}

public record StartPartidoResponse
{
    public int PartidoId { get; init; }
    public string Estado { get; init; } = "en_curso";
    public EquipoMiniDto Local { get; init; } = new();
    public EquipoMiniDto Visitante { get; init; } = new();
}

public record EquipoMiniDto
{
    public int Id { get; init; }
    public string Nombre { get; init; } = "";
    public string? Abreviatura { get; init; }
    public string? Color { get; init; }
}
