using System.Data;
using Dapper;
using Api.Models;

namespace Api.Data
{
    public class HistorialRepo
    {
        private readonly Db _db;
        public HistorialRepo(Db db) => _db = db;

        public async Task<IEnumerable<PartidoHistDto>> GetPartidosFinalizadosAsync(int? equipoId = null)
        {
            using var conn = _db.Open();
            var sql = @"
                SELECT
                    p.partido_id          AS Id,
                    p.equipo_local_id     AS EquipoLocalId,
                    p.equipo_visitante_id AS EquipoVisitanteId,
                    p.fecha_hora_inicio   AS FechaHoraInicio,
                    p.sede                AS Sede,
                    p.estado              AS Estado,
                    COALESCE(v.puntos_local, 0)      AS PuntosLocal,
                    COALESCE(v.puntos_visitante, 0)  AS PuntosVisitante
                FROM dbo.Partido p
                LEFT JOIN dbo.vw_MarcadorPartido v ON v.partido_id = p.partido_id
                /**where**/
                ORDER BY p.fecha_hora_inicio DESC";

            var where = new List<string> { "p.estado = N'finalizado'" };
            var dp = new DynamicParameters();

            if (equipoId.HasValue && equipoId.Value > 0)
            {
                where.Add("(p.equipo_local_id = @equipoId OR p.equipo_visitante_id = @equipoId)");
                dp.Add("equipoId", equipoId.Value, DbType.Int32);
            }

            sql = sql.Replace("/**where**/", "WHERE " + string.Join(" AND ", where));
            return await conn.QueryAsync<PartidoHistDto>(sql, dp);
        }
    }
}
