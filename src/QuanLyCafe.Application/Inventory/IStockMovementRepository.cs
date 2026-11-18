using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Application.Inventory;

public interface IStockMovementRepository
{
    Task SaveMovementAsync(StockMovement movement, CancellationToken ct);

    Task<PagedResult<StockMovementDto>> GetMovementsAsync(
        int page, int pageSize, CancellationToken ct);
}