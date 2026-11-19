using QuanLyCafe.Application.Reporting;
using QuanLyCafe.Domain.Common;

namespace QuanLyCafe.Tests.Reporting;

public sealed class ReportServiceTests
{
    private readonly StubReportRepository repository = new();

    [Fact]
    public async Task Revenue_UsesWholeEndDateAndGroupsByPaidAt()
    {
        var date = new DateOnly(2026, 11, 19);
        repository.Orders =
        [
            new(new DateTime(2026, 11, 19, 0, 0, 0), 25_000m),
            new(new DateTime(2026, 11, 19, 23, 59, 59), 35_000m),
            new(new DateTime(2026, 11, 20, 0, 0, 0), 55_000m)
        ];
        var service = CreateService();

        var daily = await service.GetRevenueAsync(date, date, "day", CancellationToken.None);
        var monthly = await service.GetRevenueAsync(date, date, "month", CancellationToken.None);

        Assert.Equal(new RevenuePointDto("2026-11-19", 60_000m, 2), Assert.Single(daily));
        Assert.Equal(new RevenuePointDto("2026-11", 60_000m, 2), Assert.Single(monthly));
    }

    [Fact]
    public async Task Revenue_UsesConfiguredShiftBoundariesIncludingAfterMidnight()
    {
        repository.Orders =
        [
            new(new DateTime(2026, 11, 19, 2, 0, 0), 10_000m),
            new(new DateTime(2026, 11, 19, 7, 0, 0), 20_000m),
            new(new DateTime(2026, 11, 19, 13, 0, 0), 30_000m),
            new(new DateTime(2026, 11, 19, 19, 0, 0), 40_000m)
        ];

        var result = await CreateService().GetRevenueAsync(null, null, "shift", CancellationToken.None);

        Assert.Equal<RevenuePointDto>(
        [
            new("Sáng", 20_000m, 1),
            new("Chiều", 30_000m, 1),
            new("Tối", 50_000m, 2)
        ], result);
    }

    [Fact]
    public async Task Revenue_RejectsInvalidGroupAndReversedDateRange()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetRevenueAsync(null, null, "week", CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(
            () => service.GetRevenueAsync(new(2026, 11, 20), new(2026, 11, 19), "day", CancellationToken.None));
    }

    private ReportService CreateService() => new(repository, new ReportShiftOptions
    {
        MorningStart = new(7, 0),
        AfternoonStart = new(13, 0),
        EveningStart = new(19, 0)
    });

    private sealed class StubReportRepository : IReportRepository
    {
        public List<PaidOrderTotal> Orders { get; set; } = [];

        public Task<List<PaidOrderTotal>> GetPaidOrderTotalsAsync(
            DateTime? start, DateTime? endExclusive, CancellationToken ct) =>
            Task.FromResult(Orders
                .Where(order => (start is null || order.PaidAt >= start)
                    && (endExclusive is null || order.PaidAt < endExclusive))
                .ToList());

        public Task<List<TopProductDto>> GetTopProductsAsync(
            DateTime? start, DateTime? endExclusive, CancellationToken ct) => throw new NotSupportedException();

        public Task<List<InventoryReportDto>> GetInventoryAsync(
            DateTime? start, DateTime? endExclusive, CancellationToken ct) => throw new NotSupportedException();
    }
}
