using Microsoft.AspNetCore.SignalR;
using QuanLyCafe.Application.Common;
using QuanLyCafe.Application.Ordering;

namespace QuanLyCafe.Api.Hubs;
public class BarHub : Hub
{
    public const string Path = "/hubs/bar";

    public override async Task OnConnectedAsync()
    {
        if (Context.User?.FindFirst("role")?.Value is { } role)
            await Groups.AddToGroupAsync(Context.ConnectionId, role);
        await base.OnConnectedAsync();
    }
}

public class BarHubNotifier(IHubContext<BarHub> hub) : IRealtimeNotifier
{
    public Task TicketCreatedAsync(BarTicketDto ticket, CancellationToken ct = default) =>
        hub.Clients.Group("Barista").SendAsync("TicketCreated", ticket, ct);

    public Task ItemStatusChangedAsync(ItemStatusChangedDto change, CancellationToken ct = default) =>
        hub.Clients.Groups("Waiter", "Cashier", "Barista").SendAsync("ItemStatusChanged", change, ct);

    public Task ItemUnavailableAsync(ItemUnavailableDto item, CancellationToken ct = default) =>
        hub.Clients.Groups("Waiter", "Cashier").SendAsync("ItemUnavailable", item, ct);

    public Task LowStockAlertAsync(LowStockAlertDto alert, CancellationToken ct = default) =>
        hub.Clients.Group("Manager").SendAsync("LowStockAlert", alert, ct);
}
