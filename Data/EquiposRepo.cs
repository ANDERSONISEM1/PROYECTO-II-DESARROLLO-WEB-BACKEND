// ============================== Api.Data/EquiposRepo.cs ==============================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Api.Models;

namespace Api.Data
{
    public class EquiposRepo
    {
        private readonly Db _db;
        public EquiposRepo(Db db) => _db = db;

        public async Task<IEnumerable<EquipoDto>> GetAllAsync()
        {
            using var conn = _db.Open();
            var sql = @"SELECT equipo_id   AS Id,
                               nombre      AS Nombre,
                               ciudad      AS Ciudad,
                               abreviatura AS Abreviatura,
                               activo      AS Activo,
                               CAST(fecha_creacion AS datetime2) AS FechaCreacion
                        FROM dbo.Equipo
                        ORDER BY nombre";
            return await conn.QueryAsync<EquipoDto>(sql);
        }

        public async Task<EquipoDto?> GetByIdAsync(int id)
        {
            using var conn = _db.Open();
            var sql = @"SELECT equipo_id   AS Id,
                               nombre      AS Nombre,
                               ciudad      AS Ciudad,
                               abreviatura AS Abreviatura,
                               activo      AS Activo,
                               CAST(fecha_creacion AS datetime2) AS FechaCreacion
                        FROM dbo.Equipo WHERE equipo_id = @id";
            return await conn.QueryFirstOrDefaultAsync<EquipoDto>(sql, new { id });
        }

        // ====== NUEVO: validaciones de nombre único ======
        public async Task<bool> ExistsByNameAsync(string nombre)
        {
            using var conn = _db.Open();
            // comparación insensible a mayúsculas/minúsculas y con TRIM
            var sql = @"SELECT COUNT(1)
                        FROM dbo.Equipo
                        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@nombre)))";
            var n = await conn.ExecuteScalarAsync<int>(sql, new { nombre });
            return n > 0;
        }

        public async Task<bool> ExistsByNameExceptIdAsync(int id, string nombre)
        {
            using var conn = _db.Open();
            var sql = @"SELECT COUNT(1)
                        FROM dbo.Equipo
                        WHERE UPPER(LTRIM(RTRIM(nombre))) = UPPER(LTRIM(RTRIM(@nombre)))
                          AND equipo_id <> @id";
            var n = await conn.ExecuteScalarAsync<int>(sql, new { id, nombre });
            return n > 0;
        }
        // ===================================================

        public async Task<int> CreateAsync(CreateEquipoRequest body)
        {
            using var conn = _db.Open();
            var logo = DecodeBase64OrNull(body.LogoBase64);
            var sql = @"INSERT INTO dbo.Equipo (nombre, ciudad, abreviatura, activo, fecha_creacion, logo)
                        OUTPUT INSERTED.equipo_id
                        VALUES (@n, @c, @a, @act, COALESCE(@fc, SYSUTCDATETIME()), @logo)";
            return await conn.ExecuteScalarAsync<int>(sql, new
            {
                n = body.Nombre.Trim(),
                c = string.IsNullOrWhiteSpace(body.Ciudad) ? null : body.Ciudad.Trim(),
                a = string.IsNullOrWhiteSpace(body.Abreviatura) ? null : body.Abreviatura.Trim(),
                act = body.Activo,
                fc = body.FechaCreacion,
                logo
            });
        }

        public async Task<int> UpdateAsync(int id, UpdateEquipoRequest body)
        {
            using var conn = _db.Open();
            var fields = new List<string>
            {
                "nombre = @n",
                "ciudad = @c",
                "abreviatura = @a",
                "activo = @act"
            };

            byte[]? logo = null;
            if (body.LogoBase64 != null)
            {
                logo = string.IsNullOrEmpty(body.LogoBase64) ? null : DecodeBase64OrNull(body.LogoBase64);
                fields.Add("logo = @logo");
            }

            var sql = $"UPDATE dbo.Equipo SET {string.Join(",", fields)} WHERE equipo_id = @id";
            return await conn.ExecuteAsync(sql, new
            {
                id,
                n = body.Nombre.Trim(),
                c = string.IsNullOrWhiteSpace(body.Ciudad) ? null : body.Ciudad.Trim(),
                a = string.IsNullOrWhiteSpace(body.Abreviatura) ? null : body.Abreviatura.Trim(),
                act = body.Activo,
                logo
            });
        }

        // ===== Info para el modal (igual que antes) =====
        public async Task<EquipoDeleteInfoDto> GetDeleteInfoAsync(int equipoId, int topN = 50)
        {
            using var conn = _db.Open();

            var totalJug = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Jugador WHERE equipo_id = @equipoId", new { equipoId });

            var jugadores = await conn.QueryAsync<JugadorLiteDto>(@"
                SELECT TOP(@topN)
                       j.jugador_id AS Id,
                       j.nombres    AS Nombres,
                       j.apellidos  AS Apellidos,
                       j.dorsal     AS Dorsal
                FROM dbo.Jugador j
                WHERE j.equipo_id = @equipoId
                ORDER BY CASE WHEN j.dorsal IS NULL THEN 1 ELSE 0 END, j.dorsal ASC, j.apellidos, j.nombres",
                new { equipoId, topN });

            var resumen = await conn.QueryFirstAsync<PartidosResumenDto>(@"
                WITH P AS (
                    SELECT estado
                    FROM dbo.Partido
                    WHERE equipo_local_id = @equipoId OR equipo_visitante_id = @equipoId
                )
                SELECT
                    (SELECT COUNT(1) FROM P)                                            AS Total,
                    (SELECT COUNT(1) FROM P WHERE estado = N'programado')               AS Programado,
                    (SELECT COUNT(1) FROM P WHERE estado = N'en_curso')                 AS EnCurso,
                    (SELECT COUNT(1) FROM P WHERE estado = N'finalizado')               AS Finalizado,
                    (SELECT COUNT(1) FROM P WHERE estado = N'cancelado')                AS Cancelado,
                    (SELECT COUNT(1) FROM P WHERE estado = N'suspendido')               AS Suspendido;",
                new { equipoId });

            var canDelete = resumen.Total == 0;
            return new EquipoDeleteInfoDto(canDelete, totalJug, jugadores, resumen);
        }

        // Borrado forzado si NO hay partidos
        // -2: participa en partidos; 0: no existe; >0: filas afectadas
        public async Task<int> DeleteAsync(int id)
        {
            using var conn = _db.Open();

            var partidos = await conn.ExecuteScalarAsync<int>(@"
                SELECT COUNT(1)
                FROM dbo.Partido
                WHERE equipo_local_id = @id OR equipo_visitante_id = @id", new { id });
            if (partidos > 0) return -2;

            var existe = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(1) FROM dbo.Equipo WHERE equipo_id = @id", new { id });
            if (existe == 0) return 0;

            using var tx = conn.BeginTransaction();

            await conn.ExecuteAsync("DELETE FROM dbo.Jugador WHERE equipo_id = @id", new { id }, tx);
            var n = await conn.ExecuteAsync("DELETE FROM dbo.Equipo WHERE equipo_id = @id", new { id }, tx);

            tx.Commit();
            return n;
        }

        public async Task<(byte[]? Logo, string? ContentType)> GetLogoAsync(int id)
        {
            using var conn = _db.Open();
            var sql = "SELECT logo FROM dbo.Equipo WHERE equipo_id = @id";
            var logo = await conn.ExecuteScalarAsync<byte[]?>(sql, new { id });
            if (logo == null) return (null, null);
            return (logo, "image/png");
        }

        private static byte[]? DecodeBase64OrNull(string? b64)
        {
            if (string.IsNullOrWhiteSpace(b64)) return null;
            try
            {
                var comma = b64.IndexOf(',');
                var raw = comma >= 0 ? b64[(comma + 1)..] : b64;
                return Convert.FromBase64String(raw);
            }
            catch { return null; }
        }
    }
}
