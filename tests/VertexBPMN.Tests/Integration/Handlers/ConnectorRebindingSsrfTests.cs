using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Tests.Integration.Handlers;

/// <summary>
/// M5: DNS rebinding / TOCTOU — the connector SocketsHttpHandler must connect to a
/// validated (non-private/loopback) address via ConnectCallback, so a post-check DNS
/// rebind can never reach an internal address. The shared handler resolves + validates
/// at connect time and connects to that same validated IP.
/// </summary>
public sealed class ConnectorRebindingSsrfTests
{
	[Fact]
	public void RegisteredConnectorHandler_HasRebindingSafeConnectCallback()
	{
		var services = new ServiceCollection();
		services.AddServiceTaskHandlers(null);
		using var provider = services.BuildServiceProvider();

		var handler = provider.GetRequiredService<SocketsHttpHandler>();

		Assert.False(handler.AllowAutoRedirect);
		Assert.NotNull(handler.ConnectCallback);
	}

	[Fact]
	public async Task ConnectorHandler_RejectsLoopbackAtConnect_WithoutReachingTarget()
	{
		var services = new ServiceCollection();
		services.AddServiceTaskHandlers(null);
		await using var provider = services.BuildServiceProvider();

		var handler = provider.GetRequiredService<SocketsHttpHandler>();
		using var client = new HttpClient(handler);

		using var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		var hits = 0;
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		var acceptTask = ObserveConnectionAsync();
		try
		{
			// A rejected address must never reach the listening socket.
			var exception = await Record.ExceptionAsync(() =>
				client.GetAsync($"http://127.0.0.1:{port}/", TestContext.Current.CancellationToken));
			Assert.NotNull(exception);
			Assert.True(exception is HttpRequestException
							|| exception.InnerException is HttpRequestException,
						$"Expected an HttpRequestException, got {exception.GetType().Name}.");
			await Task.Delay(150, TestContext.Current.CancellationToken);
		}
		finally
		{
			await cancellation.CancelAsync();
			await acceptTask;
		}
		Assert.Equal(0, Volatile.Read(ref hits));

		async Task ObserveConnectionAsync()
		{
			try
			{
				using var accepted = await listener.AcceptTcpClientAsync(cancellation.Token);
				Interlocked.Increment(ref hits);
			}
			catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
			{
				// A rejected connection leaves the accept pending until cleanup cancels it.
			}
		}
	}
}
