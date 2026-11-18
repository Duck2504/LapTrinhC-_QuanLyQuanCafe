using QuanLyCafe.Application.Common;
using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Application.Ordering;

// FR-02: sơ đồ bàn, mở bàn / order mang đi, thêm-bớt-sửa món khi order chưa thanh toán.
// FR-03: gửi order xuống pha chế sau khi kiểm tồn kho theo BOM.
public class OrderService(IOrderRepository orders, IInventoryService inventory, IRealtimeNotifier notifier)
{
    public const int MaxNoteLength = 200;

    public async Task<List<TableDto>> GetTablesAsync(CancellationToken ct = default)
    {
        var openOrders = await orders.GetOpenOrderIdsByTableAsync(ct);
        return (await orders.GetTablesAsync(ct))
            .Select(t => new TableDto(t.TableID, t.TableName, t.Status,
                openOrders.TryGetValue(t.TableID, out var orderId) ? orderId : null))
            .ToList();
    }

    public async Task<OrderDto> CreateAsync(CreateOrderRequest req, Guid userId, CancellationToken ct = default)
    {
        var order = new Order { OrderType = req.OrderType, UserID = userId };
        switch (req.OrderType)
        {
            case OrderType.DineIn:
                if (req.TableId is not { } tableId)
                    throw new ValidationException(ErrorCodes.ValidationFailed, "Order tại bàn phải chọn bàn.");
                var table = await orders.GetTableAsync(tableId, ct) ?? throw new NotFoundException("Không tìm thấy bàn.");
                if (await orders.HasOpenOrderAsync(tableId, ct))
                    throw new ConflictException(ErrorCodes.OrderTableHasOpenOrder, $"{table.TableName} đã có order đang mở.");
                table.Status = TableStatus.Serving;
                order.TableID = tableId;
                order.Table = table;
                break;
            case OrderType.Takeaway:
                if (req.TableId is not null)
                    throw new ValidationException(ErrorCodes.ValidationFailed, "Order mang đi không gắn bàn.");
                break;
            default:
                throw new ValidationException(ErrorCodes.ValidationFailed, "Loại order không hợp lệ.");
        }

        orders.Add(order);
        await orders.SaveChangesAsync(ct);
        return ToDto(order);
    }

    public async Task<OrderDto> GetAsync(Guid id, CancellationToken ct = default) => ToDto(await GetOrderAsync(id, ct));

    // Khóa dòng order: hai người sửa cùng lúc hoặc sửa đúng lúc thanh toán không ghi đè TotalAmount/trạng thái.
    public Task<OrderDto> UpdateItemsAsync(Guid id, UpdateOrderItemsRequest req, CancellationToken ct = default) =>
        orders.InOrderLockAsync(id, () => UpdateItemsLockedAsync(id, req, ct), ct);

    private async Task<OrderDto> UpdateItemsLockedAsync(Guid id, UpdateOrderItemsRequest req, CancellationToken ct)
    {
        var order = await GetOrderAsync(id, ct);
        order.EnsureEditable();

        var items = req.Items ?? throw new ValidationException(ErrorCodes.ValidationFailed, "Thiếu danh sách món.");
        if (items.Any(i => i.Quantity <= 0))
            throw new ValidationException(ErrorCodes.ValidationFailed, "Số lượng mỗi món phải lớn hơn 0.");
        if (items.Any(i => i.Note?.Trim().Length > MaxNoteLength))
            throw new ValidationException(ErrorCodes.ValidationFailed, $"Ghi chú tối đa {MaxNoteLength} ký tự.");

        // Dòng có Id = món đã có trong order: chỉ sửa SL/ghi chú, giữ OrderDetailID và giá đã chép (phiếu Bar không đổi ID).
        var existing = items.Where(i => i.Id is not null).ToList();
        var byId = order.OrderDetails.ToDictionary(d => d.OrderDetailID);
        if (existing.Select(i => i.Id).Distinct().Count() != existing.Count
            || existing.Any(i => !byId.TryGetValue(i.Id!.Value, out var d) || d.ProductID != i.ProductId))
            throw new ValidationException(ErrorCodes.ValidationFailed, "Có dòng món không thuộc order này.");
        if (existing.Select(i => byId[i.Id!.Value]).FirstOrDefault(d => d.Status != OrderDetailStatus.Pending) is { } started)
            throw new ConflictException(ErrorCodes.ConcurrencyConflict, $"{started.Product.ProductName} đã được pha chế bắt đầu làm, hãy tải lại order.");

        var newItems = items.Where(i => i.Id is null).ToList();
        var products = (await orders.GetProductsAsync(newItems.Select(i => i.ProductId).Distinct(), ct))
            .ToDictionary(p => p.ProductID);
        if (newItems.Any(i => !products.TryGetValue(i.ProductId, out var p) || !p.IsActive))
            throw new ValidationException(ErrorCodes.ValidationFailed, "Có món không tồn tại hoặc đã ngừng bán.");

        foreach (var i in existing)
        {
            var d = byId[i.Id!.Value];
            d.Quantity = i.Quantity;
            d.Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim();
        }
        var keptIds = existing.Select(i => i.Id!.Value).ToHashSet();
        var removed = order.OrderDetails.Where(d => d.Status == OrderDetailStatus.Pending && !keptIds.Contains(d.OrderDetailID)).ToList();
        var kept = order.OrderDetails.Except(removed).ToList();
        var added = newItems.Select(i => new OrderDetail
        {
            OrderID = order.OrderID,
            Order = order,
            ProductID = i.ProductId,
            Product = products[i.ProductId],
            Quantity = i.Quantity,
            UnitPrice = products[i.ProductId].Price, // giá chép tại thời điểm gọi món
            Note = string.IsNullOrWhiteSpace(i.Note) ? null : i.Note.Trim(),
        }).ToList();

        orders.RemoveDetails(removed);
        orders.AddDetails(added);
        order.TotalAmount = kept.Concat(added).Sum(d => d.Quantity * d.UnitPrice);
        await orders.SaveChangesAsync(ct);

        // Trả đúng danh sách sau khi sửa, không phụ thuộc EF đã đồng bộ collection hay chưa.
        return ToDto(order) with { Items = kept.Concat(added).Select(ToItemDto).ToList() };
    }

    // Gửi các món đang Chờ xuống Bar. Đọc trong lock order để PUT đồng thời không đổi món giữa lúc kiểm kho và lúc phát phiếu;
    // TicketCreated phát SAU khi lock/transaction đóng. Gửi lại phát toàn bộ món còn Chờ — Bar khớp theo OrderDetailID nên không trùng.
    public async Task<OrderDto> SubmitAsync(Guid id, CancellationToken ct = default)
    {
        var (dto, ticket) = await orders.InOrderLockAsync(id, async () =>
        {
            var order = await GetOrderAsync(id, ct);
            order.EnsureEditable();

            var pending = order.OrderDetails.Where(d => d.Status == OrderDetailStatus.Pending).ToList();
            if (pending.Count == 0)
                throw new ValidationException(ErrorCodes.ValidationFailed, "Order chưa có món mới để gửi pha chế.");

            await inventory.CheckStockAsync(pending.GroupBy(d => d.ProductID)
                .Select(g => new StockCheckItem(g.Key, g.Sum(d => d.Quantity))).ToList(), ct);

            return (ToDto(order), new BarTicketDto(order.OrderID, order.Table?.TableName, order.OrderDate, pending.Select(ToItemDto).ToList()));
        }, ct);

        await notifier.TicketCreatedAsync(ticket, ct);
        return dto;
    }

    private async Task<Order> GetOrderAsync(Guid id, CancellationToken ct) =>
        await orders.GetOrderAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy order.");

    private static OrderDto ToDto(Order o) => new(o.OrderID, o.OrderDate, o.OrderType, o.Status, o.TableID,
        o.Table?.TableName, o.TotalAmount, o.OrderDetails.Select(ToItemDto).ToList());

    private static OrderItemDto ToItemDto(OrderDetail d) =>
        new(d.OrderDetailID, d.ProductID, d.Product.ProductName, d.Quantity, d.UnitPrice, d.Status, d.Note);
}
