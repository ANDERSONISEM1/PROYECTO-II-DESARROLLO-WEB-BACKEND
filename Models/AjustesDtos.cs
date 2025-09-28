using System;

namespace Api.Models
{
    public sealed class RolDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = "";
    
    }

    public sealed class UsuarioDto
    {
        public long Id { get; set; }
        public string Usuario { get; set; } = "";
        public string PrimerNombre { get; set; } = "";
        public string? SegundoNombre { get; set; }
        public string PrimerApellido { get; set; } = "";
        public string? SegundoApellido { get; set; }
        public string? Correo { get; set; }
        public bool Activo { get; set; }
        public int RolId { get; set; }
        public string RolNombre { get; set; } = ""; // join
    }

    public sealed class CrearUsuarioRequest
    {
        public string Usuario { get; set; } = "";
        public string Password { get; set; } = "";
        public string PrimerNombre { get; set; } = "";
        public string? SegundoNombre { get; set; }
        public string PrimerApellido { get; set; } = "";
        public string? SegundoApellido { get; set; }
        public string? Correo { get; set; }
        public int RolId { get; set; }
    }

    public sealed class EditarUsuarioRequest
    {
        public string Usuario { get; set; } = "";
        public string PrimerNombre { get; set; } = "";
        public string? SegundoNombre { get; set; }
        public string PrimerApellido { get; set; } = "";
        public string? SegundoApellido { get; set; }
        public string? Correo { get; set; }
        public int RolId { get; set; }
    }

    public sealed class ResetPasswordRequest
    {
        public long UserId { get; set; }
        public string Password { get; set; } = "";
        public bool RotarSesion { get; set; } = true;
    }

    public sealed class ToggleActivoRequest
    {
        public bool Activo { get; set; }
    }
}
