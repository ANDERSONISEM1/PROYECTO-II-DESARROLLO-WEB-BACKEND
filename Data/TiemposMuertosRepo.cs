// Api/Data/TiemposMuertosRepo.cs
using System.Linq;
using Dapper;
using Api.Models;

namespace Api.Data;

public class TiemposMuertosRepo
{
    private readonly Db _db;
    public TiemposMuertosRepo(Db db) => _db = db;

    public async Task<(int localId, int visitId, string localNombre, string visitNombre)> GetEquiposDelPartidoAsync(int partidoId)
    {
        using var conn = _db.Open();
        var p = await conn.QuerySingleOrDefaultAsync<(int equipo_local_id, int equipo_visitante_id)>(
            "SELECT equipo_local_id, equipo_visitante_id FROM dbo.Partido WHERE partido_id = @id",
            new { id = partidoId });

        if (p.equipo_local_id == 0 || p.equipo_visitante_id == 0)
            return (0, 0, "", "");

        var rows = await conn.QueryAsync<(int Id, string Nombre)>(
            "SELECT equipo_id AS Id, nombre AS Nombre FROM dbo.Equipo WHERE equipo_id IN (@a,@b)",
            new { a = p.equipo_local_id, b = p.equipo_visitante_id });

        var dic = rows.ToDictionary(x => x.Id, x => x.Nombre);
        dic.TryGetValue(p.equipo_local_id, out var nLoc);
        dic.TryGetValue(p.equipo_visitante_id, out var nVis);

        return (p.equipo_local_id, p.equipo_visitante_id, nLoc ?? "", nVis ?? "");
    }

    public async Task<int?> ResolveCuartoIdAsync(int partidoId, int numeroCuarto, bool esProrroga)
    {
        using var conn = _db.Open();
        return await conn.QueryFirstOrDefaultAsync<int?>(@"
SELECT cuarto_id
FROM dbo.Cuarto
WHERE partido_id = @p AND numero = @n AND es_prorroga = @ot", new { p = partidoId, n = numeroCuarto, ot = esProrroga ? 1 : 0 });
    }

    public async Task<int> AddAsync(int partidoId, int equipoId, string tipo, int? cuartoId = null)
    {
        using var conn = _db.Open();
        var sql = @"
INSERT INTO dbo.TiempoMuerto(partido_id, cuarto_id, equipo_id, tipo)
VALUES (@partidoId, @cuartoId, @equipoId, @tipo)";
        return await conn.ExecuteAsync(sql, new { partidoId, cuartoId, equipoId, tipo });
    }

    public async Task<int> RemoveLastAsync(int partidoId, int equipoId, string tipo)
    {
        using var conn = _db.Open();
        var sql = @"
WITH x AS (
  SELECT TOP(1) tiempo_muerto_id
  FROM dbo.TiempoMuerto
  WHERE partido_id = @partidoId AND equipo_id = @equipoId AND tipo = @tipo
  ORDER BY tiempo_muerto_id DESC
)
DELETE tm
FROM dbo.TiempoMuerto tm
JOIN x ON x.tiempo_muerto_id = tm.tiempo_muerto_id;";
        return await conn.ExecuteAsync(sql, new { partidoId, equipoId, tipo });
    }

    public async Task<int> ResetAsync(int partidoId)
    {
        using var conn = _db.Open();
        return await conn.ExecuteAsync("DELETE FROM dbo.TiempoMuerto WHERE partido_id = @partidoId", new { partidoId });
    }

    public async Task<TiemposMuertosResumenDto> GetResumenAsync(int partidoId)
    {
        using var conn = _db.Open();

        var (localId, visitId, nLoc, nVis) = await GetEquiposDelPartidoAsync(partidoId);
        if (localId == 0 || visitId == 0)
            throw new System.Exception("Partido no existe o sin equipos.");

        var data = (await conn.QueryAsync<(int equipo_id, string tipo, int cnt)>(@"
SELECT equipo_id, tipo, COUNT(*) AS cnt
FROM dbo.TiempoMuerto
WHERE partido_id = @partidoId
GROUP BY equipo_id, tipo;",
            new { partidoId })).ToList();

        TeamTimeoutsDto Build(int equipoId, string nombre)
        {
            int cortos = data.Where(r => r.equipo_id == equipoId && r.tipo == "corto").Sum(r => r.cnt);
            int largos = data.Where(r => r.equipo_id == equipoId && r.tipo == "largo").Sum(r => r.cnt);
            return new TeamTimeoutsDto
            {
                EquipoId = equipoId,
                EquipoNombre = nombre,
                Cortos = cortos,
                Largos = largos,
                Total = cortos + largos
            };
        }

        return new TiemposMuertosResumenDto
        {
            PartidoId = partidoId,
            Local = Build(localId, nLoc),
            Visitante = Build(visitId, nVis)
        };
    }
}
