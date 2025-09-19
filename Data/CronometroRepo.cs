// Api/Data/CronometroRepo.cs
using Dapper;
using System.Threading.Tasks;

namespace Api.Data;

public class CronometroRepo
{
    private readonly Db _db;
    public CronometroRepo(Db db) => _db = db;

    private record PartidoCfg(int minutos_por_cuarto, int cuartos_totales);

    public async Task<(int localId, int visitId)> GetEquiposDelPartidoAsync(int partidoId)
    {
        using var conn = _db.Open();
        var p = await conn.QuerySingleOrDefaultAsync<(int equipo_local_id, int equipo_visitante_id)>(
            "SELECT equipo_local_id, equipo_visitante_id FROM dbo.Partido WHERE partido_id = @id",
            new { id = partidoId });
        return (p.equipo_local_id, p.equipo_visitante_id);
    }

    public async Task<int?> ResolveCuartoIdAsync(int partidoId, int numero, bool esProrroga)
    {
        using var conn = _db.Open();
        return await conn.QueryFirstOrDefaultAsync<int?>(@"
SELECT cuarto_id
FROM dbo.Cuarto
WHERE partido_id = @p AND numero = @n AND es_prorroga = @ot",
            new { p = partidoId, n = numero, ot = esProrroga ? 1 : 0 });
    }

    /// <summary>Garantiza que exista el cuarto y devuelve su ID. Crea prórroga si no existe.</summary>
    public async Task<int> EnsureCuartoAsync(int partidoId, int numero, bool esProrroga)
    {
        using var conn = _db.Open();
        var existing = await ResolveCuartoIdAsync(partidoId, numero, esProrroga);
        if (existing.HasValue) return existing.Value;

        var cfg = await conn.QueryFirstOrDefaultAsync<PartidoCfg>(
            "SELECT minutos_por_cuarto, cuartos_totales FROM dbo.Partido WHERE partido_id = @id",
            new { id = partidoId }) ?? throw new System.Exception("Partido no existe.");

        int dur = esProrroga ? 300 : cfg.minutos_por_cuarto * 60;

        var cuartoId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.Cuarto (partido_id, numero, es_prorroga, duracion_segundos, segundos_restantes, estado)
OUTPUT INSERTED.cuarto_id
VALUES (@p, @n, @ot, @dur, @dur, N'pendiente');",
            new { p = partidoId, n = numero, ot = esProrroga ? 1 : 0, dur });

        return cuartoId;
    }

    public async Task<long> AddEventoAsync(int partidoId, int cuartoId, string tipo, int? segundosRestantes)
    {
        using var conn = _db.Open();
        var id = await conn.ExecuteScalarAsync<long>(@"
INSERT INTO dbo.CronometroEvento (partido_id, cuarto_id, tipo, segundos_restantes)
OUTPUT INSERTED.evento_id
VALUES (@p, @c, @t, @s);",
            new { p = partidoId, c = cuartoId, t = tipo, s = segundosRestantes });
        return id;
    }

    public async Task<int> ResetEventosAsync(int partidoId)
    {
        using var conn = _db.Open();
        return await conn.ExecuteAsync("DELETE FROM dbo.CronometroEvento WHERE partido_id = @p", new { p = partidoId });
    }
}
