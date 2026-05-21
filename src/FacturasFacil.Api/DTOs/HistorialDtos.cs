namespace FacturasFacil.Api.DTOs;

public record HistorialItemResponse(
    int Id,
    DateTime FechaGeneracion,
    string NombreArchivo,
    int TotalFacturas,
    int TotalArchivosSubidos,
    long TamanioBytes);

public record SatValidarRequest(
    string Uuid,
    string RfcEmisor,
    string RfcReceptor,
    string Total);

public record SatValidarResponse(
    string Uuid,
    string Estado,
    string CodigoEstatus,
    string? EsCancelable,
    string? EstatusCancelacion);
