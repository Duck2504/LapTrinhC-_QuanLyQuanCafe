using QuanLyCafe.Application.Payment;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Payment;

namespace QuanLyCafe.Infrastructure.PaymentGateway;

public class MockPaymentGateway(string mode) : IPaymentGateway
{
    public Task AuthorizeAsync(
        Guid orderId, PaymentMethod method, decimal amount, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        return mode switch
        {
            "Success" => Task.CompletedTask,
            "Declined" => throw new PaymentGatewayException("Cổng thanh toán từ chối giao dịch."),
            "Timeout" => Task.Delay(Timeout.InfiniteTimeSpan, ct),
            _ => throw new InvalidOperationException("PaymentGateway:Mode phải là Success, Declined hoặc Timeout.")
        };
    }
}
