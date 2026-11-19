using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Domain.Payment;

namespace QuanLyCafe.Application.Payment;

public record PaymentRequest(Guid OrderId, PaymentMethod Method, decimal? CashGiven);

public record InvoiceLineDto(string ProductName, int Quantity, decimal UnitPrice, string? Note)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record InvoiceDto(
    Guid OrderId, DateTime OrderDate, DateTime? PaidAt,
    string? TableName, OrderType OrderType, PaymentMethod? PaymentMethod,
    decimal TotalAmount, string? CashierName,
    List<InvoiceLineDto> Lines);

public record PaymentResultDto(InvoiceDto Invoice, decimal Change);
