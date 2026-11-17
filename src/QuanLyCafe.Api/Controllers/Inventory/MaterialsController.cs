using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCafe.Application.Inventory;

namespace QuanLyCafe.Api.Controllers.Inventory;

[ApiController]
[Route("api/materials")]
[Authorize(Roles = "Manager")]
public class MaterialsController(IMaterialRepository materials) : ControllerBase
{
    [HttpGet]
    public async Task<List<MaterialDto>> Get(CancellationToken ct)
    {
        var items = await materials.GetAllAsync(ct);

        return items.Select(m => new MaterialDto(
            m.MaterialID, m.MaterialName, m.Unit,
            m.StockQuantity, m.MinQuantity,
            m.StockQuantity <= m.MinQuantity)).ToList();
    }
}