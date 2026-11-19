using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Domain.Payment;

namespace QuanLyCafe.Application.Payment;

public class PaymentService(
    IOrderRepository orders,
    InventoryService inventory,
    IPaymentRepository payments)
{
    public Task<PaymentResultDto> PayCashAsync(
        PaymentRequest request, Guid cashierId, string? cashierName,
        CancellationToken ct)
    {
        if (request.OrderId == Guid.Empty || request.Method != PaymentMethod.Cash)
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Hãy chọn đơn và phương thức tiền mặt.");

        return payments.RunAsync(request.OrderId, async () =>
        {
            var order = await orders.GetOrderAsync(request.OrderId, ct)
                ?? throw new NotFoundException("Không tìm thấy đơn.");

            order.EnsureEditable();

            if (order.OrderDetails.Count == 0)
                throw new ValidationException(
                    ErrorCodes.ValidationFailed, "Đơn chưa có món.");

            if (request.CashGiven is not decimal cash || cash < order.TotalAmount)
                throw new BusinessRuleException(
                    ErrorCodes.PaymentInsufficientCash, "Tiền khách đưa chưa đủ.");

            var items = order.OrderDetails.Select(line =>
                new StockCheckItem(line.ProductID, line.Quantity));

            var needed = await inventory.GetRequiredMaterialsAsync(items, ct);
            var materials = await payments.GetMaterialsAsync(needed.Keys, ct);

            foreach (var (id, required) in needed)
            {
                if (!materials.TryGetValue(id, out var material) ||
                    material.StockQuantity < required)
                {
                    throw new BusinessRuleException(
                        ErrorCodes.StockInsufficient,
                        $"Không đủ tồn kho để thanh toán. Nguyên liệu: {id}.");
                }
            }

            var paidAt = DateTime.Now;
            var movements = new List<StockMovement>();

            foreach (var (id, required) in needed)
            {
                materials[id].StockQuantity -= required;

                movements.Add(new StockMovement
                {
                    MaterialID = id,
                    UserID = cashierId,
                    OrderID = order.OrderID,
                    MovementType = MovementType.SaleDeduction,
                    MovementDate = paidAt,
                    Quantity = required
                });
            }

            order.MarkPaid(PaymentMethod.Cash, cashierId, paidAt);

            if (order.Table is { } table)
                table.Status = TableStatus.Empty;

            await payments.SaveAsync(movements, ct);

            var lines = order.OrderDetails.Select(line => new InvoiceLineDto(
                line.Product.ProductName, line.Quantity, line.UnitPrice, line.Note))
                .ToList();

            var invoice = new InvoiceDto(
                order.OrderID, order.OrderDate, order.PaidAt,
                order.Table?.TableName, order.OrderType, order.PaymentMethod,
                order.TotalAmount, cashierName, lines);

            return new PaymentResultDto(invoice, cash - order.TotalAmount);
        }, ct);
    }
}