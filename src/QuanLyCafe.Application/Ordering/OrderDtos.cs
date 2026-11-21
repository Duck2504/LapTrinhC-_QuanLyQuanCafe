using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Application.Ordering;

public record TableDto(Guid Id, string Name, TableStatus Status, Guid? OpenOrderId);

public record CreateOrderRequest(OrderType OrderType, Guid? TableId);


public record OrderItemRequest(Guid ProductId, int Quantity, string? Note, Guid? Id = null);

public record UpdateOrderItemsRequest(List<OrderItemRequest>? Items);

// Web (khách quét QR): POST /api/public/tables/{id}/orders.
public record GuestOrderRequest(List<OrderItemRequest>? Items);
public record GuestTableDto(Guid Id, string Name, OrderDto? OpenOrder);

public record OrderItemDto(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice,
    OrderDetailStatus Status, string? Note);

public record OrderDto(Guid Id, DateTime OrderDate, OrderType OrderType, OrderStatus Status, Guid? TableId,
    string? TableName, decimal TotalAmount, List<OrderItemDto> Items);

public record BarTicketDto(Guid OrderId, string? TableName, DateTime OrderDate, List<OrderItemDto> Items);

public record UpdateItemStatusRequest(OrderDetailStatus Status);

public record ItemStatusChangedDto(Guid OrderDetailId, Guid OrderId, OrderDetailStatus Status, string ProductName, string? TableName);
public record ItemUnavailableDto(Guid ProductId, string ProductName, Guid OrderId, string? TableName);

// POST /api/tables/merge: chuyển toàn bộ món bàn nguồn sang bàn đích (cả hai đang có order).
public record MergeTablesRequest(Guid SourceTableId, Guid TargetTableId);

// POST /api/tables/split: chuyển một phần món (theo số lượng) từ bàn nguồn sang bàn đích.
public record SplitItemRequest(Guid OrderItemId, int Quantity);
public record SplitTableRequest(Guid SourceTableId, Guid TargetTableId, List<SplitItemRequest>? Items);
