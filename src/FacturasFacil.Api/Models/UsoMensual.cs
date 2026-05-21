namespace FacturasFacil.Api.Models;

public class UsoMensual
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public int Mes { get; set; }
    public int Anno { get; set; }
    public int FacturasProcesadas { get; set; }
    public DateTime UltimaActualizacion { get; set; } = DateTime.UtcNow;
}
