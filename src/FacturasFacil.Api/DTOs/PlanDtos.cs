namespace FacturasFacil.Api.DTOs;

public record PlanResponse(
    int Id,
    string Nombre,
    string Descripcion,
    int LimiteFacturasMes,
    decimal PrecioMensual,
    string[] Caracteristicas);

public record UsoActualResponse(
    int FacturasUsadas,
    int LimiteFacturasMes,
    bool EsIlimitado,
    int PorcentajeUso,
    int Mes,
    int Anno);

public record CheckoutRequest(int PlanId);
public record CheckoutResponse(string Url);
