using QuanLyCafe.Application.Ordering;
using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Application.Common;
public interface IRealtimeNotifier
{
    Task TicketCreatedAsync(BarTicketDto ticket, CancellationToken ct = default);

    Task ItemStatusChangedAsync(ItemStatusChangedDto change, CancellationToken ct = default);

    Task ItemUnavailableAsync(ItemUnavailableDto item, CancellationToken ct = default);

    Task LowStockAlertAsync(LowStockAlertDto alert, CancellationToken ct = default);
}

public record LowStockAlertDto(Guid MaterialId, string MaterialName, decimal StockQuantity);

public static class LowStockNotifierExtensions
{
    // Gọi SAU khi commit: báo Manager các nguyên liệu có tồn <= mức tối thiểu.
    public static async Task NotifyLowStockAsync(
        this IRealtimeNotifier notifier, IEnumerable<Material> materials, CancellationToken ct = default)
    {
        foreach (var m in materials.Where(m => m.StockQuantity <= m.MinQuantity))
            await notifier.LowStockAlertAsync(new(m.MaterialID, m.MaterialName, m.StockQuantity), ct);
    }
}
