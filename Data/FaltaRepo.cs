using System;
using System.Data;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Api.Models;

namespace Api.Data;

public class FaltasRepo
{
    private readonly Db _db;
    public FaltasRepo(Db db) => _db = db;

    // Catálogo estándar según tu BD (TipoFalta)
    private const string TIPO_NOMBRE = "personal";

    private async Task<byte> EnsureTipoFaltaIdAsync(IDbConnection conn, IDbTransaction? tx = null)
    {
        var id = await conn.ExecuteScalarAsync<byte?>(
            "SELECT tipo_falta_id FROM dbo.TipoFalta WHERE nombre = @n",
            new { n = TIPO_NOMBRE }, tx);

        if (id.HasValue) return id.Value;

        var newId = await conn.ExecuteScalarAsync<int>(
            "INSERT INTO dbo.TipoFalta(nombre) OUTPUT inserted.tipo_falta_id VALUES(@n)",
            new { n = TIPO_NOMBRE }, tx);

        return (byte)newId;
    }

    // ====== utilidades de cuarto ======
    private sealed record Cfg(int minutos_por_cuarto, int cuartos_totales);

    private async Task<Cfg?> GetCfgAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
        => await conn.QueryFirstOrDefaultAsync<Cfg>(
            "SELECT minutos_por_cuarto, cuartos_totales FROM dbo.Partido WHERE partido_id = @id",
            new { id = partidoId }, tx);

    private async Task<int?> GetActivoCuartoIdAsync(IDbConnection conn, int partidoId, IDbTransaction? tx = null)
        => await conn.QueryFirstOrDefaultAsync<int?>(
            "SELECT TOP(1) cuarto_id FROM dbo.Cuarto WHERE partido_id=@p AND estado=N'en_curso' ORDER BY numero",
            new { p = partidoId }, tx);

    public async Task<int?> ResolveCuartoIdAsync(int partidoId, int numeroCuarto, bool esProrroga)
    {
        using var conn = _db.Open();
        const string sql = @"
SELECT cuarto_id
FROM dbo.Cuarto
WHERE partido_id = @p AND numero = @n AND es_prorroga = @ot;";
        return await conn.QueryFirstOrDefaultAsync<int?>(sql, new { p = partidoId, n = numeroCuarto, ot = esProrroga ? 1 : 0 });
    }

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

    /// <summary>
    /// Política central: devolver SIEMPRE un cuarto_id válido para registrar la falta.
    /// </summary>
    public async Task<int> ResolveCuartoIdPreferenteAsync(int partidoId, int? cuartoId, int? numeroCuarto, bool? esProrroga)
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

            var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");
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
            var cfg = await GetCfgAsync(conn, partidoId, tx) ?? throw new Exception("Partido no existe.");
            int dur = DuracionDefault(cfg, false);
            var ensured = await EnsureCuartoAsync(conn, tx, partidoId, numero: 1, esProrroga: false, dur);
            tx.Commit();
            return ensured;
        }
    }

    // ====== operaciones de faltas ======

    /// <summary>Agrega una falta personal (cuarto_id obligatorio ya resuelto arriba).</summary>
    public async Task<int> AddPersonalFaltaAsync(int partidoId, int equipoId, int jugadorId, int cuartoId)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        var tipo = await EnsureTipoFaltaIdAsync(conn, tx);

        var sql = @"
INSERT INTO dbo.Falta(partido_id, cuarto_id, equipo_id, jugador_id, tipo_falta_id)
VALUES (@partidoId, @cuartoId, @equipoId, @jugadorId, @tipo);";
        var rows = await conn.ExecuteAsync(sql, new { partidoId, cuartoId, equipoId, jugadorId, tipo }, tx);

        tx.Commit();
        return rows;
    }

    /// <summary>Elimina la última falta personal; si se pasa cuartoId, limita a ese cuarto.</summary>
    public async Task<int> RemoveLastPersonalFaltaAsync(int partidoId, int equipoId, int jugadorId, int? cuartoId = null)
    {
        using var conn = _db.Open();
        using var tx = conn.BeginTransaction();

        var tipo = await EnsureTipoFaltaIdAsync(conn, tx);

        var sql = @"
WITH x AS (
  SELECT TOP(1) f.falta_id
  FROM dbo.Falta f
  WHERE f.partido_id = @partidoId
    AND f.equipo_id  = @equipoId
    AND f.jugador_id = @jugadorId
    AND f.tipo_falta_id = @tipo
    AND (@cuartoId IS NULL OR f.cuarto_id = @cuartoId)
  ORDER BY f.falta_id DESC
)
DELETE f
FROM dbo.Falta f
JOIN x ON x.falta_id = f.falta_id;";
        var rows = await conn.ExecuteAsync(sql, new { partidoId, equipoId, jugadorId, tipo, cuartoId }, tx);

        tx.Commit();
        return rows;
    }

    public async Task<FaltasResumenDto> GetResumenAsync(int partidoId)
    {
        using var conn = _db.Open();

        var p = await conn.QuerySingleOrDefaultAsync<(int equipo_local_id, int equipo_visitante_id)>(
            "SELECT equipo_local_id, equipo_visitante_id FROM dbo.Partido WHERE partido_id = @id",
            new { id = partidoId });

        if (p.equipo_local_id == 0 || p.equipo_visitante_id == 0)
            throw new Exception("Partido no existe o sin equipos.");

        var equipos = await conn.QueryAsync<(int Id, string Nombre)>(
            "SELECT equipo_id AS Id, nombre AS Nombre FROM dbo.Equipo WHERE equipo_id IN (@a,@b)",
            new { a = p.equipo_local_id, b = p.equipo_visitante_id });

        var dicNombres = equipos.ToDictionary(x => x.Id, x => x.Nombre);

        var tipo = await EnsureTipoFaltaIdAsync(conn);

        async Task<TeamFoulsDto> BuildTeam(int equipoId)
        {
            var jugadores = (await conn.QueryAsync<PlayerFoulsDto>(@"
SELECT
  j.jugador_id AS JugadorId,
  j.dorsal     AS Dorsal,
  (j.nombres + ' ' + j.apellidos) AS Nombre,
  j.posicion   AS Posicion,
  COUNT(*)     AS Faltas
FROM dbo.Falta f
JOIN dbo.Jugador j ON j.jugador_id = f.jugador_id
WHERE f.partido_id = @partidoId AND f.equipo_id = @equipoId AND f.tipo_falta_id = @tipo
GROUP BY j.jugador_id, j.dorsal, j.nombres, j.apellidos, j.posicion
ORDER BY j.dorsal;",
                new { partidoId, equipoId, tipo })).ToList();

            var fuera5 = jugadores.Where(x => x.Faltas >= 5).ToList();
            var total = jugadores.Sum(x => (int)x.Faltas);

            return new TeamFoulsDto
            {
                EquipoId = equipoId,
                EquipoNombre = dicNombres.TryGetValue(equipoId, out var n) ? n : string.Empty,
                Jugadores = jugadores,
                Fuera5 = fuera5,
                TotalEquipo = total
            };
        }

        return new FaltasResumenDto
        {
            PartidoId = partidoId,
            Local = await BuildTeam(p.equipo_local_id),
            Visitante = await BuildTeam(p.equipo_visitante_id)
        };
    }

    public async Task<int> ResetFaltasPartidoAsync(int partidoId)
    {
        using var conn = _db.Open();
        var sql = "DELETE FROM dbo.Falta WHERE partido_id = @partidoId";
        return await conn.ExecuteAsync(sql, new { partidoId });
    }
}
