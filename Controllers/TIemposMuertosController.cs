// Api/Controllers/TiemposMuertosController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Api.Data;
using Api.Hubs;
using Api.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/partidos/{partidoId:int}/tiempos-muertos")]
public class TiemposMuertosController : ControllerBase
{
    private readonly TiemposMuertosRepo _repo;
    private readonly IHubContext<MarcadorHub> _hub;

    public TiemposMuertosController(TiemposMuertosRepo repo, IHubContext<MarcadorHub> hub)
    {
        _repo = repo;
        _hub = hub;
    }

    [HttpGet("resumen")]
    public async Task<ActionResult<TiemposMuertosResumenDto>> GetResumen(int partidoId)
    {
        var res = await _repo.GetResumenAsync(partidoId);
        return Ok(res);
    }

    [HttpPost("ajustar")]
    public async Task<ActionResult<TiemposMuertosResumenDto>> Ajustar(int partidoId, [FromBody] AjusteTiempoMuertoDto body)
    {
        if (body is null)
            return BadRequest(new { error = "Body requerido." });

        if (body.Delta != 1 && body.Delta != -1)
            return BadRequest(new { error = "Delta debe ser +1 o -1" });

        var tipo = (body.Tipo ?? "").Trim().ToLowerInvariant();
        if (tipo != "corto" && tipo != "largo")
            return BadRequest(new { error = "Tipo inválido. Debe ser 'corto' o 'largo'." });

        // Validar pertenencia del equipo al partido
        var equipos = await _repo.GetEquiposDelPartidoAsync(partidoId);
        if (equipos.localId == 0 || equipos.visitId == 0)
            return NotFound(new { error = "Partido no existe." });

        if (body.EquipoId != equipos.localId && body.EquipoId != equipos.visitId)
            return BadRequest(new { error = "Equipo no pertenece a este partido." });

        // === Determinar cuarto_id (si no viene), con NumeroCuarto + EsProrroga ===
        int? cuartoId = body.CuartoId;
        if (!cuartoId.HasValue && body.NumeroCuarto.HasValue)
        {
            cuartoId = await _repo.ResolveCuartoIdAsync(partidoId, body.NumeroCuarto.Value, body.EsProrroga ?? false);
            // Si no existe (p.ej. prórroga no creada), dejamos null y solo registramos el TM al partido.
        }

        if (body.Delta == 1)
            await _repo.AddAsync(partidoId, body.EquipoId, tipo, cuartoId);
        else
            await _repo.RemoveLastAsync(partidoId, body.EquipoId, tipo);

        var resumen = await _repo.GetResumenAsync(partidoId);

        // Notificar a todos los clientes (Control/Visor)
        await _hub.Clients.All.SendAsync("timeoutsSync", resumen);

        return Ok(resumen);
    }

    [HttpDelete("reset")]
    public async Task<ActionResult<TiemposMuertosResumenDto>> Reset(int partidoId)
    {
        await _repo.ResetAsync(partidoId);
        var resumen = await _repo.GetResumenAsync(partidoId);

        // broadcast para dejar ambos clientes en 0
        await _hub.Clients.All.SendAsync("timeoutsSync", resumen);

        return Ok(resumen);
    }
}
