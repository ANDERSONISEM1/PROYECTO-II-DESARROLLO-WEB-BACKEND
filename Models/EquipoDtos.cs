// ================================= Api.Models/EquipoDtos.cs ==============================
using System;
using System.Collections.Generic;

namespace Api.Models
{
    public class EquipoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string? Ciudad { get; set; }
        public string? Abreviatura { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }
        public string? LogoUrl { get; set; }
    }

    public class CreateEquipoRequest
    {
        public string Nombre { get; set; } = string.Empty;  // ← validamos único por nombre
        public string Ciudad { get; set; } = string.Empty;
        public string Abreviatura { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        public DateTime? FechaCreacion { get; set; }
        public string? LogoBase64 { get; set; }
    }

    public class UpdateEquipoRequest
    {
        public string Nombre { get; set; } = string.Empty;  // ← validamos único por nombre
        public string Ciudad { get; set; } = string.Empty;
        public string Abreviatura { get; set; } = string.Empty;
        public bool Activo { get; set; } = true;
        // null = no tocar; "" = borrar; valor = reemplazar
        public string? LogoBase64 { get; set; }
    }

    // ===== Para el modal de confirmación de borrado =====
    public class JugadorLiteDto
    {
        public int Id { get; set; }
        public string Nombres { get; set; } = string.Empty;
        public string Apellidos { get; set; } = string.Empty;
        public int? Dorsal { get; set; }
        public JugadorLiteDto() { }
    }

    public record PartidosResumenDto(
        int Total,
        int Programado,
        int EnCurso,
        int Finalizado,
        int Cancelado,
        int Suspendido
    );

    public record EquipoDeleteInfoDto(
        bool CanDelete,
        int TotalJugadores,
        IEnumerable<JugadorLiteDto> Jugadores,
        PartidosResumenDto Partidos
    );
}
