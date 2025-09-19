// Api/Controllers/CronometroController.cs
using Microsoft.AspNetCore.Mvc;
using Api.Data;
using Api.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/partidos/{partidoId:int}/cronometro")]
public class CronometroController : ControllerBase
{
    // ✅ incluye "reiniciar"
    private static readonly HashSet<string> TiposValidos = new(new[] {
        "inicio","pausa","reanudar","fin","prorroga","descanso","medio","reiniciar"
    });

    private readonly CronometroRepo _repo;

    public CronometroController(CronometroRepo repo)
    {
        _repo = repo;
    }

    /// <summary>Registra un evento de cronómetro SIEMPRE con cuarto_id (lo resuelve/crea si falta).</summary>
    [HttpPost("evento")]
    public async Task<ActionResult<CronometroEventResponse>> RegistrarEvento(
        int partidoId, [FromBody] CronometroEventRequest body)
    {
        if (body is null) return BadRequest(new { error = "Body requerido." });

        var tipo = (body.Tipo ?? "").Trim().ToLowerInvariant();
        if (!TiposValidos.Contains(tipo))
            return BadRequest(new { error = "Tipo inválido." });

        // Validar que el partido existe (y que tiene equipos)
        var (loc, vis) = await _repo.GetEquiposDelPartidoAsync(partidoId);
        if (loc == 0 || vis == 0) return NotFound(new { error = "Partido no existe." });

        // Resolver SIEMPRE cuarto_id:
        int cuartoId;
        if (body.CuartoId.HasValue) cuartoId = body.CuartoId.Value;
        else
        {
            if (!body.NumeroCuarto.HasValue || !body.EsProrroga.HasValue)
                return BadRequest(new { error = "Falta NumeroCuarto/EsProrroga para resolver el cuarto." });

            // Garantizar que exista (p.ej. prórroga)
            cuartoId = await _repo.EnsureCuartoAsync(partidoId, body.NumeroCuarto.Value, body.EsProrroga.Value);
        }

        var evId = await _repo.AddEventoAsync(partidoId, cuartoId, tipo, body.SegundosRestantes);

        return Ok(new CronometroEventResponse
        {
            EventoId = evId,
            PartidoId = partidoId,
            CuartoId = cuartoId,
            Tipo = tipo,
            SegundosRestantes = body.SegundosRestantes,
            CreadoEn = System.DateTime.UtcNow
        });
    }

    /// <summary>Vacía solo los eventos del cronómetro del partido.</summary>
    [HttpDelete("reset")]
    public async Task<ActionResult> Reset(int partidoId)
    {
        await _repo.ResetEventosAsync(partidoId);
        return Ok();
    }
}
