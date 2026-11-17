using QuanLyCafe.Domain.Catalog;

namespace QuanLyCafe.Domain.Ordering;

public interface IOrderRepository
{
    Task<List<Table>> GetTablesAsync(CancellationToken ct = default);

    Task<Table?> GetTableAsync(Guid tableId, CancellationToken ct = default);

    // TableID → OrderID của order đang mở trên bàn đó.
    Task<Dictionary<Guid, Guid>> GetOpenOrderIdsByTableAsync(CancellationToken ct = default);

    Task<bool> HasOpenOrderAsync(Guid tableId, CancellationToken ct = default);

    // Kèm Table, OrderDetails và Product của từng dòng.
    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default);

    Task<List<Product>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default);

    void Add(Order order);

    void AddDetails(IEnumerable<OrderDetail> details);

    void RemoveDetails(IEnumerable<OrderDetail> details);

    // Chạy work trong một transaction, khóa dòng order (UPDLOCK) tới khi commit.
    // Mọi thao tác ghi lên order đã mở (sửa món, đổi trạng thái món, thanh toán) phải đi qua đây
    // để không ghi đè lẫn nhau. Đọc order BÊN TRONG work để thấy dữ liệu mới nhất.
    Task<T> InOrderLockAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct = default);

    // Vi phạm index "một bàn một order mở" khi hai người mở cùng lúc → ConflictException ERR-ORDER-002.
    Task SaveChangesAsync(CancellationToken ct = default);
}
