using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace VertexBPMN.Studio.Services;

public sealed class NotificationClient : IAsyncDisposable
{
    private readonly NavigationManager _nav;
    private HubConnection? _connection;
    public event Action<UserNotificationDto>? OnNotification;

    public NotificationClient(NavigationManager nav)
    {
        _nav = nav;
    }

    public async Task StartAsync(string userId, CancellationToken ct = default)
    {
        if (_connection is not null) return;

        _connection = new HubConnectionBuilder()
            .WithUrl(_nav.ToAbsoluteUri("/processmonitoringhub"))
            .WithAutomaticReconnect()
            .Build();

        _connection.On<object>("UserNotification", payload =>
        {
            // Lightweight mapping
            var dto = System.Text.Json.JsonSerializer.Deserialize<UserNotificationDto>(
                System.Text.Json.JsonSerializer.Serialize(payload))!;
            OnNotification?.Invoke(dto);
        });

        await _connection.StartAsync(ct);
        await _connection.InvokeAsync("JoinUserChannel", userId, ct);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_connection is null) return;
        await _connection.StopAsync(ct);
        await _connection.DisposeAsync();
        _connection = null;
    }

    public ValueTask DisposeAsync() => _connection is null ? ValueTask.CompletedTask : _connection.DisposeAsync();
}

public sealed record UserNotificationDto(
    string Recipient,
    string Message,
    string Category,
    DateTime Timestamp);