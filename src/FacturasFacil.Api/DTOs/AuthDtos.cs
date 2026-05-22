namespace FacturasFacil.Api.DTOs;

public record RegisterRequest(
    string Email,
    string Password,
    string NombreCompleto,
    string? Empresa);

public record LoginRequest(
    string Email,
    string Password);

public record AuthResponse(
    string Token,
    string Email,
    string NombreCompleto,
    string Plan,
    int PlanId,
    int LimiteFacturasMes,
    int FacturasUsadasEsteMes,
    DateTime Expira,
    DateTime? SuscripcionVence);

public record UsuarioInfoResponse(
    string Email,
    string NombreCompleto,
    string? Empresa,
    string Plan,
    int PlanId,
    int LimiteFacturasMes,
    int FacturasUsadasEsteMes,
    DateTime FechaRegistro,
    DateTime? SuscripcionVence);
