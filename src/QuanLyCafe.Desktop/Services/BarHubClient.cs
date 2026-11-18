using Microsoft.AspNetCore.SignalR.Client;
using QuanLyCafe.Desktop.Models;

namespace QuanLyCafe.Desktop.Services;

public sealed class BarHubClient : IAsyncDisposable
{
    private readonly HubConnection _conn;

    public event Action<BarTicketDto>? TicketCreated;
    public event Action<ItemStatusChangedDto>? ItemStatusChanged;
    public event Action<bool>? ConnectionChanged;

    public BarHubClient(string baseUrl, SessionStore session)
    {
        _conn = new HubConnectionBuilder()
            .WithUrl(new Uri(new Uri(baseUrl), "hubs/bar"), o => o.AccessTokenProvider = () => Task.FromResult(session.Token))
            .WithAutomaticReconnect(new RetryForever())
            .Build();
        _conn.On<BarTicketDto>("TicketCreated", t => TicketCreated?.Invoke(t));
        _conn.On<ItemStatusChangedDto>("ItemStatusChanged", c => ItemStatusChanged?.Invoke(c));
        _conn.Reconnecting += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };
        _conn.Reconnected += _ => { ConnectionChanged?.Invoke(true); return Task.CompletedTask; };
        _conn.Closed += _ => { ConnectionChanged?.Invoke(false); return Task.CompletedTask; };
    }

    public bool IsConnected => _conn.State == HubConnectionState.Connected;

    public Task StartAsync() => _conn.State == HubConnectionState.Disconnected ? _conn.StartAsync() : Task.CompletedTask;

    public ValueTask DisposeAsync() => _conn.DisposeAsync();

    private sealed class RetryForever : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext ctx) =>
            TimeSpan.FromSeconds(ctx.PreviousRetryCount < 3 ? 2 : 10);
    }
}
