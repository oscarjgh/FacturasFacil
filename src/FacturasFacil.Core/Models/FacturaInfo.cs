namespace FacturasFacil.Core.Models;

public class FacturaInfo
{
    public string UUID { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Serie { get; set; } = string.Empty;
    public string Folio { get; set; } = string.Empty;
    public DateTime Fecha { get; set; }
    public string TipoComprobante { get; set; } = string.Empty;
    public string TipoComprobanteDescripcion => TipoComprobante switch
    {
        "I" => "Ingreso",
        "E" => "Egreso",
        "T" => "Traslado",
        "N" => "Nómina",
        "P" => "Pago",
        _ => TipoComprobante
    };

    public string RfcEmisor { get; set; } = string.Empty;
    public string NombreEmisor { get; set; } = string.Empty;
    public string RegimenFiscalEmisor { get; set; } = string.Empty;

    public string RfcReceptor { get; set; } = string.Empty;
    public string NombreReceptor { get; set; } = string.Empty;
    public string UsoCFDI { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }
    public decimal Descuento { get; set; }
    public decimal IVA { get; set; }
    public decimal IEPS { get; set; }
    public decimal ISR { get; set; }
    public decimal Total { get; set; }
    public string Moneda { get; set; } = string.Empty;
    public decimal TipoCambio { get; set; } = 1;

    public string MetodoPago { get; set; } = string.Empty;
    public string FormaPago { get; set; } = string.Empty;
    public string LugarExpedicion { get; set; } = string.Empty;

    public string ArchivoOrigen { get; set; } = string.Empty;
    public string ArchivoXml { get; set; } = string.Empty;

    public List<ConceptoInfo> Conceptos { get; set; } = [];
}

public class ConceptoInfo
{
    public string ClaveProdServ { get; set; } = string.Empty;
    public string ClaveUnidad { get; set; } = string.Empty;
    public string NoIdentificacion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public string Unidad { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public decimal ValorUnitario { get; set; }
    public decimal Importe { get; set; }
    public decimal Descuento { get; set; }
}
