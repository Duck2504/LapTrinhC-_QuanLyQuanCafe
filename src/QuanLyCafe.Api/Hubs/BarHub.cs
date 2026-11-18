using Microsoft.AspNetCore.SignalR;
using QuanLyCafe.Application.Common;
using QuanLyCafe.Application.Ordering;

namespace QuanLyCafe.Api.Hubs;

// Mục 4.3.6: khi kết nối, đưa người dùng vào nhóm theo claim role. Client chỉ nghe, không gọi method nào lên hub.
// Cần đăng nhập nhờ fallback policy; token WebSocket đi qua query access_token (xem OnMessageReceived ở Program.cs).
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
}
