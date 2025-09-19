using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Api.Data;
using Api.Hubs;
using Api.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Api.Controllers;

[ApiController]
[Route("api/partidos/{partidoId:int}/faltas")]
public class FaltasController : ControllerBase
{
    private readonly FaltasRepo _repo;
    private readonly IHubContext<MarcadorHub> _hub;

    public FaltasController(FaltasRepo repo, IHubContext<MarcadorHub> hub)
    {
        _repo = repo;
        _hub = hub;
    }

    [HttpGet("resumen")]
    public async Task<ActionResult<FaltasResumenDto>> GetResumen(int partidoId)
    {
        var res = await _repo.GetResumenAsync(partidoId);
        return Ok(res);
    }

    [HttpPost("ajustar")]
    public async Task<ActionResult<FaltasResumenDto>> Ajustar(int partidoId, [FromBody] AjusteFaltaDto body)
    {
        if (body is null) return BadRequest(new { error = "Body requerido." });
        if (body.Delta != 1 && body.Delta != -1)
            return BadRequest(new { error = "Delta debe ser +1 o -1" });

        // === Resolver SIEMPRE un cuarto_id válido ===
        var cuartoIdFirm = await _repo.ResolveCuartoIdPreferenteAsync(
            partidoId,
            body.CuartoId,
            body.NumeroCuarto,
            body.EsProrroga
        );

        if (body.Delta == 1)
            await _repo.AddPersonalFaltaAsync(partidoId, body.EquipoId, body.JugadorId, cuartoIdFirm);
        else
            await _repo.RemoveLastPersonalFaltaAsync(partidoId, body.EquipoId, body.JugadorId, cuartoIdFirm);

        var resumen = await _repo.GetResumenAsync(partidoId);

        // 🔔 Notificar a TODOS (panel + visor)
        await _hub.Clients.All.SendAsync("foulsSync", resumen);

        return Ok(resumen);
    }

    // Reset total de faltas del partido (borra en BD y notifica vacío)
    [HttpDelete("reset")]
    public async Task<ActionResult> Reset(int partidoId)
    {
        var deleted = await _repo.ResetFaltasPartidoAsync(partidoId);

        var empty = new FaltasResumenDto
        {
            PartidoId = partidoId,
            Local = new TeamFoulsDto
            {
                EquipoId = 0,
                EquipoNombre = string.Empty,
                Jugadores = new List<PlayerFoulsDto>(),
                Fuera5 = new List<PlayerFoulsDto>(),
                TotalEquipo = 0
            },
            Visitante = new TeamFoulsDto
            {
                EquipoId = 0,
                EquipoNombre = string.Empty,
                Jugadores = new List<PlayerFoulsDto>(),
                Fuera5 = new List<PlayerFoulsDto>(),
                TotalEquipo = 0
            }
        };

        await _hub.Clients.All.SendAsync("foulsSync", empty);

        return Ok(new { deleted, resumen = empty });
    }
}
