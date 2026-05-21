using ClosedXML.Excel;
using FacturasFacil.Core.Models;

namespace FacturasFacil.Core.Services;

public static class ExcelGenerator
{
    public static byte[] Generar(List<FacturaInfo> facturas)
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Facturas");

        // Encabezados
        var columnas = new[]
        {
            "UUID (Folio Fiscal)", "Versión", "Fecha", "Serie", "Folio",
            "Tipo", "RFC Emisor", "Nombre Emisor", "RFC Receptor", "Nombre Receptor",
            "Uso CFDI", "SubTotal", "Descuento", "IVA", "IEPS", "ISR Retenido",
            "Total", "Moneda", "Tipo Cambio", "Método Pago", "Forma Pago",
            "Lugar Expedición", "Archivo Origen"
        };

        for (int i = 0; i < columnas.Length; i++)
            ws.Cell(1, i + 1).Value = columnas[i];

        // Estilo de encabezado
        var headerRange = ws.Range(1, 1, 1, columnas.Length);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E5C9E");
        headerRange.Style.Font.FontColor = XLColor.White;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Border.BottomBorder = XLBorderStyleValues.Medium;

        // Datos (ya ordenados por fecha)
        int fila = 2;
        foreach (var f in facturas.OrderBy(x => x.Fecha))
        {
            ws.Cell(fila, 1).Value = f.UUID;
            ws.Cell(fila, 2).Value = f.Version;
            ws.Cell(fila, 3).Value = f.Fecha;
            ws.Cell(fila, 3).Style.DateFormat.Format = "dd/mm/yyyy hh:mm:ss";
            ws.Cell(fila, 4).Value = f.Serie;
            ws.Cell(fila, 5).Value = f.Folio;
            ws.Cell(fila, 6).Value = f.TipoComprobanteDescripcion;
            ws.Cell(fila, 7).Value = f.RfcEmisor;
            ws.Cell(fila, 8).Value = f.NombreEmisor;
            ws.Cell(fila, 9).Value = f.RfcReceptor;
            ws.Cell(fila, 10).Value = f.NombreReceptor;
            ws.Cell(fila, 11).Value = f.UsoCFDI;
            ws.Cell(fila, 12).Value = f.SubTotal;
            ws.Cell(fila, 13).Value = f.Descuento;
            ws.Cell(fila, 14).Value = f.IVA;
            ws.Cell(fila, 15).Value = f.IEPS;
            ws.Cell(fila, 16).Value = f.ISR;
            ws.Cell(fila, 17).Value = f.Total;
            ws.Cell(fila, 18).Value = f.Moneda;
            ws.Cell(fila, 19).Value = f.TipoCambio;
            ws.Cell(fila, 20).Value = f.MetodoPago;
            ws.Cell(fila, 21).Value = f.FormaPago;
            ws.Cell(fila, 22).Value = f.LugarExpedicion;
            ws.Cell(fila, 23).Value = f.ArchivoOrigen;

            // Formato numérico para montos
            for (int col = 12; col <= 17; col++)
                ws.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";

            // Filas alternadas
            if (fila % 2 == 0)
                ws.Row(fila).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4FA");

            fila++;
        }

        // Ajuste automático de columnas
        ws.ColumnsUsed().AdjustToContents();

        // Hoja de resumen
        AgregarHojaResumen(workbook, facturas);

        // Hoja de conceptos
        if (facturas.Any(f => f.Conceptos.Count > 0))
            AgregarHojaConceptos(workbook, facturas);

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private static void AgregarHojaResumen(XLWorkbook wb, List<FacturaInfo> facturas)
    {
        var ws = wb.Worksheets.Add("Resumen");

        ws.Cell("A1").Value = "Resumen de Facturas Procesadas";
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Range("A1:B1").Merge();

        var filaActual = 3;
        void AgregarFila(string etiqueta, object valor)
        {
            ws.Cell(filaActual, 1).Value = etiqueta;
            ws.Cell(filaActual, 1).Style.Font.Bold = true;
            ws.Cell(filaActual, 2).Value = valor?.ToString() ?? string.Empty;
            filaActual++;
        }

        AgregarFila("Total de facturas:", facturas.Count);
        AgregarFila("Fecha de procesamiento:", DateTime.Now.ToString("dd/MM/yyyy HH:mm"));

        filaActual++;
        ws.Cell(filaActual, 1).Value = "Por tipo de comprobante:";
        ws.Cell(filaActual, 1).Style.Font.Bold = true;
        filaActual++;

        foreach (var grupo in facturas.GroupBy(f => f.TipoComprobanteDescripcion).OrderBy(g => g.Key))
        {
            ws.Cell(filaActual, 1).Value = $"  {grupo.Key}";
            ws.Cell(filaActual, 2).Value = grupo.Count();
            ws.Cell(filaActual, 3).Value = grupo.Sum(f => f.Total);
            ws.Cell(filaActual, 3).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(filaActual, 4).Value = grupo.First().Moneda;
            filaActual++;
        }

        filaActual++;
        ws.Cell(filaActual, 1).Value = "Total general:";
        ws.Cell(filaActual, 1).Style.Font.Bold = true;
        ws.Cell(filaActual, 2).Value = facturas.Sum(f => f.Total);
        ws.Cell(filaActual, 2).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(filaActual, 2).Style.Font.Bold = true;

        ws.ColumnsUsed().AdjustToContents();
    }

    private static void AgregarHojaConceptos(XLWorkbook wb, List<FacturaInfo> facturas)
    {
        var ws = wb.Worksheets.Add("Conceptos");

        var cols = new[] { "UUID Factura", "Fecha", "RFC Emisor", "RFC Receptor", "ClaveProdServ", "ClaveUnidad", "No. Identificación", "Cantidad", "Unidad", "Descripción", "Valor Unitario", "Importe", "Descuento" };
        for (int i = 0; i < cols.Length; i++)
            ws.Cell(1, i + 1).Value = cols[i];

        var header = ws.Range(1, 1, 1, cols.Length);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E5C9E");
        header.Style.Font.FontColor = XLColor.White;

        int fila = 2;
        foreach (var f in facturas.OrderBy(x => x.Fecha))
        {
            foreach (var c in f.Conceptos)
            {
                ws.Cell(fila, 1).Value = f.UUID;
                ws.Cell(fila, 2).Value = f.Fecha;
                ws.Cell(fila, 2).Style.DateFormat.Format = "dd/mm/yyyy";
                ws.Cell(fila, 3).Value = f.RfcEmisor;
                ws.Cell(fila, 4).Value = f.RfcReceptor;
                ws.Cell(fila, 5).Value = c.ClaveProdServ;
                ws.Cell(fila, 6).Value = c.ClaveUnidad;
                ws.Cell(fila, 7).Value = c.NoIdentificacion;
                ws.Cell(fila, 8).Value = c.Cantidad;
                ws.Cell(fila, 9).Value = c.Unidad;
                ws.Cell(fila, 10).Value = c.Descripcion;
                ws.Cell(fila, 11).Value = c.ValorUnitario;
                ws.Cell(fila, 12).Value = c.Importe;
                ws.Cell(fila, 13).Value = c.Descuento;

                for (int col = 11; col <= 13; col++)
                    ws.Cell(fila, col).Style.NumberFormat.Format = "#,##0.00";

                if (fila % 2 == 0)
                    ws.Row(fila).Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F4FA");
                fila++;
            }
        }

        ws.ColumnsUsed().AdjustToContents();
    }

    public static string GenerarNombreArchivo()
        => $"Facturas_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx";
}
