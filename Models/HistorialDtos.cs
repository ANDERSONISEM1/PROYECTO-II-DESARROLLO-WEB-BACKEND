namespace Api.Models
{
    public record PartidoHistDto(
        int Id,
        int EquipoLocalId,
        int EquipoVisitanteId,
        DateTime? FechaHoraInicio,
        string? Sede,
        string Estado,
        int PuntosLocal,
        int PuntosVisitante
    );
}
