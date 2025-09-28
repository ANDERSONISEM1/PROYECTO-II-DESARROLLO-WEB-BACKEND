namespace Api.Models
{
    public class PartidoDto
    {
        public int Id { get; set; }
        public int EquipoLocalId { get; set; }
        public int EquipoVisitanteId { get; set; }
        public DateTime? FechaHoraInicio { get; set; }
        public string Estado { get; set; } = "programado";
        public int MinutosPorCuarto { get; set; }
        public int CuartosTotales { get; set; }
        public byte FaltasPorEquipoLimite { get; set; }
        public byte FaltasPorJugadorLimite { get; set; }
        public string? Sede { get; set; }
        public DateTime FechaCreacion { get; set; }
    }

  public class CreatePartidoRequest
{
    public int EquipoLocalId { get; set; }
    public int EquipoVisitanteId { get; set; }
    public DateTime? FechaHoraInicio { get; set; }
    public string Estado { get; set; } = "programado";
    public int MinutosPorCuarto { get; set; } = 10;
    public int CuartosTotales { get; set; } = 4;
    public byte FaltasPorEquipoLimite { get; set; } = 255; // 👈 255 = “sin límite”
    public byte FaltasPorJugadorLimite { get; set; } = 5;
    public string? Sede { get; set; }
}


    public class UpdatePartidoRequest : CreatePartidoRequest {}

    // ===== ROSTER =====
    public class RosterEntryDto
    {
        public int PartidoId { get; set; }
        public int EquipoId { get; set; }
        public int JugadorId { get; set; }
        public bool EsTitular { get; set; }
    }

    public class SaveRosterRequest
    {
        public int PartidoId { get; set; }
        public List<RosterEntryDto> Items { get; set; } = new();
    }
}
