using SendGrid;
using SendGrid.Helpers.Mail;

namespace FacturasFacil.Api.Services;

public class EmailService(IConfiguration config, ILogger<EmailService> logger)
{
    public async Task<bool> EnviarRecuperacionPasswordAsync(string email, string nombre, string resetUrl)
    {
        var apiKey = config["SendGrid:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("TU_API_KEY"))
        {
            logger.LogWarning("SendGrid no configurado. URL de reset: {Url}", resetUrl);
            return false;
        }

        var client = new SendGridClient(apiKey);
        var from   = new EmailAddress(config["SendGrid:FromEmail"] ?? "noreply@facturasfacil.com",
                                      config["SendGrid:FromName"]  ?? "FacturasFacil");
        var to     = new EmailAddress(email, nombre);

        var msg = MailHelper.CreateSingleEmail(from, to,
            subject: "Recupera tu contraseña — FacturasFacil",
            plainTextContent: $"Hola {nombre},\n\nHaz clic en el siguiente enlace para restablecer tu contraseña:\n{resetUrl}\n\nEste enlace expira en 2 horas.\n\nSi no solicitaste esto, ignora este mensaje.",
            htmlContent: $"""
                <div style="font-family:system-ui,sans-serif;max-width:480px;margin:auto;padding:2rem">
                  <h2 style="color:#1E5C9E">⚡ FacturasFacil</h2>
                  <h3>Recupera tu contraseña</h3>
                  <p>Hola <strong>{nombre}</strong>,</p>
                  <p>Recibimos una solicitud para restablecer la contraseña de tu cuenta.</p>
                  <div style="text-align:center;margin:2rem 0">
                    <a href="{resetUrl}"
                       style="background:#1E5C9E;color:#fff;padding:.85rem 2rem;border-radius:8px;text-decoration:none;font-weight:700;display:inline-block">
                      Restablecer contraseña
                    </a>
                  </div>
                  <p style="color:#64748b;font-size:.875rem">Este enlace expira en <strong>2 horas</strong>.<br/>
                  Si no solicitaste esto, ignora este mensaje.</p>
                  <hr style="border:none;border-top:1px solid #e2e8f0;margin:2rem 0"/>
                  <p style="color:#94a3b8;font-size:.75rem">FacturasFacil — Procesador CFDI</p>
                </div>
                """);

        try
        {
            var response = await client.SendEmailAsync(msg);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error enviando email a {Email}", email);
            return false;
        }
    }
}
