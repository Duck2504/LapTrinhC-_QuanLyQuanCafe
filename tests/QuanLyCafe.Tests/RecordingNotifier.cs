using QuanLyCafe.Application.Common;
using QuanLyCafe.Application.Ordering;

namespace QuanLyCafe.Tests;

// Ghi lại cảnh báo tồn kho thấp; các sự kiện khác bỏ qua.
internal sealed class RecordingNotifier : IRealtimeNotifier
{
    public List<LowStockAlertDto> LowStock { get; } = [];

    public Task LowStockAlertAsync(LowStockAlertDto alert, CancellationToken ct = default)
    {
        LowStock.Add(alert);
        return Task.CompletedTask;
    }

    public Task TicketCreatedAsync(BarTicketDto ticket, CancellationToken ct = default) => Task.CompletedTask;
    public Task ItemStatusChangedAsync(ItemStatusChangedDto change, CancellationToken ct = default) => Task.CompletedTask;
    public Task ItemUnavailableAsync(ItemUnavailableDto item, CancellationToken ct = default) => Task.CompletedTask;
}
