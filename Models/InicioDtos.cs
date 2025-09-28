namespace Api.Models
{
    public record InicioKpisDto(
        int TotalEquipos,
        int TotalJugadores,
        int PartidosPendientes
    );

    public record ProximoPartidoDto(
        int Id,
        int EquipoLocalId,
        int EquipoVisitanteId,
        DateTime? FechaHoraInicio,
        string? Sede,
        string Estado
    );
}
