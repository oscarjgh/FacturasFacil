namespace FacturasFacil.Core.Models;

public class ProcesamientoResultado
{
    public List<FacturaInfo> Facturas { get; set; } = [];
    public List<ErrorArchivo> Errores { get; set; } = [];
    public string? RutaExcel { get; set; }
    public int TotalArchivos { get; set; }
    public int ArchivosExitosos { get; set; }
    public int ArchivosConError { get; set; }
}

public class ErrorArchivo
{
    public string Archivo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
}
