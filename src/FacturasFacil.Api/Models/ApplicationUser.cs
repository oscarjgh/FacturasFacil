using Microsoft.AspNetCore.Identity;

namespace FacturasFacil.Api.Models;

public class ApplicationUser : IdentityUser
{
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Empresa { get; set; }
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;

    public int PlanId { get; set; } = 1; // Gratis por defecto
    public Plan Plan { get; set; } = null!;

    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTime? SuscripcionVence { get; set; }

    public ICollection<UsoMensual> UsosMensuales { get; set; } = [];
    public ICollection<HistorialExcel> HistorialExcels { get; set; } = [];
}
