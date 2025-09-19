using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace Api.Hubs
{
    public class MarcadorHub : Hub
    {
        // ===== Marcador simple =====
        public class ScorePayload
        {
            public int PartidoId { get; set; }
            public int Local { get; set; }
            public int Visitante { get; set; }
        }

        public async Task BroadcastScoreSimple(int partidoId, int local, int visitante)
        {
            await Clients.All.SendAsync("scoreUpdated", new ScorePayload
            {
                PartidoId = partidoId,
                Local = local,
                Visitante = visitante
            });
        }

        // ===== Timer =====
        public class TimerState
        {
            public string Phase { get; set; } = "stopped"; // running | paused | stopped | finished
            public int DurationSec { get; set; } = 600;
            public long? StartedAtUnixMs { get; set; }
            public int? RemainingSec { get; set; }
        }

        public async Task TimerControl(TimerState state)
        {
            await Clients.All.SendAsync("timerSync", state);
        }

        // ===== Periodo =====
        public class PeriodState
        {
            public int Numero { get; set; }
            public int Total { get; set; }
            public bool EsProrroga { get; set; }
            public string? Rotulo { get; set; } // "Descanso" | "Medio tiempo" | null
        }

        public async Task PeriodControl(PeriodState state)
        {
            await Clients.All.SendAsync("periodSync", state);
        }

        // ===== Equipos =====
        public class EquipoMini
        {
            public int Id { get; set; }
            public string Nombre { get; set; } = "";
            public string? Abreviatura { get; set; }
            public string? Color { get; set; }
        }
        public class TeamsState
        {
            public EquipoMini? Local { get; set; }
            public EquipoMini? Visitante { get; set; }
        }

        public async Task TeamsControl(TeamsState teams)
        {
            await Clients.All.SendAsync("teamsSync", teams);
        }
    }
}
