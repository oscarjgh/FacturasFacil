namespace FacturasFacil.Api.Models;

public class HistorialExcel
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    public DateTime FechaGeneracion { get; set; } = DateTime.UtcNow;
    public string NombreArchivo { get; set; } = string.Empty;
    public string RutaArchivo { get; set; } = string.Empty;
    public int TotalFacturas { get; set; }
    public int TotalArchivosSubidos { get; set; }
    public long TamanioBytes { get; set; }
}
