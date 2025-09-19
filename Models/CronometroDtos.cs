namespace Api.Models;

public class CronometroEventRequest
{
    /// <summary>inicio | pausa | reanudar | fin | prorroga | descanso | medio | reiniciar</summary>
    public string Tipo { get; set; } = "inicio";

    /// <summary>Segundos restantes que ve el front al momento del evento (si aplica).</summary>
    public int? SegundosRestantes { get; set; }

    /// <summary>Si no mandas CuartoId, envía el número y si es prórroga para resolver/crear el cuarto.</summary>
    public int? NumeroCuarto { get; set; }

    /// <summary>True si el cuarto es prórroga.</summary>
    public bool? EsProrroga { get; set; }

    /// <summary>Opcional si ya lo conoces; si no, se resuelve con NumeroCuarto+EsProrroga.</summary>
    public int? CuartoId { get; set; }
}

public class CronometroEventResponse
{
    public long EventoId { get; set; }
    public int PartidoId { get; set; }
    public int CuartoId { get; set; }
    public string Tipo { get; set; } = "";
    public int? SegundosRestantes { get; set; }
    public System.DateTime CreadoEn { get; set; }
}
