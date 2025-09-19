using Dapper;
using Api.Models;
using System.Data;

namespace Api.Data;

public class MarcadorRepo
{
    private readonly Db _db;
    public MarcadorRepo(Db db) => _db = db;

    public async Task<IEnumerable<EquipoMini>> GetEquiposAsync()
    {
        using var conn = _db.Open();
        var sql = @"
            SELECT
              e.equipo_id      AS Id,
              e.nombre         AS Nombre,
              e.abreviatura    AS Abreviatura,
              e.color_primario AS Color
            FROM dbo.Equipo e
            WHERE e.activo = 1
            ORDER BY e.nombre;";
        return await conn.QueryAsync<EquipoMini>(sql);
    }

    public async Task<Marcador?> GetMarcadorAsync(int partidoId)
    {
        using var conn = _db.Open();
        var sql = @"
SELECT
  p.partido_id                           AS PartidoId,
  ISNULL(SUM(CASE WHEN a.equipo_id = p.equipo_local_id     THEN a.puntos ELSE 0 END),0) AS Local,
  ISNULL(SUM(CASE WHEN a.equipo_id = p.equipo_visitante_id THEN a.puntos ELSE 0 END),0) AS Visitante
FROM dbo.Partido p
LEFT JOIN dbo.Anotacion a ON a.partido_id = p.partido_id
WHERE p.partido_id = @id
GROUP BY p.partido_id;";
        return await conn.QueryFirstOrDefaultAsync<Marcador>(sql, new { id = partidoId });
    }

    // ===== utilidades de cuarto (misma política que en faltas) =====
    private sealed record Cfg(int minutos_por_cuarto, int cuartos_totales);

    private async Task<Cfg?> GetCfgAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
        => await conn.QueryFirstOrDefaultAsync<Cfg>(
            "SELECT minutos_por_cuarto, cuartos_totales FROM dbo.Partido WHERE partido_id = @id",
            new { id = partidoId }, tx);

    private async Task<int?> GetActivoCuartoIdAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
        => await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT TOP(1) cuarto_id FROM dbo.Cuarto WHERE partido_id=@p AND estado=N'en_curso' ORDER BY numero",
            new { p = partidoId }, tx);

    private static int DuracionDefault(Cfg cfg, bool esProrroga)
        => esProrroga ? 300 : Math.Max(60, cfg.minutos_por_cuarto * 60);

    private async Task<int> EnsureCuartoAsync(IDbConnection conn, IDbTransaction tx, int partidoId, int numero, bool esProrroga, int dur)
    {
        var existing = await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT cuarto_id FROM dbo.Cuarto WHERE partido_id=@p AND numero=@n",
            new { p = partidoId, n = numero }, tx);
        if (existing.HasValue) return existing.Value;

        var id = await conn.ExecuteScalarAsync<int>(@"
INSERT INTO dbo.Cuarto (partido_id, numero, es_prorroga, duracion_segundos, segundos_restantes, estado)
OUTPUT INSERTED.cuarto_id
VALUES (@p, @n, @es, @dur, @dur, N'pendiente');",
            new { p = partidoId, n = numero, es = esProrroga ? 1 : 0, dur }, tx);

        return id;
    }

    private async Task<int> ResolveCuartoIdPreferenteAsync(int partidoId, int? cuartoId, int? numeroCuarto, bool? esProrroga)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        // 1) CuartoId directo
        if (cuartoId.HasValue)
        {
            tx.Commit();
            return cuartoId.Value;
        }

        // 2) Numero + (opcional) EsProrroga
        if (numeroCuarto.HasValue)
        {
            bool ot = esProrroga ?? false;
            var cid = await conn.QueryFirstOrDefaultAsync<int?>(@"
SELECT cuarto_id
FROM dbo.Cuarto
WHERE partido_id=@p AND numero=@n AND es_prorroga=@ot;",
                new { p = partidoId, n = numeroCuarto.Value, ot = ot ? 1 : 0 }, tx);

            if (cid.HasValue)
            {
                tx.Commit();
                return cid.Value;
            }

            var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new InvalidOperationException("Partido no existe.");
            int dur = DuracionDefault(cfg, ot);
            var ensured = await EnsureCuartoAsync(conn, tx, partidoId, numeroCuarto.Value, ot, dur);
            tx.Commit();
            return ensured;
        }

        // 3) Cuarto en curso
        var activo = await GetActivoCuartoIdAsync(conn, partidoId, tx);
        if (activo.HasValue)
        {
            tx.Commit();
            return activo.Value;
        }

        // 4) A falta de todo, asegurar el 1er cuarto
        {
            var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new InvalidOperationException("Partido no existe.");
            int dur = DuracionDefault(cfg, false);
            var ensured = await EnsureCuartoAsync(conn, tx, partidoId, numero: 1, esProrroga: false, dur);
            tx.Commit();
            return ensured;
        }
    }

    /// <summary>
    /// Inserta una anotación (+/- puntos) y devuelve el marcador total.
    /// Evita que el total de un equipo baje de 0 (ajusta el valor negativo si corresponde).
    /// SIEMPRE llena cuarto_id (resolviendo/creando según política).
    /// </summary>
    public async Task<Marcador> AjustarAnotacionAsync(
        int partidoId, int equipoId, short puntos,
        int? cuartoId = null, int? numeroCuarto = null, bool? esProrroga = null)
    {
        using var conn = _db.Open();

        var ids = await conn.QueryFirstOrDefaultAsync<(int LocalId, int VisitId)>(@"
SELECT equipo_local_id AS LocalId, equipo_visitante_id AS VisitId
FROM dbo.Partido WHERE partido_id = @p;", new { p = partidoId });

        if (ids.LocalId == 0 && ids.VisitId == 0)
            throw new InvalidOperationException("Partido no existe.");

        if (equipoId != ids.LocalId && equipoId != ids.VisitId)
            throw new InvalidOperationException("Equipo no pertenece al partido.");

        if (puntos is 0 or > 3 or < -3)
            throw new InvalidOperationException("Puntos inválidos. Use -3..-1 o 1..3.");

        // ✅ Resolver cuarto_id definitivo
        var cuartoIdFirm = await ResolveCuartoIdPreferenteAsync(partidoId, cuartoId, numeroCuarto, esProrroga);

        var totalEquipo = await conn.ExecuteScalarAsync<int>(@"
SELECT ISNULL(SUM(a.puntos),0)
FROM dbo.Anotacion a
WHERE a.partido_id = @p AND a.equipo_id = @e;", new { p = partidoId, e = equipoId });

        short puntosEfectivos = puntos;
        if (puntos < 0 && totalEquipo + puntos < 0)
        {
            puntosEfectivos = (short)(-totalEquipo);
            if (puntosEfectivos == 0)
                return await GetMarcadorAsync(partidoId) ?? new Marcador { PartidoId = partidoId, Local = 0, Visitante = 0 };
        }

        // ⬇️ Insert con cuarto_id ya resuelto
        await conn.ExecuteAsync(@"
INSERT INTO dbo.Anotacion (partido_id, cuarto_id, equipo_id, puntos)
VALUES (@p, @c, @e, @pts);",
            new { p = partidoId, c = cuartoIdFirm, e = equipoId, pts = puntosEfectivos });

        var m = await GetMarcadorAsync(partidoId);
        return m ?? new Marcador { PartidoId = partidoId, Local = 0, Visitante = 0 };
    }

    public async Task<int> ResetAnotacionesAsync(int partidoId)
    {
        using var conn = _db.Open();
        var n = await conn.ExecuteAsync("DELETE FROM dbo.Anotacion WHERE partido_id = @p;", new { p = partidoId });
        return n;
    }
}

public record EquipoMini
{
    public int Id { get; init; }
    public string Nombre { get; init; } = "";
    public string? Abreviatura { get; init; }
    public string? Color { get; init; }
}
