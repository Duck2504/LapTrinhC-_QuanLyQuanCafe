using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCafe.Application.Catalog;

namespace QuanLyCafe.Api.Controllers.Catalog;

[ApiController]
[Route("api")]
[Authorize]

public class CatalogController(CatalogService catalog) : ControllerBase
{
    [HttpGet("menu")]
    [Authorize(Roles = "Manager,Cashier,Waiter")]
    public Task<List<MenuItemDto>> GetMenu(CancellationToken ct)
        => catalog.GetMenuAsync(ct);

    [HttpPut("products/{id:guid}/bom")]
    [Authorize(Roles = "Manager")]
    public async Task<IActionResult> SetBom(
        Guid id, SetBomRequest request, CancellationToken ct)
    {
        await catalog.SetBomAsync(id, request, ct);
        return NoContent();
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