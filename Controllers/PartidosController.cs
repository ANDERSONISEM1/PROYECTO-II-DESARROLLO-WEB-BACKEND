// Api/Controllers/PartidosController.cs
using Api.Data;
using Api.Hubs;
using Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Api.Controllers;

[ApiController]
[Route("api/partidos")]
public class PartidosController : ControllerBase
{
    private readonly PartidosRepo _repo;
    private readonly IHubContext<MarcadorHub> _hub;

    public PartidosController(PartidosRepo repo, IHubContext<MarcadorHub> hub)
    {
        _repo = repo;
        _hub = hub;
    }

    [HttpPost("start")]
    public async Task<ActionResult<StartPartidoResponse>> Start([FromBody] StartPartidoRequest body)
    {
        if (body is null || body.EquipoLocalId <= 0 || body.EquipoVisitanteId <= 0)
            return BadRequest(new { message = "EquipoLocalId y EquipoVisitanteId son obligatorios." });

        if (body.EquipoLocalId == body.EquipoVisitanteId)
            return BadRequest(new { message = "Los equipos deben ser distintos." });

        int partidoId;
        try
        {
            partidoId = await _repo.EnsurePartidoAsync(
                localId: body.EquipoLocalId,
                visitId: body.EquipoVisitanteId,
                minutosPorCuarto: body.MinutosPorCuarto ?? 10,
                cuartosTotales: body.CuartosTotales ?? 4,
                llenarRoster: body.LlenarRoster ?? true
            );
        }
        catch (Exception ex)
        {
            // 👉 Si el repo aún intenta insertar columnas que ya borraste (sede, observaciones),
            // verás el detalle aquí para corregir el repo rápido.
            return StatusCode(500, new { message = "Error al crear/iniciar partido.", detail = ex.Message });
        }

        var (localMini, visitMini) = await _repo.GetEquiposMiniAsync(body.EquipoLocalId, body.EquipoVisitanteId);

        var resp = new StartPartidoResponse
        {
            PartidoId = partidoId,
            Estado = "en_curso",
            Local = new EquipoMiniDto {
                Id = localMini.Id,
                Nombre = localMini.Nombre,
                Abreviatura = localMini.Abreviatura
            },
            Visitante = new EquipoMiniDto {
                Id = visitMini.Id,
                Nombre = visitMini.Nombre,
                Abreviatura = visitMini.Abreviatura
            }
        };

        return Ok(resp);
    }

    [HttpGet("abierto")]
    public async Task<ActionResult<object>> GetAbierto([FromQuery] int localId, [FromQuery] int visitId)
    {
        if (localId <= 0 || visitId <= 0)
            return BadRequest(new { message = "localId y visitId son obligatorios." });

        var id = await _repo.GetPartidoAbiertoAsync(localId, visitId);
        if (!id.HasValue) return NotFound();

        return Ok(new { partidoId = id.Value });
    }

    [HttpPost("{partidoId:int}/finalizar")]
    public async Task<ActionResult> Finalizar(int partidoId)
    {
        var ok = await _repo.FinalizarAsync(partidoId);

        if (ok)
        {
            await _hub.Clients.All.SendAsync("serverMessage", $"Partido #{partidoId} finalizado y guardado.");
            await _hub.Clients.All.SendAsync("partidoCerrado", new { partidoId });

            // (Opcional) Dejar timer/periodo en estado “base” visualmente
            await _hub.Clients.All.SendAsync("timerSync", new MarcadorHub.TimerState {
                Phase = "stopped",
                DurationSec = 600,
                RemainingSec = 600
            });
            await _hub.Clients.All.SendAsync("periodSync", new MarcadorHub.PeriodState {
                Numero = 1, Total = 4, EsProrroga = false, Rotulo = null
            });
            return Ok(new { ok = true });
        }

        return NotFound(new { ok = false });
    }

    [HttpDelete("{partidoId:int}/reset")]
    public async Task<ActionResult> Reset(int partidoId)
    {
        var ok = await _repo.BorrarAsync(partidoId);
        if (!ok) return NotFound(new { ok = false });

        await _hub.Clients.All.SendAsync("partidoReset", new { partidoId });
        return Ok(new { ok = true, partidoId });
    }

    [HttpDelete("{partidoId:int}")]
    public async Task<ActionResult> Delete(int partidoId)
    {
        var ok = await _repo.BorrarAsync(partidoId);
        if (!ok) return NotFound(new { ok = false });

        await _hub.Clients.All.SendAsync("partidoReset", new { partidoId });
        return Ok(new { ok = true, partidoId });
    }
}
