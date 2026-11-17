using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Infrastructure.Persistence;

namespace QuanLyCafe.Infrastructure.Repositories.Ordering;

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<List<Table>> GetTablesAsync(CancellationToken ct = default) =>
        db.Tables.OrderBy(t => t.TableName).ToListAsync(ct);

    public Task<Table?> GetTableAsync(Guid tableId, CancellationToken ct = default) =>
        db.Tables.FirstOrDefaultAsync(t => t.TableID == tableId, ct);

    public Task<Dictionary<Guid, Guid>> GetOpenOrderIdsByTableAsync(CancellationToken ct = default) =>
        db.Orders.Where(o => o.Status == OrderStatus.Open && o.TableID != null)
            .ToDictionaryAsync(o => o.TableID!.Value, o => o.OrderID, ct);

    public Task<bool> HasOpenOrderAsync(Guid tableId, CancellationToken ct = default) =>
        db.Orders.AnyAsync(o => o.TableID == tableId && o.Status == OrderStatus.Open, ct);

    public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default) =>
        db.Orders.Include(o => o.Table)
            .Include(o => o.OrderDetails).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(o => o.OrderID == orderId, ct);

    public Task<List<Product>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default)
    {
        var ids = productIds.ToList();
        return db.Products.Where(p => ids.Contains(p.ProductID)).ToListAsync(ct);
    }

    public void Add(Order order) => db.Orders.Add(order);

    // Add tường minh: OrderDetailID đã có giá trị nên nếu chỉ thêm vào collection EF sẽ hiểu nhầm là UPDATE.
    public void AddDetails(IEnumerable<OrderDetail> details) => db.OrderDetails.AddRange(details);

    public void RemoveDetails(IEnumerable<OrderDetail> details) => db.OrderDetails.RemoveRange(details);

    public async Task<T> InOrderLockAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Request thứ hai trên cùng order chờ ở đây tới khi request đầu commit/rollback.
        await db.Database.ExecuteSqlAsync($"SELECT 1 FROM Orders WITH (UPDLOCK, ROWLOCK) WHERE OrderID = {orderId}", ct);
        var result = await work();
        await tx.CommitAsync(ct);
        return result;
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            // Unique index duy nhất của Orders là "một bàn một order mở" (OrderConfiguration).
            throw new ConflictException(ErrorCodes.OrderTableHasOpenOrder, "Bàn này đã có order đang mở.");
        }
    }
}
