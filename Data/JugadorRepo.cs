using System.Data;
using Dapper;
using Api.Models;

namespace Api.Data
{
    public class JugadorRepo
    {
        private readonly Db _db;
        public JugadorRepo(Db db) => _db = db;

        public async Task<IEnumerable<JugadorDto>> GetAllAsync(int? equipoId = null)
        {
            using var conn = _db.Open();
            var sql = @"
                SELECT j.jugador_id AS Id, j.equipo_id AS EquipoId,
                       j.nombres AS Nombres, j.apellidos AS Apellidos,
                       j.dorsal AS Dorsal, j.posicion AS Posicion,
                       j.estatura_cm AS EstaturaCm, j.edad AS Edad,
                       j.nacionalidad AS Nacionalidad, j.activo AS Activo
                FROM dbo.Jugador j
                /**where**/
                ORDER BY j.nombres, j.apellidos";
            var where = new List<string>();
            var p = new DynamicParameters();
            if (equipoId.HasValue && equipoId.Value > 0)
            {
                where.Add("j.equipo_id = @equipoId");
                p.Add("equipoId", equipoId.Value, DbType.Int32);
            }
            if (where.Count > 0) sql = sql.Replace("/**where**/", "WHERE " + string.Join(" AND ", where));
            else sql = sql.Replace("/**where**/", "");
            return await conn.QueryAsync<JugadorDto>(sql, p);
        }

        public async Task<JugadorDto?> GetByIdAsync(int id)
        {
            using var conn = _db.Open();
            var sql = @"
                SELECT j.jugador_id AS Id, j.equipo_id AS EquipoId,
                       j.nombres AS Nombres, j.apellidos AS Apellidos,
                       j.dorsal AS Dorsal, j.posicion AS Posicion,
                       j.estatura_cm AS EstaturaCm, j.edad AS Edad,
                       j.nacionalidad AS Nacionalidad, j.activo AS Activo
                FROM dbo.Jugador j
                WHERE j.jugador_id = @id";
            return await conn.QueryFirstOrDefaultAsync<JugadorDto>(sql, new { id });
        }

        public async Task<int> CreateAsync(CreateJugadorRequest body)
        {
            using var conn = _db.Open();
            var sql = @"
                INSERT INTO dbo.Jugador
                  (equipo_id, nombres, apellidos, dorsal, posicion, estatura_cm, edad, nacionalidad, activo)
                OUTPUT INSERTED.jugador_id
                VALUES
                  (@EquipoId, @Nombres, @Apellidos, @Dorsal, @Posicion, @EstaturaCm, @Edad, @Nacionalidad, @Activo)";
            return await conn.ExecuteScalarAsync<int>(sql, body);
        }

        public async Task<int> UpdateAsync(int id, UpdateJugadorRequest body)
        {
            using var conn = _db.Open();
            var sql = @"
                UPDATE dbo.Jugador SET
                    equipo_id = @EquipoId,
                    nombres = @Nombres,
                    apellidos = @Apellidos,
                    dorsal = @Dorsal,
                    posicion = @Posicion,
                    estatura_cm = @EstaturaCm,
                    edad = @Edad,
                    nacionalidad = @Nacionalidad,
                    activo = @Activo
                WHERE jugador_id = @id";
            return await conn.ExecuteAsync(sql, new {
                id,
                body.EquipoId, body.Nombres, body.Apellidos, body.Dorsal,
                body.Posicion, body.EstaturaCm, body.Edad, body.Nacionalidad, body.Activo
            });
        }

        /// <summary>
        /// Devuelve:
        ///  -1 si el jugador está involucrado (RosterPartido o Falta)
        ///   0 si no existe
        ///  >0 cantidad filas borradas
        /// </summary>
        public async Task<int> DeleteAsync(int id)
        {
            using var conn = _db.Open();

            // 🚫 Validación de “involucrado”
            var involucrado = await conn.ExecuteScalarAsync<int>(@"
                SELECT
                    CASE
                        WHEN EXISTS(SELECT 1 FROM dbo.RosterPartido rp WHERE rp.jugador_id = @id) THEN 1
                        WHEN EXISTS(SELECT 1 FROM dbo.Falta f WHERE f.jugador_id = @id) THEN 1
                        ELSE 0
                    END", new { id });
            if (involucrado == 1) return -1; // señal de 409 Conflict

            return await conn.ExecuteAsync("DELETE FROM dbo.Jugador WHERE jugador_id = @id", new { id });
        }
    }
}
