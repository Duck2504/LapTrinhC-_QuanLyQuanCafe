using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Application.Inventory;

public record SaveStockMovementRequest(
    Guid MaterialId, decimal Quantity, string? Note);

public record StockMovementResult(
    Guid MovementId, Guid UserId, decimal StockQuantity);

public record StockMovementDto(
    Guid MovementId, Guid MaterialId, string MaterialName,
    MovementType MovementType, decimal Quantity, DateTime MovementDate,
    string UserName, Guid? OrderId, string? Note);

public record PagedResult<T>(
    List<T> Items, int Page, int PageSize, int TotalCount);

public record StocktakeRequest(
    Guid MaterialId, decimal? ActualQuantity, string? Note);

public record StocktakeResultDto(
    Guid MaterialId, decimal SystemQuantity, decimal ActualQuantity,
    decimal Difference);