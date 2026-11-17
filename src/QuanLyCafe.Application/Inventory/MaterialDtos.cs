namespace QuanLyCafe.Application.Inventory;

public record MaterialDto(
    Guid MaterialId,
    string MaterialName,
    string Unit,
    decimal StockQuantity,
    decimal MinQuantity,
    bool IsLow);

public record CreateMaterialRequest(
    string MaterialName,
    string Unit,
    decimal MinQuantity);