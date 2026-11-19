namespace QuanLyCafe.Application.Reporting;

public interface IReportRepository
{
    Task<List<PaidOrderTotal>> GetPaidOrderTotalsAsync(
        DateTime? start, DateTime? endExclusive, CancellationToken ct);

    Task<List<TopProductDto>> GetTopProductsAsync(
        DateTime? start, DateTime? endExclusive, CancellationToken ct);

    Task<List<InventoryReportDto>> GetInventoryAsync(
        DateTime? start, DateTime? endExclusive, CancellationToken ct);
}

public record PaidOrderTotal(DateTime PaidAt, decimal TotalAmount);
