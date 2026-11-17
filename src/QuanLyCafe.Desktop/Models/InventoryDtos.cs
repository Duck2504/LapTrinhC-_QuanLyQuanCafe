namespace QuanLyCafe.Desktop.Models;

// GET /api/materials
public record MaterialDto(
    Guid MaterialId, string MaterialName, string Unit,
    decimal StockQuantity, decimal MinQuantity)
{
    // Dùng để tô nổi dòng dưới mức tối thiểu (US-INV-04)
    public bool IsLow => StockQuantity <= MinQuantity;
}

// POST /api/materials
public record CreateMaterialRequest(string MaterialName, string Unit, decimal MinQuantity);

// POST /api/stock/receipts, /issues
public record StockReceiptRequest(Guid MaterialId, decimal Quantity, string? Note);
public record StockIssueRequest(Guid MaterialId, decimal Quantity, string? Note);

// POST /api/stock/stocktakes
public record StocktakeRequest(Guid MaterialId, decimal ActualQuantity, string? Note);
public record StocktakeResultDto(Guid MaterialId, decimal SystemQuantity, decimal ActualQuantity, decimal Difference);

// GET /api/stock/movements?page=&pageSize=
public record StockMovementDto(
    Guid MovementId, Guid MaterialId, string MaterialName,
    MovementType MovementType, decimal Quantity, DateTime MovementDate,
    string UserName, Guid? OrderId, string? Note);

public record PagedResult<T>(List<T> Items, int Page, int PageSize, int TotalCount);