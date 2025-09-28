using System.Data;
using Dapper;
using Api.Models;

namespace Api.Data
{
    public class InicioRepo
    {
        private readonly Db _db;
        public InicioRepo(Db db) => _db = db;

        // KPI: equipos, jugadores, y SOLO partidos programados
        public async Task<InicioKpisDto> GetKpisAsync()
        {
            using var conn = _db.Open();
            var sql = @"
                SELECT
                    (SELECT COUNT(1) FROM dbo.Equipo)  AS TotalEquipos,
                    (SELECT COUNT(1) FROM dbo.Jugador) AS TotalJugadores,
                    (SELECT COUNT(1) FROM dbo.Partido
                      WHERE estado = N'programado')    AS PartidosPendientes";
            return await conn.QuerySingleAsync<InicioKpisDto>(sql);
        }

        // Próximo = el programado más cercano en el FUTURO (ignora NULL y pasados)
        public async Task<ProximoPartidoDto?> GetProximoPartidoAsync()
        {
            using var conn = _db.Open();
            var sql = @"
                SELECT TOP 1
                    p.partido_id          AS Id,
                    p.equipo_local_id     AS EquipoLocalId,
                    p.equipo_visitante_id AS EquipoVisitanteId,
                    p.fecha_hora_inicio   AS FechaHoraInicio,
                    p.sede                AS Sede,
                    p.estado              AS Estado
                FROM dbo.Partido p
                WHERE p.estado = N'programado'
                  AND p.fecha_hora_inicio IS NOT NULL
                  AND p.fecha_hora_inicio >= SYSUTCDATETIME()
                ORDER BY p.fecha_hora_inicio ASC";
            return await conn.QueryFirstOrDefaultAsync<ProximoPartidoDto>(sql);
        }
    }
}
