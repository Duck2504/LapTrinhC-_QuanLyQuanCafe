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

    public async Task<List<OrderDto>> GetOpenOrdersAsync(CancellationToken ct) =>
        (await orders.GetOpenOrdersAsync(ct))
            .Where(order => order.OrderDetails.Count > 0)
            .OrderBy(order => order.OrderDate)
            .Select(ToDto)
            .ToList();

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

        return ToDto(order) with { Items = kept.Concat(added).Select(ToItemDto).ToList() };
    }

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

        public async Task<List<BarTicketDto>> GetBarTicketsAsync(CancellationToken ct = default) =>
        (await orders.GetOpenOrdersAsync(ct))
            .Select(o => new BarTicketDto(o.OrderID, o.Table?.TableName, o.OrderDate,
                o.OrderDetails.Where(d => d.Status != OrderDetailStatus.Delivered).Select(ToItemDto).ToList()))
            .Where(t => t.Items.Count > 0)
            .OrderBy(t => t.OrderDate)
            .ToList();

    public async Task<OrderItemDto> UpdateItemStatusAsync(Guid itemId, UpdateItemStatusRequest req, bool byWaiter, CancellationToken ct = default)
    {
        var allowed = byWaiter
            ? req.Status == OrderDetailStatus.Delivered
            : req.Status is OrderDetailStatus.Preparing or OrderDetailStatus.Done;
        if (!allowed)
            throw new ValidationException(ErrorCodes.ValidationFailed,
                byWaiter ? "Phục vụ chỉ được chuyển món sang Đã giao." : "Pha chế chỉ được chuyển món sang Đang làm hoặc Xong.");

        var orderId = await orders.GetOrderIdOfDetailAsync(itemId, ct) ?? throw new NotFoundException("Không tìm thấy món.");
        var (dto, change) = await orders.InOrderLockAsync(orderId, async () =>
        {
            var order = await GetOrderAsync(orderId, ct);
            order.EnsureEditable();
            var d = order.OrderDetails.FirstOrDefault(x => x.OrderDetailID == itemId) ?? throw new NotFoundException("Không tìm thấy món.");
            if (d.Status != req.Status - 1)
                throw new ConflictException(ErrorCodes.ConcurrencyConflict, $"{d.Product.ProductName} đang ở trạng thái khác, hãy tải lại danh sách.");

            d.Status = req.Status;
            await orders.SaveChangesAsync(ct);
            return (ToItemDto(d), new ItemStatusChangedDto(d.OrderDetailID, order.OrderID, d.Status, d.Product.ProductName, order.Table?.TableName));
        }, ct);

        await notifier.ItemStatusChangedAsync(change, CancellationToken.None); // sau commit; client hủy request thì vẫn phải báo
        return dto;
    }

    public async Task ReportUnavailableAsync(Guid itemId, CancellationToken ct = default)
    {
        var orderId = await orders.GetOrderIdOfDetailAsync(itemId, ct) ?? throw new NotFoundException("Không tìm thấy món.");
        var order = await GetOrderAsync(orderId, ct);
        order.EnsureEditable();
        var d = order.OrderDetails.FirstOrDefault(x => x.OrderDetailID == itemId) ?? throw new NotFoundException("Không tìm thấy món.");
        if (d.Status != OrderDetailStatus.Pending)
            throw new ConflictException(ErrorCodes.ConcurrencyConflict, $"{d.Product.ProductName} đã bắt đầu làm, không báo hết được.");

        await notifier.ItemUnavailableAsync(new ItemUnavailableDto(d.ProductID, d.Product.ProductName, order.OrderID, order.Table?.TableName), CancellationToken.None);
    }

    // Gộp bàn: toàn bộ món của bàn nguồn sang order bàn đích (giữ ID, đơn giá, trạng thái); order nguồn Hủy, bàn nguồn Trống.
    public async Task<OrderDto> MergeTablesAsync(MergeTablesRequest req, CancellationToken ct = default)
    {
        var (sourceId, targetId, _) = await GetMergeSplitOrdersAsync(req.SourceTableId, req.TargetTableId, ct);
        if (targetId is null)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Chỉ gộp được vào bàn đang có order.");

        var (dto, ticket) = await orders.InOrdersLockAsync([sourceId, targetId.Value], async () =>
        {
            var source = await GetOpenOrderAsync(sourceId, ct);
            var target = await GetOpenOrderAsync(targetId.Value, ct);
            foreach (var d in source.OrderDetails.ToList())
                Move(d, source, target);
            source.Status = OrderStatus.Cancelled;
            source.Table!.Status = TableStatus.Empty;
            Recalculate(source);
            Recalculate(target);
            await orders.SaveChangesAsync(ct);
            return (ToDto(target), ToTicket(target));
        }, ct);

        await NotifyBarAsync(ticket);
        return dto;
    }

    // Tách bàn: chuyển một phần số lượng món sang bàn đích (bàn trống → mở order mới). Không chuyển hết món — dùng gộp bàn.
    public async Task<OrderDto> SplitTableAsync(SplitTableRequest req, Guid userId, CancellationToken ct = default)
    {
        var items = req.Items is { Count: > 0 } list ? list
            : throw new ValidationException(ErrorCodes.ValidationFailed, "Chọn ít nhất một món để tách.");
        if (items.Any(i => i.Quantity <= 0) || items.Select(i => i.OrderItemId).Distinct().Count() != items.Count)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Danh sách món tách không hợp lệ.");

        var (sourceId, targetId, targetTable) = await GetMergeSplitOrdersAsync(req.SourceTableId, req.TargetTableId, ct);
        var (dto, ticket) = await orders.InOrdersLockAsync(targetId is { } t ? [sourceId, t] : [sourceId], async () =>
        {
            var source = await GetOpenOrderAsync(sourceId, ct);
            var byId = source.OrderDetails.ToDictionary(d => d.OrderDetailID);
            if (items.Any(i => !byId.TryGetValue(i.OrderItemId, out var d) || i.Quantity > d.Quantity))
                throw new ValidationException(ErrorCodes.ValidationFailed, "Có món không thuộc bàn nguồn hoặc tách quá số lượng, hãy tải lại order.");
            if (items.Sum(i => i.Quantity) == source.OrderDetails.Sum(d => d.Quantity))
                throw new ValidationException(ErrorCodes.ValidationFailed, "Không thể tách hết món, hãy dùng Gộp bàn.");

            Order target;
            if (targetId is { } existing)
                target = await GetOpenOrderAsync(existing, ct);
            else
            {
                target = new Order { OrderType = OrderType.DineIn, UserID = userId, TableID = targetTable.TableID, Table = targetTable };
                targetTable.Status = TableStatus.Serving;
                orders.Add(target); // bàn đích vừa có order khác → unique index chặn, ERR-ORDER-002
            }

            foreach (var i in items)
            {
                var d = byId[i.OrderItemId];
                if (i.Quantity == d.Quantity)
                {
                    Move(d, source, target);
                    continue;
                }
                d.Quantity -= i.Quantity;
                orders.AddDetails([new OrderDetail
                {
                    OrderID = target.OrderID, Order = target, ProductID = d.ProductID, Product = d.Product,
                    Quantity = i.Quantity, UnitPrice = d.UnitPrice, Status = d.Status, Note = d.Note,
                }]);
            }
            Recalculate(source);
            Recalculate(target);
            await orders.SaveChangesAsync(ct);
            return (ToDto(target), ToTicket(target));
        }, ct);

        await NotifyBarAsync(ticket);
        return dto;
    }

    // Bàn nguồn phải có order mở; trả order mở của bàn đích (nếu có) và bàn đích.
    private async Task<(Guid SourceOrderId, Guid? TargetOrderId, Table TargetTable)> GetMergeSplitOrdersAsync(
        Guid sourceTableId, Guid targetTableId, CancellationToken ct)
    {
        if (sourceTableId == targetTableId)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Bàn nguồn và bàn đích phải khác nhau.");
        var source = await orders.GetTableAsync(sourceTableId, ct) ?? throw new NotFoundException("Không tìm thấy bàn nguồn.");
        var target = await orders.GetTableAsync(targetTableId, ct) ?? throw new NotFoundException("Không tìm thấy bàn đích.");
        var open = await orders.GetOpenOrderIdsByTableAsync(ct);
        if (!open.TryGetValue(sourceTableId, out var sourceOrderId))
            throw new ValidationException(ErrorCodes.ValidationFailed, $"{source.TableName} chưa có order, không gộp/tách được.");
        return (sourceOrderId, open.TryGetValue(targetTableId, out var t) ? t : null, target);
    }

    // Đọc lại order trong khóa: vừa bị thanh toán hoặc bị gộp (Hủy) trước khi khóa → ERR-ORDER-001.
    private async Task<Order> GetOpenOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await GetOrderAsync(orderId, ct);
        order.EnsureEditable();
        return order;
    }

    private static void Move(OrderDetail d, Order from, Order to)
    {
        from.OrderDetails.Remove(d);
        d.OrderID = to.OrderID;
        d.Order = to;
        to.OrderDetails.Add(d);
    }

    private static void Recalculate(Order o) => o.TotalAmount = o.OrderDetails.Sum(d => d.Quantity * d.UnitPrice);

    private static BarTicketDto ToTicket(Order o) => new(o.OrderID, o.Table?.TableName, o.OrderDate,
        o.OrderDetails.Where(d => d.Status != OrderDetailStatus.Delivered).Select(ToItemDto).ToList());

    // Sau commit: Bar tải lại danh sách để thấy món đổi bàn.
    private Task NotifyBarAsync(BarTicketDto ticket) =>
        ticket.Items.Count > 0 ? notifier.TicketCreatedAsync(ticket, CancellationToken.None) : Task.CompletedTask;

    private async Task<Order> GetOrderAsync(Guid id, CancellationToken ct) =>
        await orders.GetOrderAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy order.");

    private static OrderDto ToDto(Order o) => new(o.OrderID, o.OrderDate, o.OrderType, o.Status, o.TableID,
        o.Table?.TableName, o.TotalAmount, o.OrderDetails.Select(ToItemDto).ToList());

    private static OrderItemDto ToItemDto(OrderDetail d) =>
        new(d.OrderDetailID, d.ProductID, d.Product.ProductName, d.Quantity, d.UnitPrice, d.Status, d.Note);
}
