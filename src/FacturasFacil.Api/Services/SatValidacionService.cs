using System.Text;
using System.Xml.Linq;

namespace FacturasFacil.Api.Services;

public class SatValidacionService(HttpClient httpClient)
{
    private const string SatUrl =
        "https://consultaqr.facturaelectronica.sat.gob.mx/ConsultaCFDIService.svc";

    public async Task<SatResultado> ValidarAsync(
        string uuid, string rfcEmisor, string rfcReceptor, string total)
    {
        // Formateamos el total: el SAT requiere exactamente 6 decimales
        if (decimal.TryParse(total,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var t))
            total = t.ToString("F6", System.Globalization.CultureInfo.InvariantCulture);

        var expresion =
            $"?re={Uri.EscapeDataString(rfcEmisor)}" +
            $"&rr={Uri.EscapeDataString(rfcReceptor)}" +
            $"&tt={Uri.EscapeDataString(total)}" +
            $"&id={Uri.EscapeDataString(uuid)}";

        var soap = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <soapenv:Envelope
              xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
              xmlns:tem="http://tempuri.org/">
              <soapenv:Header/>
              <soapenv:Body>
                <tem:Consulta>
                  <tem:expresionImpresa><![CDATA[{expresion}]]></tem:expresionImpresa>
                </tem:Consulta>
              </soapenv:Body>
            </soapenv:Envelope>
            """;

        try
        {
            var request = new HttpRequestMessage(HttpMethod.Post, SatUrl)
            {
                Content = new StringContent(soap, Encoding.UTF8, "text/xml")
            };
            request.Headers.Add("SOAPAction", "http://tempuri.org/IConsultaCFDIService/Consulta");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var response = await httpClient.SendAsync(request, cts.Token);
            var xml = await response.Content.ReadAsStringAsync();

            return ProcesarRespuesta(uuid, xml);
        }
        catch (TaskCanceledException)
        {
            return new SatResultado(uuid, "Error", "Tiempo de espera agotado al consultar el SAT", null, null);
        }
        catch (Exception ex)
        {
            return new SatResultado(uuid, "Error", $"Error de conexión: {ex.Message}", null, null);
        }
    }

    private static SatResultado ProcesarRespuesta(string uuid, string xml)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            XNamespace tem = "http://tempuri.org/";

            var resultado = doc.Descendants(tem + "ConsultaResult").FirstOrDefault();
            if (resultado == null)
                return new SatResultado(uuid, "Error", "Respuesta inesperada del SAT", null, null);

            var estado = resultado.Element(tem + "Estado")?.Value ?? "No Encontrado";
            var codigo = resultado.Element(tem + "CodigoEstatus")?.Value ?? string.Empty;
            var esCancelable = resultado.Element(tem + "EsCancelable")?.Value;
            var estatusCancelacion = resultado.Element(tem + "EstatusCancelacion")?.Value;

            return new SatResultado(uuid, estado, codigo, esCancelable, estatusCancelacion);
        }
        catch
        {
            return new SatResultado(uuid, "Error", "No se pudo interpretar la respuesta del SAT", null, null);
        }
    }
}

public record SatResultado(
    string Uuid,
    string Estado,
    string CodigoEstatus,
    string? EsCancelable,
    string? EstatusCancelacion);
