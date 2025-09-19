// Api/Controllers/CuartosController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Api.Data;
using Api.Hubs;
using Api.Models;

namespace Api.Controllers;

[ApiController]
[Route("api/partidos/{partidoId:int}/cuartos")]
public class CuartosController : ControllerBase
{
    private readonly CuartosRepo _repo;
    private readonly IHubContext<MarcadorHub> _hub;

    public CuartosController(CuartosRepo repo, IHubContext<MarcadorHub> hub)
    {
        _repo = repo;
        _hub = hub;
    }

    [HttpGet("resumen")]
    public async Task<ActionResult<PeriodStateDto>> GetResumen(int partidoId)
    {
      var dto = await _repo.GetResumenAsync(partidoId);
      return Ok(dto);
    }

    [HttpPost("iniciar")]
    public async Task<ActionResult<PeriodStateDto>> Iniciar(int partidoId)
    {
      var dto = await _repo.IniciarAsync(partidoId);
      string msg =
          dto.EsProrroga ? "Inicia prórroga."
        : dto.Numero == 1 ? "Inicia el primer cuarto."
        : $"Avanza al siguiente cuarto ({dto.Numero}/{dto.Total}).";
      await _hub.Clients.All.SendAsync("periodSync", dto);
      await _hub.Clients.All.SendAsync("serverMessage", msg);
      return Ok(dto);
    }

    [HttpPost("reiniciar")]
    public async Task<ActionResult<PeriodStateDto>> Reiniciar(int partidoId)
    {
      var dto = await _repo.ReiniciarAsync(partidoId);
      string ord = OrdinalEs(dto.Numero);
      string msg = dto.EsProrroga ? "Reinicia la prórroga." : $"Reinicia el {ord} cuarto.";
      await _hub.Clients.All.SendAsync("periodSync", dto);
      await _hub.Clients.All.SendAsync("serverMessage", msg);
      return Ok(dto);
    }

    [HttpPost("finalizar")]
    public async Task<ActionResult<PeriodStateDto>> Finalizar(int partidoId)
    {
      var dto = await _repo.FinalizarAsync(partidoId);
      var rotulo = (!dto.EsProrroga && dto.Numero == 2) ? "Medio tiempo" : "Descanso";
      var show = new PeriodStateDto { Numero = dto.Numero, Total = dto.Total, EsProrroga = dto.EsProrroga, Rotulo = rotulo };
      string msg = dto.EsProrroga ? "Se finalizó la prórroga." : $"Se finalizó el {OrdinalEs(dto.Numero)} cuarto.";
      await _hub.Clients.All.SendAsync("periodSync", show);
      await _hub.Clients.All.SendAsync("serverMessage", msg);
      return Ok(show);
    }

    [HttpPost("set/{numero:int}")]
    public async Task<ActionResult<PeriodStateDto>> SetNumero(int partidoId, int numero)
    {
      var dto = await _repo.SetNumeroAsync(partidoId, numero);
      string msg = dto.EsProrroga ? "Prórroga seleccionada." : $"Se estableció el {OrdinalEs(dto.Numero)} cuarto.";
      await _hub.Clients.All.SendAsync("periodSync", dto);
      await _hub.Clients.All.SendAsync("serverMessage", msg);
      return Ok(dto);
    }

    [HttpPost("siguiente")]
    public async Task<ActionResult<PeriodStateDto>> Siguiente(int partidoId)
    {
      var dto = await _repo.SiguienteAsync(partidoId);
      string msg = dto.EsProrroga ? "Avanza a prórroga." : $"Avanza al {OrdinalEs(dto.Numero)} cuarto.";
      await _hub.Clients.All.SendAsync("periodSync", dto);
      await _hub.Clients.All.SendAsync("serverMessage", msg);
      return Ok(dto);
    }

    // 🚫 NO se puede volver al cuarto anterior
    [HttpPost("anterior")]
    public async Task<IActionResult> Anterior(int partidoId)
    {
      const string msg = "No se puede volver al cuarto anterior.";
      await _hub.Clients.All.SendAsync("serverMessage", msg);
      return Conflict(new { message = msg });
    }

    [HttpPost("prorroga")]
    public async Task<ActionResult<PeriodStateDto>> Prorroga(int partidoId)
    {
      var dto = await _repo.ProrrogaAsync(partidoId);
      await _hub.Clients.All.SendAsync("periodSync", dto);
      await _hub.Clients.All.SendAsync("serverMessage", "Inicia prórroga.");
      return Ok(dto);
    }

    private static string OrdinalEs(int n) => n switch
    {
      1 => "primer",
      2 => "segundo",
      3 => "tercer",
      4 => "cuarto",
      _ => $"{n}°"
    };
}
