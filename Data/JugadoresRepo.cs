// Api/Data/JugadoresRepo.cs
using Dapper;
using Api.Models;

namespace Api.Data;

public class JugadoresRepo
{
    private readonly Db _db;
    public JugadoresRepo(Db db) => _db = db;

    public async Task<IEnumerable<dynamic>> GetJugadoresPorEquipoAsync(int equipoId, bool soloActivos = true)
    {
        const string sql = @"
SELECT
    j.jugador_id AS Jugador_Id,
    j.equipo_id  AS Equipo_Id,
    j.dorsal     AS Dorsal,
    j.nombres    AS Nombres,
    j.apellidos  AS Apellidos,
    j.posicion   AS Posicion,
    j.activo     AS Activo
FROM dbo.Jugador j
WHERE j.equipo_id = @equipoId
  AND (@soloActivos = 0 OR j.activo = 1)
ORDER BY 
    CASE WHEN j.dorsal IS NULL THEN 1 ELSE 0 END,
    j.dorsal,
    j.nombres,
    j.apellidos;";

        using var conn = _db.Open();
        var rows = await conn.QueryAsync<JugadorMini>(sql, new { equipoId, soloActivos });
        return rows.Select(r => new {
            jugador_id = r.Jugador_Id,
            equipo_id  = r.Equipo_Id,
            dorsal     = r.Dorsal,
            nombres    = r.Nombres,
            apellidos  = r.Apellidos,
            posicion   = r.Posicion,
            activo     = r.Activo
        });
    }
}
