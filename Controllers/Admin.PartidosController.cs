
using Microsoft.AspNetCore.Mvc;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using System.Linq;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/admin/partidos")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public class AdminPartidosController : ControllerBase
    {
        private readonly PartidosCrudRepo _repo;
        public AdminPartidosController(PartidosCrudRepo repo) => _repo = repo;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PartidoDto>>> GetAll()
            => Ok(await _repo.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<ActionResult<PartidoDto>> GetById(int id)
        {
            var row = await _repo.GetByIdAsync(id);
            return row is null ? NotFound() : Ok(row);
        }

        // Leer roster persistido
        [HttpGet("{id:int}/roster")]
        public async Task<ActionResult<IEnumerable<RosterEntryDto>>> GetRoster(int id)
            => Ok(await _repo.GetRosterAsync(id));

        [HttpPost]
        public async Task<ActionResult<PartidoDto>> Create([FromBody] CreatePartidoRequest body)
        {
            if (body is null || body.EquipoLocalId <= 0 || body.EquipoVisitanteId <= 0)
                return BadRequest(new { error = "EquipoLocalId y EquipoVisitanteId son requeridos." });

            if (body.EquipoLocalId == body.EquipoVisitanteId)
                return Conflict(new { error = "No se puede elegir los mismos equipos; elige uno diferente." });

            var id = await _repo.CreateAsync(new CreatePartidoRequest
            {
                EquipoLocalId = body.EquipoLocalId,
                EquipoVisitanteId = body.EquipoVisitanteId,
                FechaHoraInicio = body.FechaHoraInicio,
                Estado = string.IsNullOrWhiteSpace(body.Estado) ? "programado" : body.Estado.Trim(),
                MinutosPorCuarto = 10,
                CuartosTotales = 4,
                FaltasPorEquipoLimite = body.FaltasPorEquipoLimite == 0 ? (byte)255 : body.FaltasPorEquipoLimite,
                FaltasPorJugadorLimite = body.FaltasPorJugadorLimite == 0 ? (byte)5 : body.FaltasPorJugadorLimite,
                Sede = body.Sede
            });
            var dto = await _repo.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] UpdatePartidoRequest body)
        {
            if (body is null || body.EquipoLocalId <= 0 || body.EquipoVisitanteId <= 0)
                return BadRequest(new { error = "EquipoLocalId y EquipoVisitanteId son requeridos." });

            if (body.EquipoLocalId == body.EquipoVisitanteId)
                return Conflict(new { error = "No se puede elegir los mismos equipos; elige uno diferente." });

            var n = await _repo.UpdateAsync(id, body);
            return n == 0 ? NotFound() : NoContent();
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var n = await _repo.DeleteAsync(id);
            return n == 0 ? NotFound() : NoContent();
        }

        [HttpPut("{id:int}/roster")]
        public async Task<ActionResult> SaveRoster(int id, [FromBody] SaveRosterRequest body)
        {
            if (body is null || body.PartidoId != id)
                return BadRequest(new { error = "PartidoId inválido." });

            // Máx. 5 titulares por equipo (defensivo)
            var titularesPorEquipo = body.Items
                .Where(i => i.EsTitular)
                .GroupBy(i => i.EquipoId)
                .ToDictionary(g => g.Key, g => g.Count());

            if (titularesPorEquipo.Values.Any(c => c > 5))
                return Conflict(new { error = "Solo 5 titulares por equipo." });

            var n = await _repo.SaveRosterAsync(body);
            if (n == -10) return Conflict(new { error = "Solo 5 titulares por equipo." });

            return n == 0 ? NotFound() : NoContent();
        }
    }
}
