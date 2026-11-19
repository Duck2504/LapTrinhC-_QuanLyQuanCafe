namespace QuanLyCafe.Desktop.Models;

// POST /api/payments
public record PaymentRequest(Guid OrderId, PaymentMethod Method, decimal? CashGiven);

// GET /api/orders/{id}/invoice
public record InvoiceLineDto(string ProductName, int Quantity, decimal UnitPrice, string? Note)
{
    public decimal LineTotal => Quantity * UnitPrice;
}

public record InvoiceDto(
    Guid OrderId, DateTime OrderDate, DateTime? PaidAt,
    string? TableName, OrderType OrderType, PaymentMethod? PaymentMethod,
    decimal TotalAmount, string? CashierName,
    List<InvoiceLineDto> Lines);

// Kết quả POST /api/payments
public record PaymentResultDto(InvoiceDto Invoice, decimal? Change);
