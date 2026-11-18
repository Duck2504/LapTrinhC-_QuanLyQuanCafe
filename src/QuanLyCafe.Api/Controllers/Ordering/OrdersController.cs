using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using QuanLyCafe.Application.Ordering;

namespace QuanLyCafe.Api.Controllers.Ordering;

[ApiController]
[Route("api")]
public class OrdersController(OrderService orders) : ControllerBase
{
    private Guid CurrentUserId => Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpGet("tables")]
    [Authorize(Roles = "Manager,Cashier,Waiter")]
    public Task<List<TableDto>> GetTables(CancellationToken ct) => orders.GetTablesAsync(ct);

    [HttpPost("tables/merge")]
    [Authorize(Roles = "Cashier,Waiter")]
    public Task<OrderDto> MergeTables(MergeTablesRequest req, CancellationToken ct) => orders.MergeTablesAsync(req, ct);

    [HttpPost("tables/split")]
    [Authorize(Roles = "Cashier,Waiter")]
    public Task<OrderDto> SplitTable(SplitTableRequest req, CancellationToken ct) => orders.SplitTableAsync(req, CurrentUserId, ct);

    [HttpPost("orders")]
    [Authorize(Roles = "Cashier,Waiter")]
    public async Task<IActionResult> Create(CreateOrderRequest req, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await orders.CreateAsync(req, CurrentUserId, ct));

    [HttpGet("orders/{id:guid}")]
    [Authorize(Roles = "Manager,Cashier,Waiter")]
    public Task<OrderDto> Get(Guid id, CancellationToken ct) => orders.GetAsync(id, ct);

    [HttpPut("orders/{id:guid}/items")]
    [Authorize(Roles = "Cashier,Waiter")]
    public Task<OrderDto> UpdateItems(Guid id, UpdateOrderItemsRequest req, CancellationToken ct) =>
        orders.UpdateItemsAsync(id, req, ct);

    [HttpPost("orders/{id:guid}/submit")]
    [Authorize(Roles = "Cashier,Waiter")]
    public Task<OrderDto> Submit(Guid id, CancellationToken ct) => orders.SubmitAsync(id, ct);

    [HttpGet("bar/tickets")]
    [Authorize(Roles = "Manager,Barista")]
    public Task<List<BarTicketDto>> GetBarTickets(CancellationToken ct) => orders.GetBarTicketsAsync(ct);

    [HttpPut("order-items/{id:guid}/status")]
    [Authorize(Roles = "Barista,Waiter")]
    public Task<OrderItemDto> UpdateItemStatus(Guid id, UpdateItemStatusRequest req, CancellationToken ct) =>
        orders.UpdateItemStatusAsync(id, req, User.IsInRole("Waiter"), ct);

    [HttpPost("order-items/{id:guid}/unavailable")]
    [Authorize(Roles = "Barista")]
    public async Task<IActionResult> ReportUnavailable(Guid id, CancellationToken ct)
    {
        await orders.ReportUnavailableAsync(id, ct);
        return NoContent();
    }
}
