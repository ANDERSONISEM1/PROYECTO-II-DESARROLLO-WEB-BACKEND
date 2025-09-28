using System.Data;
using Dapper;
using Api.Models;
using System.Linq;

namespace Api.Data
{
    public class PartidosCrudRepo
    {
        private readonly Db _db;
        public PartidosCrudRepo(Db db) => _db = db;

        public async Task<IEnumerable<PartidoDto>> GetAllAsync()
        {
            using var conn = _db.Open();
            var sql = @"
              SELECT p.partido_id AS Id,
                     p.equipo_local_id AS EquipoLocalId,
                     p.equipo_visitante_id AS EquipoVisitanteId,
                     p.fecha_hora_inicio AS FechaHoraInicio,
                     p.estado AS Estado,
                     p.minutos_por_cuarto AS MinutosPorCuarto,
                     p.cuartos_totales AS CuartosTotales,
                     p.faltas_por_equipo_limite AS FaltasPorEquipoLimite,
                     p.faltas_por_jugador_limite AS FaltasPorJugadorLimite,
                     p.sede AS Sede,
                     p.fecha_creacion AS FechaCreacion
              FROM dbo.Partido p
              ORDER BY p.fecha_hora_inicio DESC, p.partido_id DESC";
            return await conn.QueryAsync<PartidoDto>(sql);
        }

        public async Task<PartidoDto?> GetByIdAsync(int id)
        {
            using var conn = _db.Open();
            var sql = @"
              SELECT p.partido_id AS Id,
                     p.equipo_local_id AS EquipoLocalId,
                     p.equipo_visitante_id AS EquipoVisitanteId,
                     p.fecha_hora_inicio AS FechaHoraInicio,
                     p.estado AS Estado,
                     p.minutos_por_cuarto AS MinutosPorCuarto,
                     p.cuartos_totales AS CuartosTotales,
                     p.faltas_por_equipo_limite AS FaltasPorEquipoLimite,
                     p.faltas_por_jugador_limite AS FaltasPorJugadorLimite,
                     p.sede AS Sede,
                     p.fecha_creacion AS FechaCreacion
              FROM dbo.Partido p
              WHERE p.partido_id = @id";
            return await conn.QueryFirstOrDefaultAsync<PartidoDto>(sql, new { id });
        }

        public async Task<int> CreateAsync(CreatePartidoRequest body)
        {
            using var conn = _db.Open();
            var sql = @"
            INSERT INTO dbo.Partido
              (equipo_local_id, equipo_visitante_id, fecha_hora_inicio, estado,
               minutos_por_cuarto, cuartos_totales, faltas_por_equipo_limite, faltas_por_jugador_limite, sede)
            OUTPUT INSERTED.partido_id
            VALUES
              (@EquipoLocalId, @EquipoVisitanteId, @FechaHoraInicio, @Estado,
               @MinutosPorCuarto, @CuartosTotales, @FaltasPorEquipoLimite, @FaltasPorJugadorLimite, @Sede)";
            return await conn.ExecuteScalarAsync<int>(sql, body);
        }

        public async Task<int> UpdateAsync(int id, UpdatePartidoRequest body)
        {
            using var conn = _db.Open();
            var sql = @"
            UPDATE dbo.Partido SET
              equipo_local_id = @EquipoLocalId,
              equipo_visitante_id = @EquipoVisitanteId,
              fecha_hora_inicio = @FechaHoraInicio,
              estado = @Estado,
              minutos_por_cuarto = @MinutosPorCuarto,
              cuartos_totales = @CuartosTotales,
              faltas_por_equipo_limite = @FaltasPorEquipoLimite,
              faltas_por_jugador_limite = @FaltasPorJugadorLimite,
              sede = @Sede
            WHERE partido_id = @id";
            return await conn.ExecuteAsync(sql, new {
                id,
                body.EquipoLocalId, body.EquipoVisitanteId, body.FechaHoraInicio, body.Estado,
                body.MinutosPorCuarto, body.CuartosTotales, body.FaltasPorEquipoLimite, body.FaltasPorJugadorLimite, body.Sede
            });
        }

        /// <summary>
        /// 0 si no existe; >0 filas borradas (incluye hijos).
        /// </summary>
        public async Task<int> DeleteAsync(int id)
        {
            using var conn = _db.Open();
            using var tx = conn.BeginTransaction();

            var sql = @"
                DELETE FROM dbo.Anotacion        WHERE partido_id = @id;
                DELETE FROM dbo.Falta            WHERE partido_id = @id;
                DELETE FROM dbo.TiempoMuerto     WHERE partido_id = @id;
                DELETE FROM dbo.CronometroEvento WHERE partido_id = @id;
                DELETE FROM dbo.RosterPartido    WHERE partido_id = @id;
                DELETE FROM dbo.Cuarto           WHERE partido_id = @id;
                DELETE FROM dbo.Partido          WHERE partido_id = @id;";
            var rows = await conn.ExecuteAsync(sql, new { id }, tx);
            tx.Commit();
            return rows > 0 ? rows : 0;
        }

        public async Task<IEnumerable<RosterEntryDto>> GetRosterAsync(int partidoId)
        {
            using var conn = _db.Open();
            var sql = @"
                SELECT partido_id AS PartidoId,
                       equipo_id  AS EquipoId,
                       jugador_id AS JugadorId,
                       es_titular AS EsTitular
                FROM dbo.RosterPartido
                WHERE partido_id = @partidoId";
            return await conn.QueryAsync<RosterEntryDto>(sql, new { partidoId });
        }

        /// <summary>
        /// Reemplaza el roster. -10 si >5 titulares en algún equipo; 0 si partido no existe; >0 filas insertadas.
        /// </summary>
        public async Task<int> SaveRosterAsync(SaveRosterRequest body)
        {
            using var conn = _db.Open();
            using var tx = conn.BeginTransaction();

            var ids = await conn.QueryFirstOrDefaultAsync<(int LocalId, int VisId)>(
                @"SELECT equipo_local_id AS LocalId, equipo_visitante_id AS VisId
                  FROM dbo.Partido WHERE partido_id = @pid",
                new { pid = body.PartidoId }, tx);

            if (ids.Equals(default((int,int)))) { tx.Rollback(); return 0; }

            var titularesPorEquipo = body.Items
                .Where(i => i.EsTitular)
                .GroupBy(i => i.EquipoId)
                .ToDictionary(g => g.Key, g => g.Count());

            if (titularesPorEquipo.Values.Any(c => c > 5))
            {
                tx.Rollback();
                return -10; // conflict
            }

            await conn.ExecuteAsync("DELETE FROM dbo.RosterPartido WHERE partido_id = @pid",
                new { pid = body.PartidoId }, tx);

            const string ins = @"
              INSERT INTO dbo.RosterPartido (partido_id, equipo_id, jugador_id, es_titular)
              VALUES (@PartidoId, @EquipoId, @JugadorId, @EsTitular)";

            int count = 0;
            foreach (var it in body.Items.DistinctBy(x => new { x.PartidoId, x.JugadorId }))
            {
                if (it.EquipoId != ids.LocalId && it.EquipoId != ids.VisId) continue;
                count += await conn.ExecuteAsync(ins, it, tx);
            }

            tx.Commit();
            return count;
        }
    }
}
