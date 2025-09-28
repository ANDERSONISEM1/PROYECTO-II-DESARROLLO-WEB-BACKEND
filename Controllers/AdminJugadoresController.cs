using Microsoft.AspNetCore.Mvc;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Data.SqlClient; // ← NUEVO: capturar errores de clave única

namespace Api.Controllers
{
    [ApiController]
    [Route("api/admin/jugadores")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public class AdminJugadoresController : ControllerBase
    {
        private readonly JugadorRepo _repo;
        public AdminJugadoresController(JugadorRepo repo) => _repo = repo;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<JugadorDto>>> GetAll([FromQuery] int? equipoId)
            => Ok(await _repo.GetAllAsync(equipoId));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<JugadorDto>> GetById(int id)
        {
            var row = await _repo.GetByIdAsync(id);
            return row is null ? NotFound() : Ok(row);
        }

        [HttpPost]
        public async Task<ActionResult<JugadorDto>> Create([FromBody] CreateJugadorRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Nombres) || string.IsNullOrWhiteSpace(body?.Apellidos))
                return BadRequest(new { error = "Nombres y Apellidos son requeridos." });

            try
            {
                var id = await _repo.CreateAsync(body);
                var dto = await _repo.GetByIdAsync(id);
                return CreatedAtAction(nameof(GetById), new { id }, dto);
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                // violación de índice/clave única: p.ej. UNIQUE(equipo_id, dorsal)
                return Conflict(new { error = "No se puede repetir el mismo dorsal en el equipo." });
            }
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateJugadorRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Nombres) || string.IsNullOrWhiteSpace(body?.Apellidos))
                return BadRequest(new { error = "Nombres y Apellidos son requeridos." });

            try
            {
                var n = await _repo.UpdateAsync(id, body);
                return n == 0 ? NotFound() : NoContent();
            }
            catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
            {
                return Conflict(new { error = "No se puede repetir el mismo dorsal en el equipo." });
            }
        }

        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var n = await _repo.DeleteAsync(id);

            if (n == -1)
            {
                return Conflict(new
                {
                    error = "Este jugador está involucrado en partidos (convocado o con faltas registradas). Elimínalo de los partidos y luego vuelve a intentar."
                });
            }
            else if (n == 0)
            {
                return NotFound();
            }
            else
            {
                return NoContent();
            }
        }
    }
}
