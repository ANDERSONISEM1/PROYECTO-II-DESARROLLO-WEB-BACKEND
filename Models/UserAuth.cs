namespace Api.Models.Auth;

public sealed class UserAuth
{
    public long Id { get; set; }
    public string Usuario { get; set; } = "";
    public string? Correo { get; set; }
    public byte[] ContraseniaHash { get; set; } = Array.Empty<byte>(); // [salt||hash]
    public string AlgoritmoHash { get; set; } = ""; // ej: argon2id(v=19,m=65536,t=3,p=1,saltlen=16,hashlen=32)
    public bool Activo { get; set; }
    public DateTime? UltimoLogin { get; set; }
}
