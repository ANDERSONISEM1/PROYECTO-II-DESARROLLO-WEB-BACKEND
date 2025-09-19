namespace Api.Models;

public class JugadorMini
{
    public int Jugador_Id { get; set; }
    public int Equipo_Id  { get; set; }
    public byte? Dorsal   { get; set; }
    public string? Nombres { get; set; }
    public string? Apellidos { get; set; }
    public string? Posicion  { get; set; }
    public bool Activo { get; set; }
}
