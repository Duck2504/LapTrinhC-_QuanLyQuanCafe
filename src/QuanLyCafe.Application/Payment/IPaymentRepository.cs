using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Application.Payment;

public interface IPaymentRepository
{
    Task<PaymentPreview?> GetPaymentPreviewAsync(Guid orderId, CancellationToken ct);

    Task<T> RunAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct);

    Task<Dictionary<Guid, Material>> GetMaterialsAsync(
        IEnumerable<Guid> ids, CancellationToken ct);

    Task SaveAsync(IEnumerable<StockMovement> movements, CancellationToken ct);
}

public record PaymentPreview(OrderStatus Status, decimal TotalAmount, bool HasItems);
