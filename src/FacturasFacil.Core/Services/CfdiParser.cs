using System.Xml.Linq;
using FacturasFacil.Core.Models;

namespace FacturasFacil.Core.Services;

public static class CfdiParser
{
    private static readonly XNamespace NsCfdi3 = "http://www.sat.gob.mx/cfd/3";
    private static readonly XNamespace NsCfdi4 = "http://www.sat.gob.mx/cfd/4";
    private static readonly XNamespace NsTfd = "http://www.sat.gob.mx/TimbreFiscalDigital";

    public static FacturaInfo Parse(Stream xmlStream, string nombreArchivo)
    {
        var doc = XDocument.Load(xmlStream);
        var root = doc.Root ?? throw new InvalidDataException("XML vacío o inválido.");

        var ns = root.Name.Namespace;
        if (ns != NsCfdi3 && ns != NsCfdi4)
            throw new InvalidDataException($"Namespace CFDI no reconocido: {ns}");

        var factura = new FacturaInfo
        {
            ArchivoXml = nombreArchivo,
            Version = root.Attribute("Version")?.Value ?? string.Empty,
            Serie = root.Attribute("Serie")?.Value ?? string.Empty,
            Folio = root.Attribute("Folio")?.Value ?? string.Empty,
            TipoComprobante = root.Attribute("TipoDeComprobante")?.Value ?? string.Empty,
            Moneda = root.Attribute("Moneda")?.Value ?? "MXN",
            MetodoPago = root.Attribute("MetodoPago")?.Value ?? string.Empty,
            FormaPago = root.Attribute("FormaPago")?.Value ?? string.Empty,
            LugarExpedicion = root.Attribute("LugarExpedicion")?.Value ?? string.Empty,
        };

        if (DateTime.TryParse(root.Attribute("Fecha")?.Value, out var fecha))
            factura.Fecha = fecha;

        if (decimal.TryParse(root.Attribute("SubTotal")?.Value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var subtotal))
            factura.SubTotal = subtotal;

        if (decimal.TryParse(root.Attribute("Total")?.Value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var total))
            factura.Total = total;

        if (decimal.TryParse(root.Attribute("Descuento")?.Value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var descuento))
            factura.Descuento = descuento;

        if (decimal.TryParse(root.Attribute("TipoCambio")?.Value,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var tipoCambio))
            factura.TipoCambio = tipoCambio;

        // Emisor
        var emisor = root.Element(ns + "Emisor");
        if (emisor != null)
        {
            factura.RfcEmisor = emisor.Attribute("Rfc")?.Value ?? string.Empty;
            factura.NombreEmisor = emisor.Attribute("Nombre")?.Value ?? string.Empty;
            factura.RegimenFiscalEmisor = emisor.Attribute("RegimenFiscal")?.Value ?? string.Empty;
        }

        // Receptor
        var receptor = root.Element(ns + "Receptor");
        if (receptor != null)
        {
            factura.RfcReceptor = receptor.Attribute("Rfc")?.Value ?? string.Empty;
            factura.NombreReceptor = receptor.Attribute("Nombre")?.Value ?? string.Empty;
            factura.UsoCFDI = receptor.Attribute("UsoCFDI")?.Value ?? string.Empty;
        }

        // Conceptos
        var conceptos = root.Element(ns + "Conceptos");
        if (conceptos != null)
        {
            foreach (var concepto in conceptos.Elements(ns + "Concepto"))
            {
                var c = new ConceptoInfo
                {
                    ClaveProdServ = concepto.Attribute("ClaveProdServ")?.Value ?? string.Empty,
                    ClaveUnidad = concepto.Attribute("ClaveUnidad")?.Value ?? string.Empty,
                    NoIdentificacion = concepto.Attribute("NoIdentificacion")?.Value ?? string.Empty,
                    Unidad = concepto.Attribute("Unidad")?.Value ?? string.Empty,
                    Descripcion = concepto.Attribute("Descripcion")?.Value ?? string.Empty,
                };
                if (decimal.TryParse(concepto.Attribute("Cantidad")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var cant)) c.Cantidad = cant;
                if (decimal.TryParse(concepto.Attribute("ValorUnitario")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var vu)) c.ValorUnitario = vu;
                if (decimal.TryParse(concepto.Attribute("Importe")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var imp)) c.Importe = imp;
                if (decimal.TryParse(concepto.Attribute("Descuento")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var desc)) c.Descuento = desc;
                factura.Conceptos.Add(c);
            }
        }

        // Impuestos totales
        var impuestos = root.Element(ns + "Impuestos");
        if (impuestos != null)
        {
            if (decimal.TryParse(impuestos.Attribute("TotalImpuestosTrasladados")?.Value,
                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ivaTotal))
            {
                // Desglosamos IVA e IEPS desde los traslados
                var traslados = impuestos.Element(ns + "Traslados");
                if (traslados != null)
                {
                    foreach (var t in traslados.Elements(ns + "Traslado"))
                    {
                        var impuesto = t.Attribute("Impuesto")?.Value;
                        if (!decimal.TryParse(t.Attribute("Importe")?.Value,
                            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var importe))
                            continue;

                        if (impuesto == "002") factura.IVA += importe;   // IVA
                        else if (impuesto == "003") factura.IEPS += importe; // IEPS
                    }
                }
            }

            var retenciones = impuestos.Element(ns + "Retenciones");
            if (retenciones != null)
            {
                foreach (var r in retenciones.Elements(ns + "Retencion"))
                {
                    var impuesto = r.Attribute("Impuesto")?.Value;
                    if (!decimal.TryParse(r.Attribute("Importe")?.Value,
                        System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var importe))
                        continue;

                    if (impuesto == "001") factura.ISR += importe;   // ISR
                    else if (impuesto == "002") factura.IVA -= importe; // IVA retenido (se resta)
                }
            }
        }

        // UUID desde TimbreFiscalDigital
        var complemento = root.Element(ns + "Complemento");
        if (complemento != null)
        {
            var timbre = complemento.Element(NsTfd + "TimbreFiscalDigital");
            if (timbre != null)
                factura.UUID = timbre.Attribute("UUID")?.Value ?? string.Empty;
        }

        return factura;
    }
}
