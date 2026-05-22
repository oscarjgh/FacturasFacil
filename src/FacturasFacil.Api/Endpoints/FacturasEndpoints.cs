using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using FacturasFacil.Api.Data;
using FacturasFacil.Api.DTOs;
using FacturasFacil.Api.Models;
using FacturasFacil.Api.Services;
using FacturasFacil.Core.Services;

namespace FacturasFacil.Api.Endpoints;

public static class FacturasEndpoints
{
    public static IEndpointRouteBuilder MapFacturasEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/facturas").WithTags("Facturas");

        // Procesar archivos ZIP/RAR y generar Excel
        g.MapPost("/procesar", async (
            IFormFileCollection files,
            ClaimsPrincipal principal,
            UserManager<ApplicationUser> um,
            UsoService usoSvc,
            AppDbContext db,
            IConfiguration config) =>
        {
            if (files.Count == 0)
                return Results.BadRequest(new { error = "Debe enviar al menos un archivo ZIP o RAR." });

            var userId = principal.FindFirstValue("sub")
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var user = await um.Users.Include(u => u.Plan).FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null) return Results.Unauthorized();

            // Leer archivos en memoria para poder contar facturas primero
            var archivos = new List<(string Nombre, Stream Contenido)>();
            foreach (var file in files)
            {
                var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (ext != ".zip" && ext != ".rar")
                    return Results.BadRequest(new { error = $"'{file.FileName}' no es ZIP ni RAR." });
                var ms = new MemoryStream();
                await file.CopyToAsync(ms);
                ms.Position = 0;
                archivos.Add((file.FileName, ms));
            }

            // Extraer y contar XMLs antes de verificar el límite
            var (facturas, errores) = FacturaProcessor.ProcesarStreams(archivos);
            foreach (var (_, s) in archivos) await s.DisposeAsync();

            if (facturas.Count == 0)
            {
                var msg = errores.Count > 0
                    ? string.Join("; ", errores.Select(e => e.Mensaje))
                    : "No se encontraron XMLs CFDI en los archivos.";
                return Results.BadRequest(new { error = msg, errores });
            }

            // Verificar límite del plan
            if (!await usoSvc.VerificarLimiteAsync(userId, facturas.Count))
            {
                var uso = await usoSvc.ObtenerUsoActualAsync(userId);
                var limite = user.Plan.LimiteFacturasMes;
                return Results.Json(new
                {
                    error = $"Límite del plan alcanzado. Has procesado {uso.FacturasProcesadas}/{limite} facturas este mes.",
                    limiteSuperado = true,
                    facturasDisponibles = Math.Max(0, limite - uso.FacturasProcesadas)
                }, statusCode: 402); // 402 Payment Required
            }

            // Generar Excel
            var excelBytes = ExcelGenerator.Generar(facturas);
            var nombreArchivo = ExcelGenerator.GenerarNombreArchivo();

            // Guardar en historial
            var carpetaUsuario = Path.Combine(
                config["Historial:CarpetaBase"]!, userId);
            Directory.CreateDirectory(carpetaUsuario);
            var rutaArchivo = Path.Combine(carpetaUsuario, nombreArchivo);
            await File.WriteAllBytesAsync(rutaArchivo, excelBytes);

            db.HistorialExcels.Add(new HistorialExcel
            {
                UserId = userId,
                NombreArchivo = nombreArchivo,
                RutaArchivo = rutaArchivo,
                TotalFacturas = facturas.Count,
                TotalArchivosSubidos = files.Count,
                TamanioBytes = excelBytes.Length
            });
            await db.SaveChangesAsync();

            // Incrementar uso
            await usoSvc.IncrementarUsoAsync(userId, facturas.Count);

            return Results.File(
                excelBytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                nombreArchivo);
        })
        .RequireAuthorization()
        .DisableAntiforgery();

        // Historial de Excels del usuario
        g.MapGet("/historial", async (
            ClaimsPrincipal principal,
            AppDbContext db,
            int pagina = 1,
            int porPagina = 20) =>
        {
            var userId = principal.FindFirstValue("sub")
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var historial = await db.HistorialExcels
                .Where(h => h.UserId == userId)
                .OrderByDescending(h => h.FechaGeneracion)
                .Skip((pagina - 1) * porPagina)
                .Take(porPagina)
                .Select(h => new HistorialItemResponse(
                    h.Id, h.FechaGeneracion, h.NombreArchivo,
                    h.TotalFacturas, h.TotalArchivosSubidos, h.TamanioBytes))
                .ToListAsync();

            return Results.Ok(historial);
        }).RequireAuthorization();

        // Descargar un Excel del historial
        g.MapGet("/historial/{id:int}/descargar", async (
            int id,
            ClaimsPrincipal principal,
            AppDbContext db) =>
        {
            var userId = principal.FindFirstValue("sub")
                        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId is null) return Results.Unauthorized();
            var item = await db.HistorialExcels
                .FirstOrDefaultAsync(h => h.Id == id && h.UserId == userId);

            if (item == null) return Results.NotFound();
            if (!File.Exists(item.RutaArchivo))
                return Results.NotFound(new { error = "El archivo ya no existe en el servidor." });

            return Results.File(
                item.RutaArchivo,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                item.NombreArchivo);
        }).RequireAuthorization();

        // Validación SAT
        g.MapPost("/sat/validar", async (
            SatValidarRequest req,
            ClaimsPrincipal principal,
            SatValidacionService satSvc) =>
        {
            var resultado = await satSvc.ValidarAsync(
                req.Uuid, req.RfcEmisor, req.RfcReceptor, req.Total);

            return Results.Ok(new SatValidarResponse(
                resultado.Uuid, resultado.Estado, resultado.CodigoEstatus,
                resultado.EsCancelable, resultado.EstatusCancelacion));
        }).RequireAuthorization();

        return app;
    }
}
