using Microsoft.AspNetCore.SignalR.Client;
using QuanLyCafe.Desktop.Models;

namespace QuanLyCafe.Desktop.Services;

// Giữ kết nối SignalR tới BarHub. Sự kiện được gọi trên luồng nền → form phải BeginInvoke.
public sealed class BarHubClient : IAsyncDisposable
{
    private readonly HubConnection _conn;

    public event Action<BarTicketDto>? TicketCreated;

    public BarHubClient(string baseUrl, SessionStore session)
    {
        _conn = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(baseUrl), "hubs/bar"), o => o.AccessTokenProvider = () => Task.FromResult(session.Token))
            // shortcut: thử lại mặc định 0/2/10/30 giây rồi dừng; ngày 9 thêm thử lại mãi + GET /api/bar/tickets sau khi nối lại.
            .WithAutomaticReconnect()
            .Build();
        _conn.On<BarTicketDto>("TicketCreated", t => TicketCreated?.Invoke(t));
    }

    public Task StartAsync() => _conn.StartAsync();

    public ValueTask DisposeAsync() => _conn.DisposeAsync();
}
