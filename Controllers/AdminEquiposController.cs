// ======================= Api.Controllers/AdminEquiposController.cs =======================
using Microsoft.AspNetCore.Mvc;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace Api.Controllers
{
    [ApiController]
    [Route("api/admin/equipos")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public class AdminEquiposController : ControllerBase
    {
        private readonly EquiposRepo _repo;
        public AdminEquiposController(EquiposRepo repo) => _repo = repo;

        [HttpGet]
        public async Task<ActionResult<IEnumerable<EquipoDto>>> GetAll()
            => Ok(await _repo.GetAllAsync());

        [HttpGet("{id:int}")]
        public async Task<ActionResult<EquipoDto>> GetById(int id)
        {
            var row = await _repo.GetByIdAsync(id);
            return row is null ? NotFound() : Ok(row);
        }

        // Info para modal de eliminación (lista jugadores + resumen partidos)
        [HttpGet("{id:int}/delete-info")]
        public async Task<ActionResult<EquipoDeleteInfoDto>> GetDeleteInfo(int id)
            => Ok(await _repo.GetDeleteInfoAsync(id));

        [HttpPost]
        public async Task<ActionResult<EquipoDto>> Create([FromBody] CreateEquipoRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Nombre))
                return BadRequest(new { error = "Nombre requerido." });

            var nombre = body.Nombre.Trim();

            // ====== Validación de nombre único (solo nombre) ======
            if (await _repo.ExistsByNameAsync(nombre))
                return Conflict(new { error = "Ya existe un equipo con ese nombre." });

            var id = await _repo.CreateAsync(body);
            var dto = await _repo.GetByIdAsync(id);
            return CreatedAtAction(nameof(GetById), new { id }, dto);
        }

        [HttpPut("{id:int}")]
        public async Task<ActionResult> Update(int id, [FromBody] UpdateEquipoRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.Nombre))
                return BadRequest(new { error = "Nombre requerido." });

            var nombre = body.Nombre.Trim();

            // ====== Validación de nombre único (excluyendo el propio id) ======
            if (await _repo.ExistsByNameExceptIdAsync(id, nombre))
                return Conflict(new { error = "Ya existe otro equipo con ese nombre." });

            var n = await _repo.UpdateAsync(id, body);
            return n == 0 ? NotFound() : NoContent();
        }

        // Eliminar:
        // - Si participa en partidos: 409 (bloqueado)
        // - Si NO participa: borra jugadores y luego el equipo
        [HttpDelete("{id:int}")]
        public async Task<ActionResult> Delete(int id)
        {
            var n = await _repo.DeleteAsync(id);
            if (n == -2)
                return Conflict(new { error = "No se puede eliminar: el equipo participa en partidos. Elimine esos partidos primero." });
            return n == 0 ? NotFound() : NoContent();
        }

        // Logo
        [HttpGet("{id:int}/logo")]
        public async Task<IActionResult> GetLogo(int id)
        {
            var (logo, contentType) = await _repo.GetLogoAsync(id);
            if (logo == null) return NotFound();
            return File(logo, contentType ?? "application/octet-stream");
        }
    }
}
