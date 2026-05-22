using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FacturasFacil.Api.Data;
using FacturasFacil.Api.DTOs;
using FacturasFacil.Api.Models;
using FacturasFacil.Api.Services;

namespace FacturasFacil.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/auth").WithTags("Auth");

        g.MapPost("/register", async (
            RegisterRequest req,
            UserManager<ApplicationUser> um,
            AppDbContext db,
            TokenService ts) =>
        {
            if (await um.FindByEmailAsync(req.Email) != null)
                return Results.BadRequest(new { error = "El email ya está registrado." });

            var user = new ApplicationUser
            {
                UserName = req.Email,
                Email = req.Email,
                NombreCompleto = req.NombreCompleto,
                Empresa = req.Empresa,
                PlanId = 1
            };

            var result = await um.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { errores = result.Errors.Select(e => e.Description) });

            // Recargar para incluir plan
            var userConPlan = await um.Users.Include(u => u.Plan).FirstAsync(u => u.Id == user.Id);
            var (token, expira) = ts.GenerarToken(userConPlan);
            return Results.Ok(new AuthResponse(token, userConPlan.Email!, userConPlan.NombreCompleto,
                userConPlan.Plan.Nombre, userConPlan.PlanId, userConPlan.Plan.LimiteFacturasMes,
                0, expira, userConPlan.SuscripcionVence));
        });

        g.MapPost("/login", async (
            LoginRequest req,
            UserManager<ApplicationUser> um,
            SignInManager<ApplicationUser> sm,
            AppDbContext db,
            TokenService ts,
            UsoService usoSvc) =>
        {
            var user = await um.Users
                .Include(u => u.Plan)
                .FirstOrDefaultAsync(u => u.Email == req.Email);

            if (user == null) return Results.Unauthorized();

            var result = await sm.CheckPasswordSignInAsync(user, req.Password, false);
            if (!result.Succeeded) return Results.Unauthorized();

            var uso = await usoSvc.ObtenerUsoActualAsync(user.Id);
            var (token, expira) = ts.GenerarToken(user);

            return Results.Ok(new AuthResponse(
                token, user.Email!, user.NombreCompleto,
                user.Plan.Nombre, user.PlanId, user.Plan.LimiteFacturasMes,
                uso.FacturasProcesadas, expira, user.SuscripcionVence));
        });

        g.MapGet("/me", async (
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> um,
            UsoService usoSvc) =>
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? principal.FindFirstValue("sub");
            if (userId == null) return Results.Unauthorized();

            var user = await um.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return Results.NotFound();

            var uso = await usoSvc.ObtenerUsoActualAsync(user.Id);

            return Results.Ok(new UsuarioInfoResponse(
                user.Email!, user.NombreCompleto, user.Empresa,
                user.Plan.Nombre, user.PlanId,
                user.Plan.LimiteFacturasMes, uso.FacturasProcesadas,
                user.FechaRegistro, user.SuscripcionVence));
        }).RequireAuthorization();

        return app;
    }
}
