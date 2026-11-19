using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Application.Payment;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Infrastructure.Persistence;

namespace QuanLyCafe.Infrastructure.Repositories.Payment;

public class PaymentRepository(AppDbContext db, IOrderRepository orders)
    : IPaymentRepository
{
    public Task<PaymentPreview?> GetPaymentPreviewAsync(Guid orderId, CancellationToken ct) =>
        db.Orders.AsNoTracking()
            .Where(order => order.OrderID == orderId)
            .Select(order => new PaymentPreview(
                order.Status, order.TotalAmount, order.OrderDetails.Any()))
            .SingleOrDefaultAsync(ct);

    public async Task<T> RunAsync<T>(
        Guid orderId, Func<Task<T>> work, CancellationToken ct)
    {
        try
        {
            return await orders.InOrderLockAsync(orderId, work, ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
        }

        return await orders.InOrderLockAsync(orderId, work, ct);
    }

    public Task<Dictionary<Guid, Material>> GetMaterialsAsync(
        IEnumerable<Guid> ids, CancellationToken ct)
    {
        var materialIds = ids.ToArray();
        return db.Materials
            .Where(m => materialIds.Contains(m.MaterialID))
            .ToDictionaryAsync(m => m.MaterialID, ct);
    }

    public Task SaveAsync(IEnumerable<StockMovement> movements, CancellationToken ct)
    {
        db.StockMovements.AddRange(movements);
        return db.SaveChangesAsync(ct);
    }
}
