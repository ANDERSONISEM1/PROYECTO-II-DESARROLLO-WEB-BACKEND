namespace Api.Models
{
    public class EquipoMini
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
        public string? Abreviatura { get; set; }
        public string? Color { get; set; }
    }

    // Marcador total por equipo
    public class Marcador
    {
        public int PartidoId { get; set; }
        public int Local { get; set; }
        public int Visitante { get; set; }
    }

    // ✅ Ajustar anotación (con soporte de cuarto)
    public class AjustarAnotacionRequest
    {
        public int  EquipoId { get; set; }
        public short Puntos { get; set; } // -3..-1 o 1..3
        public bool? EsCorreccion { get; set; } // (no se usa, pero lo dejamos por compat)

        // === Para llenar cuarto_id ===
        public int? CuartoId { get; set; }        // si ya lo conoces
        public int? NumeroCuarto { get; set; }    // alternativa si no tienes CuartoId
        public bool? EsProrroga { get; set; }     // default false si no viene
    }
}
