namespace Api.Models
{
    public class JugadorDto
    {
        public int Id { get; set; }
        public int EquipoId { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;

        // Tipos alineados a SQL Server:
        // TINYINT -> byte?, SMALLINT -> short?
        public byte? Dorsal { get; set; }
        public string? Posicion { get; set; }
        public short? EstaturaCm { get; set; }
        public byte? Edad { get; set; }
        public string? Nacionalidad { get; set; }
        public bool Activo { get; set; }
    }

    public class CreateJugadorRequest
    {
        public int EquipoId { get; set; }
        public string Nombres { get; set; } = "";
        public string Apellidos { get; set; } = "";
        public int? Dorsal { get; set; }          // puedes dejar int? aquí; SQL lo castea sin problema
        public string? Posicion { get; set; }
        public short? EstaturaCm { get; set; }
        public byte? Edad { get; set; }
        public string? Nacionalidad { get; set; }
        public bool Activo { get; set; } = true;
    }

    public class UpdateJugadorRequest : CreateJugadorRequest {}
}
