namespace QuanLyCafe.Desktop.Models;

// GET /api/tables
public record TableDto(Guid Id, string Name, TableStatus Status, Guid? OpenOrderId);

// GET /api/orders/{id}, POST /api/orders, PUT /api/orders/{id}/items, POST /api/orders/{id}/submit
public record OrderItemDto(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice,
    OrderDetailStatus Status, string? Note);
public record OrderDto(Guid Id, DateTime OrderDate, OrderType OrderType, OrderStatus Status, Guid? TableId,
    string? TableName, decimal TotalAmount, List<OrderItemDto> Items);

// BarHub "TicketCreated"
public record BarTicketDto(Guid OrderId, string? TableName, DateTime OrderDate, List<OrderItemDto> Items);

// PUT /api/order-items/{id}/status
public record UpdateItemStatusRequest(OrderDetailStatus Status);

// BarHub "ItemStatusChanged"
public record ItemStatusChangedDto(Guid OrderDetailId, Guid OrderId, OrderDetailStatus Status);
