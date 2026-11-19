using System.Text.Json;
using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Domain.Payment;

namespace QuanLyCafe.Application.Payment;

public class PaymentService(
    IOrderRepository orders,
    InventoryService inventory,
    IPaymentRepository payments,
    IPaymentGateway gateway)
{
    public async Task<PaymentResultDto> PayAsync(
        PaymentRequest request, Guid cashierId, string? cashierName,
        CancellationToken ct)
    {
        if (request.OrderId == Guid.Empty || !Enum.IsDefined(request.Method))
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Hãy chọn đơn và phương thức thanh toán hợp lệ.");

        decimal? approvedAmount = null;
        if (request.Method != PaymentMethod.Cash)
        {
            var preview = await payments.GetPaymentPreviewAsync(request.OrderId, ct)
                ?? throw new NotFoundException("Không tìm thấy đơn.");

            if (preview.Status != OrderStatus.Paid)
            {
                EnsurePayable(preview.Status, preview.HasItems);
                await AuthorizeElectronicPaymentAsync(request, preview.TotalAmount, ct);
                approvedAmount = preview.TotalAmount;
            }
        }

        return await payments.RunAsync(request.OrderId, async () =>
        {
            var order = await orders.GetOrderAsync(request.OrderId, ct)
                ?? throw new NotFoundException("Không tìm thấy đơn.");

            if (order.Status == OrderStatus.Paid)
                return GetExistingResult(order);

            EnsurePayable(order.Status, order.OrderDetails.Count > 0);

            decimal change = 0;
            if (request.Method == PaymentMethod.Cash)
            {
                if (request.CashGiven is not decimal cash || cash < order.TotalAmount)
                    throw new BusinessRuleException(
                        ErrorCodes.PaymentInsufficientCash, "Tiền khách đưa chưa đủ.");

                change = cash - order.TotalAmount;
            }
            else if (approvedAmount != order.TotalAmount)
            {
                throw new ConflictException(
                    ErrorCodes.ConcurrencyConflict, "Tổng tiền đơn đã thay đổi. Vui lòng kiểm tra và thanh toán lại.");
            }

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

            order.MarkPaid(request.Method, cashierId, paidAt);

            if (order.Table is { } table)
                table.Status = TableStatus.Empty;

            var result = new PaymentResultDto(BuildInvoice(order, cashierName), change);
            order.PaymentResultJson = JsonSerializer.Serialize(result);
            await payments.SaveAsync(movements, ct);
            return result;
        }, ct);
    }

    private async Task AuthorizeElectronicPaymentAsync(
        PaymentRequest request, decimal amount, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        try
        {
            await gateway.AuthorizeAsync(request.OrderId, request.Method, amount, timeout.Token)
                .WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            throw new PaymentGatewayException("Cổng thanh toán không phản hồi trong 15 giây.");
        }
    }

    private static void EnsurePayable(OrderStatus status, bool hasItems)
    {
        if (status != OrderStatus.Open)
            throw new BusinessRuleException(ErrorCodes.OrderAlreadyPaid, "Đơn không còn mở để thanh toán.");

        if (!hasItems)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Đơn chưa có món.");
    }

    private static PaymentResultDto GetExistingResult(Order order)
    {
        if (order.PaymentResultJson is { } json)
        {
            return JsonSerializer.Deserialize<PaymentResultDto>(json)
                ?? throw new InvalidOperationException("Kết quả thanh toán đã lưu không hợp lệ.");
        }

        decimal? change = order.PaymentMethod == PaymentMethod.Cash ? null : 0m;
        return new PaymentResultDto(BuildInvoice(order, order.Cashier?.FullName), change);
    }

    private static InvoiceDto BuildInvoice(Order order, string? cashierName)
    {
        var lines = order.OrderDetails.Select(line => new InvoiceLineDto(
            line.Product.ProductName, line.Quantity, line.UnitPrice, line.Note))
            .ToList();

        return new InvoiceDto(
            order.OrderID, order.OrderDate, order.PaidAt,
            order.Table?.TableName, order.OrderType, order.PaymentMethod,
            order.TotalAmount, cashierName, lines);
    }
}
