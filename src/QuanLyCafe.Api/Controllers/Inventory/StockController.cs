using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using QuanLyCafe.Application.Inventory;

namespace QuanLyCafe.Api.Controllers.Inventory;

[ApiController]
[Route("api/stock")]
[Authorize(Roles = "Manager")]
public class StockController(StockService stock) : ControllerBase
{
    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

    [HttpPost("receipts")]
    public async Task<IActionResult> Receive(
        SaveStockMovementRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await stock.ReceiveAsync(request, CurrentUserId, ct));

    [HttpPost("issues")]
    public async Task<IActionResult> Issue(
        SaveStockMovementRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created,
            await stock.IssueAsync(request, CurrentUserId, ct));

    [HttpGet("movements")]
    public Task<PagedResult<StockMovementDto>> GetMovements(
        CancellationToken ct, int page = 1, int pageSize = 20) =>
        stock.GetMovementsAsync(page, pageSize, ct);
    
    [HttpPost("stocktakes")]
    public Task<StocktakeResultDto> Stocktake(
        StocktakeRequest request, CancellationToken ct) =>
        stock.StocktakeAsync(request, CurrentUserId, ct);
}