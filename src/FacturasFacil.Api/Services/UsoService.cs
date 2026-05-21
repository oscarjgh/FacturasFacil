using Microsoft.EntityFrameworkCore;
using FacturasFacil.Api.Data;
using FacturasFacil.Api.Models;

namespace FacturasFacil.Api.Services;

public class UsoService(AppDbContext db)
{
    public async Task<UsoMensual> ObtenerUsoActualAsync(string userId)
    {
        var hoy = DateTime.UtcNow;
        var uso = await db.UsosMensuales
            .FirstOrDefaultAsync(u => u.UserId == userId
                && u.Mes == hoy.Month && u.Anno == hoy.Year);

        if (uso == null)
        {
            uso = new UsoMensual { UserId = userId, Mes = hoy.Month, Anno = hoy.Year };
            db.UsosMensuales.Add(uso);
            await db.SaveChangesAsync();
        }
        return uso;
    }

    public async Task<bool> VerificarLimiteAsync(string userId, int facturasAagregar)
    {
        var user = await db.Users
            .Include(u => u.Plan)
            .FirstAsync(u => u.Id == userId);

        if (user.Plan.LimiteFacturasMes == -1) return true; // ilimitado

        var uso = await ObtenerUsoActualAsync(userId);
        return (uso.FacturasProcesadas + facturasAagregar) <= user.Plan.LimiteFacturasMes;
    }

    public async Task IncrementarUsoAsync(string userId, int cantidad)
    {
        var uso = await ObtenerUsoActualAsync(userId);
        uso.FacturasProcesadas += cantidad;
        uso.UltimaActualizacion = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }
}
