// Api/Controllers/JugadoresController.cs
using Microsoft.AspNetCore.Mvc;
using Api.Data;

namespace Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class JugadoresController : ControllerBase
{
    private readonly JugadoresRepo _repo;
    public JugadoresController(JugadoresRepo repo) => _repo = repo;

    // GET /api/jugadores/por-equipo/2?activos=true
    [HttpGet("por-equipo/{equipoId:int}")]
    public async Task<IActionResult> GetPorEquipo(int equipoId, [FromQuery] bool activos = true)
    {
        if (equipoId <= 0) return BadRequest(new { error = "equipoId inválido" });
        var data = await _repo.GetJugadoresPorEquipoAsync(equipoId, activos);
        return Ok(data);
    }
}
