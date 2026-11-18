using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Application.Ordering;

public record TableDto(Guid Id, string Name, TableStatus Status, Guid? OpenOrderId);

public record CreateOrderRequest(OrderType OrderType, Guid? TableId);


public record OrderItemRequest(Guid ProductId, int Quantity, string? Note, Guid? Id = null);

public record UpdateOrderItemsRequest(List<OrderItemRequest>? Items);

public record OrderItemDto(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice,
    OrderDetailStatus Status, string? Note);

public record OrderDto(Guid Id, DateTime OrderDate, OrderType OrderType, OrderStatus Status, Guid? TableId,
    string? TableName, decimal TotalAmount, List<OrderItemDto> Items);

public record BarTicketDto(Guid OrderId, string? TableName, DateTime OrderDate, List<OrderItemDto> Items);

public record UpdateItemStatusRequest(OrderDetailStatus Status);

public record ItemStatusChangedDto(Guid OrderDetailId, Guid OrderId, OrderDetailStatus Status);
public record ItemUnavailableDto(Guid ProductId, string ProductName, Guid OrderId, string? TableName);
