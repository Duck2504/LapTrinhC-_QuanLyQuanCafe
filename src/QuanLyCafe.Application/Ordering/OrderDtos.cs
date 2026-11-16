using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Application.Ordering;

public record TableDto(Guid Id, string Name, TableStatus Status, Guid? OpenOrderId);

public record CreateOrderRequest(OrderType OrderType, Guid? TableId);

public record OrderItemRequest(Guid ProductId, int Quantity, string? Note);

// PUT thay toàn bộ các món còn ở trạng thái Chờ; món đang làm/xong/đã giao giữ nguyên.
public record UpdateOrderItemsRequest(List<OrderItemRequest>? Items);

public record OrderItemDto(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice,
    OrderDetailStatus Status, string? Note);

public record OrderDto(Guid Id, DateTime OrderDate, OrderType OrderType, OrderStatus Status, Guid? TableId,
    string? TableName, decimal TotalAmount, List<OrderItemDto> Items);
