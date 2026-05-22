using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using FacturasFacil.Api.Models;

namespace FacturasFacil.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Plan> Planes => Set<Plan>();
    public DbSet<UsoMensual> UsosMensuales => Set<UsoMensual>();
    public DbSet<HistorialExcel> HistorialExcels => Set<HistorialExcel>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<Plan>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Nombre).HasMaxLength(50).IsRequired();
            // SQLite no tiene decimal nativo; se guarda como TEXT numérico
            e.Property(p => p.PrecioMensual).HasColumnType("TEXT");
            e.Property(p => p.Caracteristicas).HasConversion(
                v => string.Join("|", v),
                v => v.Split("|", StringSplitOptions.RemoveEmptyEntries));
        });

        builder.Entity<ApplicationUser>(e =>
        {
            e.HasOne(u => u.Plan).WithMany()
             .HasForeignKey(u => u.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<UsoMensual>(e =>
        {
            e.HasIndex(u => new { u.UserId, u.Anno, u.Mes }).IsUnique();
            e.HasOne(u => u.User).WithMany(u => u.UsosMensuales)
             .HasForeignKey(u => u.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<HistorialExcel>(e =>
        {
            e.HasOne(h => h.User).WithMany(u => u.HistorialExcels)
             .HasForeignKey(h => h.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // Seed planes
        builder.Entity<Plan>().HasData(
            new Plan
            {
                Id = 1, Nombre = "Gratis", Descripcion = "Ideal para probar la plataforma",
                LimiteFacturasMes = 30, PrecioMensual = 0,
                Caracteristicas = ["30 facturas/mes", "Descarga en Excel", "Soporte por email"]
            },
            new Plan
            {
                Id = 2, Nombre = "Contador", Descripcion = "Para contadores independientes",
                LimiteFacturasMes = 2000, PrecioMensual = 199,
                StripePriceId = "price_1TZhohEIUe6HZv681DUf6IjX",
                Caracteristicas = ["2,000 facturas/mes", "Historial 6 meses", "Validación SAT", "Soporte prioritario"]
            },
            new Plan
            {
                Id = 3, Nombre = "Despacho", Descripcion = "Para despachos contables",
                LimiteFacturasMes = -1, PrecioMensual = 499,
                StripePriceId = "price_1TZhqnEIUe6HZv68vqfRf9g9",
                Caracteristicas = ["Facturas ilimitadas", "Historial completo", "Validación SAT", "Soporte 24/7", "Múltiples usuarios (próximamente)"]
            }
        );
    }
}
