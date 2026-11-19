using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Application.Payment;

public interface IPaymentRepository
{
    Task<T> RunAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct);

    Task<Dictionary<Guid, Material>> GetMaterialsAsync(
        IEnumerable<Guid> ids, CancellationToken ct);

    Task SaveAsync(IEnumerable<StockMovement> movements, CancellationToken ct);
}