namespace Api.Models;

public class PeriodStateDto
{
    public int  Numero { get; set; }
    public int  Total  { get; set; }
    public bool EsProrroga { get; set; }
    public string? Rotulo { get; set; } // "Descanso" | "Medio tiempo" | null
}

public class SetCuartoRequest
{
    public int Numero { get; set; }
}

public class RotuloRequest
{
    /// <summary>"Descanso" | "Medio tiempo"</summary>
    public string Rotulo { get; set; } = "Descanso";
}
