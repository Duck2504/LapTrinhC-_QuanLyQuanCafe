using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace QuanLyCafe.Api.Controllers.Catalog;

[ApiController]
[Route("api")]
[Authorize]
public class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("menu")]
    [Authorize(Roles = "Manager,Cashier,Waiter")]
    public Task<List<MenuItemDto>> GetMenu(CancellationToken ct)
    {
        return catalog.GetMenuAsync(ct);
    }

    [HttpGet("products")]
    [Authorize(Roles = "Manager")]
    public Task<List<ProductDto>> GetProducts(CancellationToken ct)
        => catalog.GetProductsAsync(ct);

    [HttpPost("products")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> CreateProduct(
        SaveProductRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created,
            await catalog.CreateAsync(request, ct));

    [HttpPut("products/{id:guid}")]
    [Authorize(Roles = "Manager")]
    public Task<ProductDto> UpdateProduct(
        Guid id, SaveProductRequest request, CancellationToken ct)
        => catalog.UpdateAsync(id, request, ct);

}