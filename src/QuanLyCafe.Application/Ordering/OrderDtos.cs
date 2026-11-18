using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Application.Ordering;

public record TableDto(Guid Id, string Name, TableStatus Status, Guid? OpenOrderId);

public record CreateOrderRequest(OrderType OrderType, Guid? TableId);

// Id = OrderDetailID của dòng đã có (sửa SL/ghi chú); null = món mới.
public record OrderItemRequest(Guid ProductId, int Quantity, string? Note, Guid? Id = null);

// PUT gửi đủ các món còn Chờ: dòng có Id được sửa, dòng mới được thêm, món Chờ không còn trong danh sách bị xóa.
// Món đang làm/xong/đã giao giữ nguyên; gửi Id của món đã bắt đầu làm → ERR-CON-001.
public record UpdateOrderItemsRequest(List<OrderItemRequest>? Items);

public record OrderItemDto(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice,
    OrderDetailStatus Status, string? Note);

public record OrderDto(Guid Id, DateTime OrderDate, OrderType OrderType, OrderStatus Status, Guid? TableId,
    string? TableName, decimal TotalAmount, List<OrderItemDto> Items);

// Phiếu pha chế: mã order, bàn (null = mang đi), các dòng món, thời điểm order.
public record BarTicketDto(Guid OrderId, string? TableName, DateTime OrderDate, List<OrderItemDto> Items);
