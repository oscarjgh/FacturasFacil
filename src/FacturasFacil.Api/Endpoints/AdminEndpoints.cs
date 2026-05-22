using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FacturasFacil.Api.Data;
using FacturasFacil.Api.DTOs;
using FacturasFacil.Api.Models;

namespace FacturasFacil.Api.Endpoints;

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/admin").WithTags("Admin").RequireAuthorization();

        // Middleware: verificar que el usuario sea admin
        g.AddEndpointFilter(async (ctx, next) =>
        {
            var principal = ctx.HttpContext.User;
            var adminClaim = principal.FindFirstValue("isAdmin");
            if (adminClaim != "true")
                return Results.Forbid();
            return await next(ctx);
        });

        // Listar todos los usuarios
        g.MapGet("/usuarios", async (
            UserManager<ApplicationUser> um,
            AppDbContext db) =>
        {
            var usuarios = await um.Users
                .Include(u => u.Plan)
                .OrderByDescending(u => u.FechaRegistro)
                .Select(u => new AdminUsuarioResponse(
                    u.Id, u.Email!, u.NombreCompleto, u.Empresa,
                    u.Plan.Nombre, u.PlanId, u.FechaRegistro,
                    u.SuscripcionVence, u.IsAdmin))
                .ToListAsync();

            return Results.Ok(usuarios);
        });

        // Estadísticas generales
        g.MapGet("/stats", async (
            UserManager<ApplicationUser> um,
            AppDbContext db) =>
        {
            var totalUsuarios  = await um.Users.CountAsync();
            var usuariosPago   = await um.Users.CountAsync(u => u.PlanId > 1);
            var totalExcels    = await db.HistorialExcels.CountAsync();
            var totalFacturas  = await db.UsosMensuales.SumAsync(u => (long)u.FacturasProcesadas);
            var porPlan = await um.Users
                .Include(u => u.Plan)
                .GroupBy(u => u.Plan.Nombre)
                .Select(g => new { Plan = g.Key, Total = g.Count() })
                .ToListAsync();

            return Results.Ok(new
            {
                totalUsuarios,
                usuariosPago,
                totalExcels,
                totalFacturas,
                porPlan
            });
        });

        // Cambiar plan de un usuario
        g.MapPatch("/usuarios/{id}/plan", async (
            string id,
            CambiarPlanRequest req,
            UserManager<ApplicationUser> um,
            AppDbContext db) =>
        {
            var user = await um.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == id);
            if (user is null) return Results.NotFound();

            var plan = await db.Planes.FindAsync(req.PlanId);
            if (plan is null) return Results.BadRequest(new { error = "Plan no existe." });

            var planAnterior = user.PlanId;
            user.PlanId = req.PlanId;

            // Si baja a Gratis, quitar suscripción
            if (req.PlanId == 1)
            {
                user.StripeSubscriptionId = null;
                user.SuscripcionVence = null;
            }
            // Si sube a plan de pago manualmente, poner vencimiento a 1 mes
            else if (planAnterior == 1)
            {
                user.SuscripcionVence = DateTime.UtcNow.AddMonths(1);

                // Reiniciar contador del mes
                var ahora = DateTime.UtcNow;
                var uso = await db.UsosMensuales.FirstOrDefaultAsync(u =>
                    u.UserId == id && u.Anno == ahora.Year && u.Mes == ahora.Month);
                if (uso != null) { uso.FacturasProcesadas = 0; await db.SaveChangesAsync(); }
            }

            await um.UpdateAsync(user);
            return Results.Ok(new { mensaje = $"Plan cambiado a {plan.Nombre}." });
        });

        // Alternar admin de un usuario
        g.MapPatch("/usuarios/{id}/admin", async (
            string id,
            UserManager<ApplicationUser> um) =>
        {
            var user = await um.FindByIdAsync(id);
            if (user is null) return Results.NotFound();
            user.IsAdmin = !user.IsAdmin;
            await um.UpdateAsync(user);
            return Results.Ok(new { isAdmin = user.IsAdmin });
        });

        return app;
    }
}
