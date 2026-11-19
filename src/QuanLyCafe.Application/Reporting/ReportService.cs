using System.Globalization;
using QuanLyCafe.Domain.Common;

namespace QuanLyCafe.Application.Reporting;

public sealed class ReportService(IReportRepository repository, ReportShiftOptions shifts)
{
    private static readonly string[] ShiftLabels = ["Sáng", "Chiều", "Tối"];

    public async Task<List<RevenuePointDto>> GetRevenueAsync(
        DateOnly? from, DateOnly? to, string groupBy, CancellationToken ct)
    {
        var (start, endExclusive) = GetDateRange(from, to);
        groupBy = groupBy.Trim().ToLowerInvariant();
        if (groupBy is not ("day" or "month" or "shift"))
        {
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Kiểu nhóm doanh thu phải là day, month hoặc shift.");
        }

        var orders = await repository.GetPaidOrderTotalsAsync(start, endExclusive, ct);
        if (groupBy == "shift")
        {
            return orders
                .GroupBy(order => shifts.GetShiftIndex(TimeOnly.FromDateTime(order.PaidAt)))
                .OrderBy(group => group.Key)
                .Select(group => new RevenuePointDto(
                    ShiftLabels[group.Key], group.Sum(order => order.TotalAmount), group.Count()))
                .ToList();
        }

        var format = groupBy == "day" ? "yyyy-MM-dd" : "yyyy-MM";
        return orders
            .GroupBy(order => order.PaidAt.ToString(format, CultureInfo.InvariantCulture))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new RevenuePointDto(
                group.Key, group.Sum(order => order.TotalAmount), group.Count()))
            .ToList();
    }

    public Task<List<TopProductDto>> GetTopProductsAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (start, endExclusive) = GetDateRange(from, to);
        return repository.GetTopProductsAsync(start, endExclusive, ct);
    }

    public Task<List<InventoryReportDto>> GetInventoryAsync(
        DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var (start, endExclusive) = GetDateRange(from, to);
        return repository.GetInventoryAsync(start, endExclusive, ct);
    }

    private static (DateTime? Start, DateTime? EndExclusive) GetDateRange(DateOnly? from, DateOnly? to)
    {
        if (from > to)
        {
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        }

        var start = from?.ToDateTime(TimeOnly.MinValue);
        var endExclusive = to is { } lastDay && lastDay < DateOnly.MaxValue
            ? lastDay.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : (DateTime?)null;

        return (start, endExclusive);
    }
}
