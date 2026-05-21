namespace FacturasFacil.Api.Models;

public class Plan
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public int LimiteFacturasMes { get; set; } // -1 = ilimitado
    public decimal PrecioMensual { get; set; }
    public string? StripePriceId { get; set; }
    public bool Activo { get; set; } = true;
    public string[] Caracteristicas { get; set; } = [];
}
