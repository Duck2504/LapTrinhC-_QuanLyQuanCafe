using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Application.Reporting;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Infrastructure.Persistence;

namespace QuanLyCafe.Infrastructure.Repositories.Reporting;

public sealed class ReportRepository(AppDbContext db) : IReportRepository
{
    public Task<List<PaidOrderTotal>> GetPaidOrderTotalsAsync(
        DateTime? start, DateTime? endExclusive, CancellationToken ct) =>
        PaidOrders(start, endExclusive)
            .Where(order => order.PaidAt.HasValue)
            .Select(order => new PaidOrderTotal(order.PaidAt!.Value, order.TotalAmount))
            .ToListAsync(ct);

    public Task<List<TopProductDto>> GetTopProductsAsync(
        DateTime? start, DateTime? endExclusive, CancellationToken ct) =>
        PaidOrders(start, endExclusive)
            .SelectMany(order => order.OrderDetails)
            .GroupBy(line => new { line.ProductID, line.Product.ProductName })
            .Select(group => new
            {
                group.Key.ProductID,
                group.Key.ProductName,
                Quantity = group.Sum(line => line.Quantity),
                Revenue = group.Sum(line => line.Quantity * line.UnitPrice)
            })
            .OrderByDescending(product => product.Quantity)
            .ThenByDescending(product => product.Revenue)
            .ThenBy(product => product.ProductName)
            .ThenBy(product => product.ProductID)
            .Select(product => new TopProductDto(product.ProductName, product.Quantity, product.Revenue))
            .ToListAsync(ct);

    public Task<List<InventoryReportDto>> GetInventoryAsync(
        DateTime? start, DateTime? endExclusive, CancellationToken ct)
    {
        var shrinkage = db.StockMovements
            .Where(movement => movement.MovementType == MovementType.Shrinkage);

        if (start is { } first)
            shrinkage = shrinkage.Where(movement => movement.MovementDate >= first);
        if (endExclusive is { } end)
            shrinkage = shrinkage.Where(movement => movement.MovementDate < end);

        return db.Materials
            .OrderBy(material => material.MaterialName)
            .ThenBy(material => material.MaterialID)
            .Select(material => new InventoryReportDto(
                material.MaterialID, material.MaterialName, material.Unit,
                material.StockQuantity, material.MinQuantity,
                shrinkage.Where(movement => movement.MaterialID == material.MaterialID)
                    .Sum(movement => movement.Quantity)))
            .ToListAsync(ct);
    }

    private IQueryable<Order> PaidOrders(DateTime? start, DateTime? endExclusive)
    {
        var orders = db.Orders.Where(order => order.Status == OrderStatus.Paid);
        if (start is { } first)
            orders = orders.Where(order => order.PaidAt >= first);
        if (endExclusive is { } end)
            orders = orders.Where(order => order.PaidAt < end);

        return orders;
    }
}
