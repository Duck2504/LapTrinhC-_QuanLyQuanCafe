namespace QuanLyCafe.Application.Catalog;

public record BomLineDto(
    Guid MaterialId,
    string MaterialName,
    string Unit,
    decimal QuantityRequired);

public record BomLineRequest(
    Guid MaterialId,
    decimal QuantityRequired);

public record SetBomRequest(
    List<BomLineRequest> Lines);