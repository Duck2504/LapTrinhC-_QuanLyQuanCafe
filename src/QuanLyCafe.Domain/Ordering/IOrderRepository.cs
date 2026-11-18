using QuanLyCafe.Domain.Catalog;

namespace QuanLyCafe.Domain.Ordering;

public interface IOrderRepository
{
    Task<List<Table>> GetTablesAsync(CancellationToken ct = default);

    Task<Table?> GetTableAsync(Guid tableId, CancellationToken ct = default);
    Task<Dictionary<Guid, Guid>> GetOpenOrderIdsByTableAsync(CancellationToken ct = default);

    Task<bool> HasOpenOrderAsync(Guid tableId, CancellationToken ct = default);

    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default);

    Task<List<Order>> GetOpenOrdersAsync(CancellationToken ct = default);

    Task<Guid?> GetOrderIdOfDetailAsync(Guid orderDetailId, CancellationToken ct = default);

    Task<List<Product>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default);

    void Add(Order order);

    void AddDetails(IEnumerable<OrderDetail> details);

    void RemoveDetails(IEnumerable<OrderDetail> details);

    Task<T> InOrderLockAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
