// Api/Data/PartidosRepo.cs
using System.Data;
using Dapper;

namespace Api.Data;

public class PartidosRepo
{
    private readonly Db _db;
    public PartidosRepo(Db db) => _db = db;

    public async Task<int?> GetPartidoAbiertoAsync(int localId, int visitId)
    {
        const string sql = @"
SELECT TOP(1) partido_id
FROM dbo.Partido
WHERE equipo_local_id = @local AND equipo_visitante_id = @visit
  AND estado IN (N'programado', N'en_curso')
ORDER BY fecha_creacion DESC;";
        
        using var conn = _db.Open();
        return await conn.QueryFirstOrDefaultAsync<int?>(sql, new { local = localId, visit = visitId });
    }

    public async Task<int> EnsurePartidoAsync(
        int localId,
        int visitId,
        int minutosPorCuarto = 10,
        int cuartosTotales = 4,
        bool llenarRoster = true)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var existente = await conn.QueryFirstOrDefaultAsync<int?>(@"
SELECT TOP(1) partido_id
FROM dbo.Partido
WHERE equipo_local_id = @local AND equipo_visitante_id = @visit
  AND estado IN (N'programado', N'en_curso')
ORDER BY fecha_creacion DESC;",
            new { local = localId, visit = visitId }, tx);

        if (existente.HasValue)
        {
            tx.Commit();
            return existente.Value;
        }

        // ⬇️ SIN 'sede' ni 'observaciones'
        var partidoId = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.Partido
(equipo_local_id, equipo_visitante_id, fecha_hora_inicio, estado,
 minutos_por_cuarto, cuartos_totales, faltas_por_equipo_limite, faltas_por_jugador_limite)
OUTPUT INSERTED.partido_id
VALUES (@local, @visit, SYSUTCDATETIME(), N'en_curso',
        @minCuarto, @cuartos, 5, 5);",
            new { local = localId, visit = visitId, minCuarto = minutosPorCuarto, cuartos = cuartosTotales }, tx);

        var dur = minutosPorCuarto * 60;
        for (int i = 1; i <= cuartosTotales; i++)
        {
            await conn.ExecuteAsync(@"
INSERT INTO dbo.Cuarto (partido_id, numero, es_prorroga, duracion_segundos, segundos_restantes, estado)
VALUES (@p, @n, 0, @dur, @dur, N'pendiente');",
                new { p = partidoId, n = i, dur }, tx);
        }

        if (llenarRoster)
        {
            await conn.ExecuteAsync(@"
INSERT INTO dbo.RosterPartido (partido_id, equipo_id, jugador_id, es_titular)
SELECT @p, j.equipo_id, j.jugador_id, 0
FROM dbo.Jugador j
WHERE j.activo = 1 AND j.equipo_id IN (@local, @visit);",
                new { p = partidoId, local = localId, visit = visitId }, tx);
        }

        tx.Commit();
        return partidoId;
    }

    public async Task<(Api.Data.EquipoMini Local, Api.Data.EquipoMini Visit)> GetEquiposMiniAsync(int localId, int visitId)
    {
        const string sql = @"
SELECT equipo_id   AS Id,
       nombre      AS Nombre,
       abreviatura AS Abreviatura
FROM dbo.Equipo
WHERE equipo_id IN (@local, @visit);";

        using var conn = _db.Open();
        var rows = (await conn.QueryAsync<Api.Data.EquipoMini>(sql, new { local = localId, visit = visitId })).ToList();

        var local = rows.First(r => r.Id == localId);
        var visit = rows.First(r => r.Id == visitId);
        return (local, visit);
    }

    public async Task<bool> FinalizarAsync(int partidoId)
    {
        using var conn = _db.Open();
        var n = await conn.ExecuteAsync(@"
UPDATE dbo.Partido
SET estado = N'finalizado'
WHERE partido_id = @id;", new { id = partidoId });
        return n > 0;
    }

    public async Task<bool> BorrarAsync(int partidoId)
    {
        using var conn = _db.Open();
        var n = await conn.ExecuteAsync("DELETE FROM dbo.Partido WHERE partido_id = @id;", new { id = partidoId });
        return n > 0;
    }
}
