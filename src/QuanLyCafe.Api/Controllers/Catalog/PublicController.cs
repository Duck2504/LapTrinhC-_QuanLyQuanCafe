using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCafe.Application.Catalog;
using QuanLyCafe.Application.Ordering;

namespace QuanLyCafe.Api.Controllers.Catalog;

// Endpoint cho trang Web khách hàng (QuanLyCafe.Web): không cần đăng nhập.
// Chỉ đọc thực đơn + gọi món tại bàn; bàn được nhận diện bằng TableID (Guid, in trong mã QR).
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController(CatalogService catalog, OrderService orders) : ControllerBase
{
    [HttpGet("categories")]
    public Task<List<CategoryDto>> GetCategories(CancellationToken ct) => catalog.GetCategoriesAsync(onlyActive: true, ct);

    [HttpGet("menu")]
    public Task<List<MenuItemDto>> GetMenu(string? q, Guid? categoryId, CancellationToken ct) =>
        catalog.GetMenuAsync(ct, q, categoryId);

    [HttpGet("products/{id:guid}")]
    public Task<MenuItemDto> GetProduct(Guid id, CancellationToken ct) => catalog.GetMenuItemAsync(id, ct);

    [HttpGet("tables/{id:guid}")]
    public Task<GuestTableDto> GetTable(Guid id, CancellationToken ct) => orders.GetGuestTableAsync(id, ct);

    [HttpPost("tables/{id:guid}/orders")]
    public Task<OrderDto> Order(Guid id, GuestOrderRequest req, CancellationToken ct) => orders.GuestOrderAsync(id, req, ct);
}
