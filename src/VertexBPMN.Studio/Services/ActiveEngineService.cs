using System.ComponentModel;
using System.Net;

namespace VertexBPMN.Studio.Services;

internal sealed class ActiveEngineService(IHttpClientFactory? httpClientFactory = null) : INotifyPropertyChanged, IDisposable
{
	private int _checking;
	private bool _disposed;
	public string ConnectionStatus { get; private set => SetProperty(ref field, value); } = "Not checked";
	public bool IsChecking { get; private set; }

	public event PropertyChangedEventHandler? PropertyChanged;
	public event EventHandler? OnChange;
	public event EventHandler<EngineChangedEventArgs>? OnEngineChanged;
	public string ActiveEngineId
	{
		get;
		set
		{
			if (SetProperty(ref field, value))
			{
				OnEngineChanged?.Invoke(this, new EngineChangedEventArgs(value));
			}
		}
	} = "engine1";

	public bool IsConnected { get; private set => SetProperty(ref field, value); }
	public DateTime LastConnectionCheck { get; private set => SetProperty(ref field, value); }

	public async Task CheckConnectionAsync(CancellationToken cancellationToken = default)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		cancellationToken.ThrowIfCancellationRequested();
		if (Interlocked.CompareExchange(ref _checking, 1, 0) != 0)
		{
			return;
		}

		try
		{
			IsChecking = true;
			OnChange?.Invoke(this, EventArgs.Empty);
			if (httpClientFactory is null)
			{
				IsConnected = false;
				ConnectionStatus = "Disconnected";
				return;
			}

			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(TimeSpan.FromSeconds(5));
			using var client = httpClientFactory.CreateClient("VertexBPMN.Api");
			using var response = await client.GetAsync("/api/ready", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
			IsConnected = response.IsSuccessStatusCode;
			ConnectionStatus = response.StatusCode switch
			{
				HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Unauthorized",
				_ when IsConnected => "Connected",
				_ => "Not ready"
			};
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			IsConnected = false;
			ConnectionStatus = "Timed out";
		}
		catch (HttpRequestException)
		{
			IsConnected = false;
			ConnectionStatus = "Disconnected";
		}
		catch (OidcSessionExpiredException)
		{
			IsConnected = false;
			ConnectionStatus = "Unauthorized";
		}
		finally
		{
			LastConnectionCheck = DateTime.UtcNow;
			IsChecking = false;
			Volatile.Write(ref _checking, 0);
			OnChange?.Invoke(this, EventArgs.Empty);
		}
	}

	private bool SetProperty<T>(ref T field, T value, [System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
	{
		if (EqualityComparer<T>.Default.Equals(field, value))
		{
			return false;
		}

		field = value;
		PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		OnChange?.Invoke(this, EventArgs.Empty);
		return true;
	}

	public void Dispose()
	{
		_disposed = true;
		OnChange = null;
		OnEngineChanged = null;
		PropertyChanged = null;
	}
}
