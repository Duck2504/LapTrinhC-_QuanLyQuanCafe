using QuanLyCafe.Domain.Payment;

namespace QuanLyCafe.Application.Payment;

public interface IPaymentGateway
{
    Task AuthorizeAsync(
        Guid orderId, PaymentMethod method, decimal amount, CancellationToken ct);
}
