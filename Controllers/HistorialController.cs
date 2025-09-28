using Microsoft.AspNetCore.Mvc;
using Api.Data;
using Api.Models;
using Microsoft.AspNetCore.Authorization;
namespace Api.Controllers
{
    [ApiController]
    [Route("api/historial")]
    [Authorize(Roles = "ADMINISTRADOR")]
    public class HistorialController : ControllerBase
    {
        private readonly HistorialRepo _repo;
        private readonly EquiposRepo _equipos; // para listar equipos si lo necesitas
        public HistorialController(HistorialRepo repo, EquiposRepo equipos)
        {
            _repo = repo;
            _equipos = equipos;
        }

        // Lista de partidos finalizados (opcional: filtrar por equipo)
        [HttpGet("partidos")]
        public async Task<ActionResult<IEnumerable<PartidoHistDto>>> GetPartidos([FromQuery] int? equipoId)
            => Ok(await _repo.GetPartidosFinalizadosAsync(equipoId));

        // (Opcional) Lista simple de equipos para el filtro del front
        [HttpGet("equipos")]
        public async Task<ActionResult> GetEquipos()
            => Ok(await _equipos.GetAllAsync());
    }
}
