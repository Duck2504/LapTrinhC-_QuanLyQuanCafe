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
}
