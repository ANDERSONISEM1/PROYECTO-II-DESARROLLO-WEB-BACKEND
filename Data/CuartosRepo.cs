using System;
using System.Data;
using Dapper;
using Api.Models;

namespace Api.Data;

public class CuartosRepo
{
    private readonly Db _db;
    public CuartosRepo(Db db) => _db = db;

    private sealed record Cfg(int MinutosPorCuarto, int CuartosTotales);

    private async Task<Cfg?> GetCfgAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
    {
        const string sql = @"SELECT minutos_por_cuarto AS MinutosPorCuarto, cuartos_totales AS CuartosTotales
                             FROM dbo.Partido WHERE partido_id = @id;";
        return await conn.QueryFirstOrDefaultAsync<Cfg>(sql, new { id = partidoId }, tx);
    }

    private async Task<(int? CuartoId, int? Numero, bool EsProrroga)> GetActivoAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
    {
        const string sql = @"SELECT TOP(1) cuarto_id, numero, es_prorroga
                             FROM dbo.Cuarto
                             WHERE partido_id = @p AND estado = N'en_curso'
                             ORDER BY numero;";
        var row = await conn.QueryFirstOrDefaultAsync<(int cuarto_id, int numero, bool es_prorroga)?>(sql, new { p = partidoId }, tx);
        return row.HasValue ? (row.Value.cuarto_id, row.Value.numero, row.Value.es_prorroga) : (null, null, false);
    }

    private async Task<(int? CuartoId, int Numero, bool EsProrroga)?> GetPendienteMasBajoAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
    {
        const string sql = @"SELECT TOP(1) cuarto_id, numero, es_prorroga
                             FROM dbo.Cuarto
                             WHERE partido_id = @p AND estado = N'pendiente'
                             ORDER BY numero;";
        var row = await conn.QueryFirstOrDefaultAsync<(int cuarto_id, int numero, bool es_prorroga)?>(sql, new { p = partidoId }, tx);
        if (!row.HasValue) return null;
        return (row.Value.cuarto_id, row.Value.numero, row.Value.es_prorroga);
    }

    private async Task<int> GetMaxNumeroAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
    {
        return await conn.ExecuteScalarAsync<int?>(@"SELECT MAX(numero) FROM dbo.Cuarto WHERE partido_id = @p;", new { p = partidoId }, tx) ?? 0;
    }

    private static int DuracionDefault(Cfg cfg, bool esProrroga) =>
        esProrroga ? 300 : Math.Max(60, cfg.MinutosPorCuarto * 60);

    private async Task EnsureRowAsync(IDbConnection conn, IDbTransaction tx, int partidoId, int numero, bool esProrroga, int dur)
    {
        const string sel = @"SELECT cuarto_id FROM dbo.Cuarto WHERE partido_id = @p AND numero = @n;";
        var id = await conn.QueryFirstOrDefaultAsync<int?>(sel, new { p = partidoId, n = numero }, tx);
        if (id.HasValue)
        {
            const string upd = @"UPDATE dbo.Cuarto
                                 SET es_prorroga = @es, duracion_segundos = @dur
                                 WHERE partido_id = @p AND cuarto_id = @id;";
            await conn.ExecuteAsync(upd, new { es = esProrroga ? 1 : 0, dur, p = partidoId, id }, tx);
            return;
        }

        const string ins = @"INSERT INTO dbo.Cuarto(partido_id, numero, es_prorroga, duracion_segundos, segundos_restantes, estado)
                             VALUES (@p, @n, @es, @dur, @dur, N'pendiente');";
        await conn.ExecuteAsync(ins, new { p = partidoId, n = numero, es = esProrroga ? 1 : 0, dur }, tx);
    }

    private PeriodStateDto ToDto(Cfg cfg, int numero, bool esProrroga, string? rotulo = null)
        => new PeriodStateDto { Numero = numero, Total = cfg.CuartosTotales, EsProrroga = esProrroga, Rotulo = rotulo };

    // ===== Iniciar / Reiniciar / Finalizar =====

    public async Task<PeriodStateDto> IniciarAsync(int partidoId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");

        const string upPartido = @"UPDATE dbo.Partido
                                   SET estado = N'en_curso',
                                       fecha_hora_inicio = COALESCE(fecha_hora_inicio, SYSUTCDATETIME())
                                   WHERE partido_id = @p;";
        await conn.ExecuteAsync(upPartido, new { p = partidoId }, tx);

        var (cid, num, esOT) = await GetActivoAsync(conn, partidoId, tx);
        if (cid.HasValue && num.HasValue)
        {
            var dur = DuracionDefault(cfg, esOT);
            const string upd = @"UPDATE dbo.Cuarto
                                 SET duracion_segundos = @dur,
                                     hora_inicio = COALESCE(hora_inicio, SYSUTCDATETIME())
                                 WHERE cuarto_id = @id;";
            await conn.ExecuteAsync(upd, new { dur, id = cid.Value }, tx);
            tx.Commit();
            return ToDto(cfg, num.Value, esOT);
        }

        var pend = await GetPendienteMasBajoAsync(conn, partidoId, tx);
        if (pend is null)
        {
            var maxNum = await GetMaxNumeroAsync(conn, partidoId, tx);
            var nextNum = maxNum + 1;
            var dur = DuracionDefault(cfg, true);
            await EnsureRowAsync(conn, tx, partidoId, nextNum, esProrroga: true, dur);

            const string openPr = @"UPDATE dbo.Cuarto
                                    SET estado = N'en_curso',
                                        hora_inicio = SYSUTCDATETIME(),
                                        hora_fin = NULL,
                                        segundos_restantes = duracion_segundos
                                    WHERE partido_id = @p AND numero = @n;";
            await conn.ExecuteAsync(openPr, new { p = partidoId, n = nextNum }, tx);

            tx.Commit();
            return ToDto(cfg, nextNum, true);
        }
        else
        {
          var dur = DuracionDefault(cfg, pend.Value.EsProrroga);
          const string open = @"UPDATE dbo.Cuarto
                                SET estado = N'en_curso',
                                    hora_inicio = SYSUTCDATETIME(),
                                    hora_fin = NULL,
                                    duracion_segundos = @dur,
                                    segundos_restantes = @dur
                                WHERE cuarto_id = @id;";
          await conn.ExecuteAsync(open, new { id = pend.Value.CuartoId!.Value, dur }, tx);

          tx.Commit();
          return ToDto(cfg, pend.Value.Numero, pend.Value.EsProrroga);
        }
    }

    public async Task<PeriodStateDto> ReiniciarAsync(int partidoId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");

        const string upPartido = @"UPDATE dbo.Partido
                                   SET estado = N'en_curso',
                                       fecha_hora_inicio = COALESCE(fecha_hora_inicio, SYSUTCDATETIME())
                                   WHERE partido_id = @p;";
        await conn.ExecuteAsync(upPartido, new { p = partidoId }, tx);

        var (cid, num, esOT) = await GetActivoAsync(conn, partidoId, tx);
        if (!cid.HasValue)
        {
            tx.Commit();
            return await IniciarAsync(partidoId);
        }

        var dur = DuracionDefault(cfg, esOT);
        const string upd = @"UPDATE dbo.Cuarto
                             SET segundos_restantes = @dur,
                                 duracion_segundos = @dur,
                                 hora_inicio = SYSUTCDATETIME(),
                                 estado = N'en_curso'
                             WHERE cuarto_id = @id;";
        await conn.ExecuteAsync(upd, new { id = cid.Value, dur }, tx);

        tx.Commit();
        return ToDto(cfg, num!.Value, esOT);
    }

    public async Task<PeriodStateDto> FinalizarAsync(int partidoId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");

        var (cid, num, esOT) = await GetActivoAsync(conn, partidoId, tx);
        if (cid.HasValue)
        {
            const string close = @"UPDATE dbo.Cuarto
                                   SET estado = N'finalizado',
                                       hora_fin = SYSUTCDATETIME()
                                   WHERE cuarto_id = @id;";
            await conn.ExecuteAsync(close, new { id = cid.Value }, tx);
        }

        // No cerramos el partido aquí.

        tx.Commit();

        var visibleNum = num ?? Math.Max(1, await GetMaxNumeroAsync(conn, partidoId));
        return ToDto(cfg, visibleNum, esOT, null);
    }

    public async Task<PeriodStateDto> SetNumeroAsync(int partidoId, int numero)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");
        var num = Math.Max(1, Math.Min(cfg.CuartosTotales, numero));
        var dur = DuracionDefault(cfg, false);

        const string closeSql = @"UPDATE dbo.Cuarto
                                  SET estado = N'finalizado', hora_fin = SYSUTCDATETIME()
                                  WHERE partido_id = @p AND estado = N'en_curso';";
        await conn.ExecuteAsync(closeSql, new { p = partidoId }, tx);

        await EnsureRowAsync(conn, tx, partidoId, num, esProrroga: false, dur);

        const string openSql = @"UPDATE dbo.Cuarto
                                 SET estado = N'en_curso',
                                     hora_inicio = COALESCE(hora_inicio, SYSUTCDATETIME()),
                                     hora_fin = NULL,
                                     duracion_segundos = @dur,
                                     segundos_restantes = @dur
                                 WHERE partido_id = @p AND numero = @n;";
        await conn.ExecuteAsync(openSql, new { p = partidoId, n = num, dur }, tx);

        const string upPartido = @"UPDATE dbo.Partido
                                   SET estado = N'en_curso',
                                       fecha_hora_inicio = COALESCE(fecha_hora_inicio, SYSUTCDATETIME())
                                   WHERE partido_id = @p;";
        await conn.ExecuteAsync(upPartido, new { p = partidoId }, tx);

        tx.Commit();
        return ToDto(cfg, num, false);
    }

    public async Task<PeriodStateDto> SiguienteAsync(int partidoId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");
        var (_, curNum, _) = await GetActivoAsync(conn, partidoId, tx);
        int target = curNum.HasValue ? curNum.Value + 1 : 1;
        target = Math.Min(target, cfg.CuartosTotales);

        var dto = await SetNumeroAsync(partidoId, target);
        tx.Commit();
        return dto;
    }

    public async Task<PeriodStateDto> AnteriorAsync(int partidoId)
    {
        using var conn = _db.Open();
        var cfg = await GetCfgAsync(conn, partidoId) ?? throw new Exception("Partido no existe.");
        var (_, curNum, _) = await GetActivoAsync(conn, partidoId);
        int target = curNum.HasValue ? Math.Max(1, curNum.Value - 1) : 1;
        return await SetNumeroAsync(partidoId, target);
    }

    public async Task<PeriodStateDto> ProrrogaAsync(int partidoId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

        var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");
        var maxNum = await GetMaxNumeroAsync(conn, partidoId, tx);
        int next = maxNum + 1;
        var dur = DuracionDefault(cfg, true);

        const string closeSql = @"UPDATE dbo.Cuarto
                                  SET estado = N'finalizado', hora_fin = SYSUTCDATETIME()
                                  WHERE partido_id = @p AND estado = N'en_curso';";
        await conn.ExecuteAsync(closeSql, new { p = partidoId }, tx);

        await EnsureRowAsync(conn, tx, partidoId, next, esProrroga: true, dur);

        const string open = @"UPDATE dbo.Cuarto
                              SET estado = N'en_curso',
                                  hora_inicio = SYSUTCDATETIME(),
                                  hora_fin = NULL,
                                  duracion_segundos = @dur,
                                  segundos_restantes = @dur
                              WHERE partido_id = @p AND numero = @n;";
        await conn.ExecuteAsync(open, new { p = partidoId, n = next, dur }, tx);

        const string upPartido = @"UPDATE dbo.Partido
                                   SET estado = N'en_curso',
                                       fecha_hora_inicio = COALESCE(fecha_hora_inicio, SYSUTCDATETIME())
                                   WHERE partido_id = @p;";
        await conn.ExecuteAsync(upPartido, new { p = partidoId }, tx);

        tx.Commit();
        return ToDto(cfg, next, true);
    }

    public async Task<PeriodStateDto> GetResumenAsync(int partidoId)
    {
        using var conn = _db.Open();
        var cfg = await GetCfgAsync(conn, partidoId) ?? throw new Exception("Partido no existe.");
        var (_, num, esOT) = await GetActivoAsync(conn, partidoId);

        if (num.HasValue)
            return ToDto(cfg, num.Value, esOT, null);

        return ToDto(cfg, 1, false, null);
    }
}
