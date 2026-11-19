using QuanLyCafe.Application.Ordering;

namespace QuanLyCafe.Application.Common;
public interface IRealtimeNotifier
{
    Task TicketCreatedAsync(BarTicketDto ticket, CancellationToken ct = default);

    Task ItemStatusChangedAsync(ItemStatusChangedDto change, CancellationToken ct = default);

    Task ItemUnavailableAsync(ItemUnavailableDto item, CancellationToken ct = default);

    Task LowStockAlertAsync(LowStockAlertDto alert, CancellationToken ct = default);
}

public record LowStockAlertDto(Guid MaterialId, string MaterialName, decimal StockQuantity);
