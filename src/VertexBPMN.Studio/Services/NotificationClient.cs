using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;

namespace VertexBPMN.Studio.Services;
internal sealed class NotificationClient : IAsyncDisposable
{
	private readonly IConfiguration _configuration;
	private HubConnection? _connection;
	public event EventHandler<NotificationReceivedEventArgs>? OnNotification;
	public NotificationClient(IConfiguration configuration)
	{
		_configuration = configuration;
	}

	public async Task StartAsync(string userId, CancellationToken ct = default)
	{
		if (_connection is not null)
		{
			return;
		}

		var apiBaseUrl = _configuration["ApiBaseUrl"] ?? throw new InvalidOperationException("ApiBaseUrl is not configured.");
		var hubUri = new Uri(new Uri(apiBaseUrl), "api/monitoring-hub");
		_connection = new HubConnectionBuilder().WithUrl(hubUri).WithAutomaticReconnect().Build();
		_connection.On<UserNotificationDto>("UserNotification", payload =>
		{
			OnNotification?.Invoke(this, new NotificationReceivedEventArgs(payload));
		});
		await _connection.StartAsync(ct);
		await _connection.InvokeAsync("JoinUserChannel", userId, ct);
	}

	public async Task StopAsync(CancellationToken ct = default)
	{
		if (_connection is null)
		{
			return;
		}

		await _connection.StopAsync(ct);
		await _connection.DisposeAsync();
		_connection = null;
	}

	public ValueTask DisposeAsync() => _connection is null ? ValueTask.CompletedTask : _connection.DisposeAsync();
}
