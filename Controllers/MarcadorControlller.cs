using Api.Data;
using Api.Hubs;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Api.Controllers;

[ApiController]
[Route("api")]
public class MarcadorController : ControllerBase
{
    private readonly MarcadorRepo _repo;
    private readonly IHubContext<MarcadorHub> _hub;
    public MarcadorController(MarcadorRepo repo, IHubContext<MarcadorHub> hub)
    {
        _repo = repo;
        _hub  = hub;
    }

    // GET /api/equipos
    [HttpGet("equipos")]
    public async Task<IActionResult> GetEquipos()
    {
        var list = await _repo.GetEquiposAsync();
        return Ok(list);
    }

    // ====== MARCADOR ======

    // GET /api/partidos/{id}/marcador
    [HttpGet("partidos/{partidoId:int}/marcador")]
    public async Task<ActionResult<Marcador>> GetMarcador(int partidoId)
    {
        var m = await _repo.GetMarcadorAsync(partidoId);
        if (m is null) return NotFound();
        return Ok(m);
    }

    // POST /api/partidos/{id}/anotaciones/ajustar
    [HttpPost("partidos/{partidoId:int}/anotaciones/ajustar")]
    public async Task<ActionResult<Marcador>> AjustarAnotacion(
        int partidoId,
        [FromBody] AjustarAnotacionRequest body)
    {
        if (body is null || body.EquipoId <= 0 || body.Puntos is 0 or > 3 or < -3)
            return BadRequest("Datos inválidos. EquipoId y Puntos (-3..-1 o 1..3) son obligatorios.");

        Marcador marcador;
        try
        {
            // ✅ Ahora el repo resuelve SIEMPRE un cuarto_id válido (o crea el 1/OT).
            marcador = await _repo.AjustarAnotacionAsync(
                partidoId: partidoId,
                equipoId:  body.EquipoId,
                puntos:    body.Puntos,
                cuartoId:  body.CuartoId,
                numeroCuarto: body.NumeroCuarto,
                esProrroga:   body.EsProrroga
            );
        }
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }

        // Broadcast a todos los clientes
        await _hub.Clients.All.SendAsync("scoreUpdated", new
        {
            PartidoId = marcador.PartidoId,
            Local     = marcador.Local,
            Visitante = marcador.Visitante
        });

        return Ok(marcador);
    }

    // DELETE /api/partidos/{id}/anotaciones/reset
    [HttpDelete("partidos/{partidoId:int}/anotaciones/reset")]
    public async Task<IActionResult> ResetAnotaciones(int partidoId)
    {
        await _repo.ResetAnotacionesAsync(partidoId);
        return Ok();
    }
}
