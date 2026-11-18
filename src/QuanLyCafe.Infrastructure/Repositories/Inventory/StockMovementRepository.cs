using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Infrastructure.Persistence;

namespace QuanLyCafe.Infrastructure.Repositories.Inventory;

public class StockMovementRepository(AppDbContext db) : IStockMovementRepository
{
    public async Task SaveMovementAsync(
        StockMovement movement, CancellationToken ct)
    {
        db.StockMovements.Add(movement);
        await db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<StockMovementDto>> GetMovementsAsync(
        int page, int pageSize, CancellationToken ct)
    {
        var query = db.StockMovements.AsNoTracking();
        var totalCount = await query.CountAsync(ct);
        var offset = (long)(page - 1) * pageSize;

        if (offset >= totalCount)
            return new([], page, pageSize, totalCount);

        var items = await query
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.MovementID)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(m => new StockMovementDto(
                m.MovementID, m.MaterialID, m.Material.MaterialName,
                m.MovementType, m.Quantity, m.MovementDate,
                m.User.FullName, m.OrderID, m.Note))
            .ToListAsync(ct);

        return new(items, page, pageSize, totalCount);
    }
}