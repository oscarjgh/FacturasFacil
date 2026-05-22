using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Stripe.Checkout;
using FacturasFacil.Api.Data;
using FacturasFacil.Api.DTOs;
using FacturasFacil.Api.Models;
using FacturasFacil.Api.Services;

namespace FacturasFacil.Api.Endpoints;

public static class PlanesEndpoints
{
    public static IEndpointRouteBuilder MapPlanesEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api").WithTags("Planes y Pagos");

        // Listar planes
        g.MapGet("/planes", async (AppDbContext db) =>
        {
            var planes = await db.Planes.Where(p => p.Activo).OrderBy(p => p.Id).ToListAsync();
            return Results.Ok(planes.Select(p => new PlanResponse(
                p.Id, p.Nombre, p.Descripcion, p.LimiteFacturasMes,
                p.PrecioMensual, p.Caracteristicas)));
        });

        // Uso actual del usuario
        g.MapGet("/uso/actual", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> um,
            UsoService usoSvc) =>
        {
            var userId = principal.FindFirstValue("sub")
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var user = await um.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Results.Unauthorized();
            var uso = await usoSvc.ObtenerUsoActualAsync(userId);
            var limite = user.Plan.LimiteFacturasMes;
            var esIlimitado = limite == -1;
            var pct = esIlimitado ? 0 : (int)Math.Round((double)uso.FacturasProcesadas / limite * 100);

            return Results.Ok(new UsoActualResponse(
                uso.FacturasProcesadas, limite, esIlimitado,
                pct, uso.Mes, uso.Anno));
        }).RequireAuthorization();

        // Crear sesión de pago Stripe
        g.MapPost("/pagos/checkout", async (
            CheckoutRequest req,
            HttpRequest httpReq,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> um,
            AppDbContext db,
            IConfiguration config) =>
        {
            // Verificar que Stripe esté configurado
            var stripeKey = config["Stripe:SecretKey"] ?? "";
            if (string.IsNullOrWhiteSpace(stripeKey) ||
                stripeKey.Contains("TU_CLAVE") || stripeKey == "sk_test_")
                return Results.BadRequest(new
                {
                    error = "Los pagos aún no están configurados en este servidor. " +
                            "Contacta al administrador para activar Stripe."
                });

            var userId = principal.FindFirstValue("sub")
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var user   = await um.FindByIdAsync(userId);
            var plan   = await db.Planes.FindAsync(req.PlanId);

            if (plan == null || string.IsNullOrEmpty(plan.StripePriceId) ||
                plan.StripePriceId.Contains("_ID"))
                return Results.BadRequest(new
                {
                    error = "Este plan aún no tiene precio configurado en Stripe."
                });

            try
            {
                // Crear o recuperar customer en Stripe
                if (string.IsNullOrEmpty(user!.StripeCustomerId))
                {
                    var custSvc  = new Stripe.CustomerService();
                    var customer = await custSvc.CreateAsync(new Stripe.CustomerCreateOptions
                    {
                        Email    = user.Email,
                        Name     = user.NombreCompleto,
                        Metadata = new Dictionary<string, string> { ["userId"] = userId }
                    });
                    user.StripeCustomerId = customer.Id;
                    await um.UpdateAsync(user);
                }

                // Base URL: variable de entorno o se detecta del request
                var baseUrl = config["Stripe:SuccessUrl"]?.TrimEnd('/')
                              ?? $"{httpReq.Scheme}://{httpReq.Host}";

                var options = new SessionCreateOptions
                {
                    Customer  = user.StripeCustomerId,
                    Mode      = "subscription",
                    LineItems = [new SessionLineItemOptions { Price = plan.StripePriceId, Quantity = 1 }],
                    SuccessUrl = baseUrl + "?session_id={CHECKOUT_SESSION_ID}",
                    CancelUrl  = config["Stripe:CancelUrl"]?.TrimEnd('/') ?? baseUrl,
                    Metadata   = new Dictionary<string, string>
                    {
                        ["userId"] = userId,
                        ["planId"] = plan.Id.ToString()
                    }
                };

                var session = await new SessionService().CreateAsync(options);
                return Results.Ok(new CheckoutResponse(session.Url!));
            }
            catch (Stripe.StripeException ex)
            {
                return Results.BadRequest(new { error = $"Error de Stripe: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return Results.Problem($"Error inesperado: {ex.Message}", statusCode: 500);
            }
        }).RequireAuthorization();

        // Webhook de Stripe
        g.MapPost("/pagos/webhook", async (
            HttpRequest request,
            UserManager<ApplicationUser> um,
            AppDbContext db,
            IConfiguration config) =>
        {
            var payload = await new StreamReader(request.Body).ReadToEndAsync();
            var sig = request.Headers["Stripe-Signature"].ToString();

            try
            {
                var evt = Stripe.EventUtility.ConstructEvent(
                    payload, sig, config["Stripe:WebhookSecret"]);

                if (evt.Type == "checkout.session.completed")
                {
                    var session = (Stripe.Checkout.Session)evt.Data.Object;
                    var userId = session.Metadata["userId"];
                    var planId = int.Parse(session.Metadata["planId"]);

                    var user = await um.FindByIdAsync(userId);
                    if (user != null)
                    {
                        user.PlanId = planId;
                        user.StripeSubscriptionId = session.SubscriptionId;
                        user.SuscripcionVence = DateTime.UtcNow.AddMonths(1);
                        await um.UpdateAsync(user);

                        // Reiniciar contador del mes actual al activar plan de pago
                        var ahora = DateTime.UtcNow;
                        var uso = await db.UsosMensuales.FirstOrDefaultAsync(u =>
                            u.UserId == userId && u.Anno == ahora.Year && u.Mes == ahora.Month);
                        if (uso != null)
                        {
                            uso.FacturasProcesadas = 0;
                            await db.SaveChangesAsync();
                        }
                    }
                }
                else if (evt.Type == "customer.subscription.deleted")
                {
                    var sub = (Stripe.Subscription)evt.Data.Object;
                    var customerId = sub.CustomerId;
                    var user = await um.Users.FirstOrDefaultAsync(u => u.StripeCustomerId == customerId);
                    if (user != null)
                    {
                        user.PlanId = 1; // Regresa a Gratis
                        user.StripeSubscriptionId = null;
                        user.SuscripcionVence = null;
                        await um.UpdateAsync(user);
                    }
                }

                return Results.Ok();
            }
            catch (Stripe.StripeException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        return app;
    }
}
