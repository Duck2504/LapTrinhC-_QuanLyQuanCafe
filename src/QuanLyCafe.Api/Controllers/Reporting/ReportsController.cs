using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuanLyCafe.Application.Reporting;

namespace QuanLyCafe.Api.Controllers.Reporting;

[ApiController]
[Route("api/reports")]
[Authorize(Roles = "Manager")]
public sealed class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("revenue")]
    public Task<List<RevenuePointDto>> GetRevenue(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct,
        [FromQuery] string groupBy = "day") =>
        reports.GetRevenueAsync(from, to, groupBy, ct);

    [HttpGet("top-products")]
    public Task<List<TopProductDto>> GetTopProducts(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        reports.GetTopProductsAsync(from, to, ct);

    [HttpGet("inventory")]
    public Task<List<InventoryReportDto>> GetInventory(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct) =>
        reports.GetInventoryAsync(from, to, ct);
}
