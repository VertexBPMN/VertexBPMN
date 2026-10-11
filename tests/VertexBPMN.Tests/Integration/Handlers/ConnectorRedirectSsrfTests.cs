using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Application.Extensions;

namespace VertexBPMN.Tests.Integration.Handlers;

public sealed class ConnectorRedirectSsrfTests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RegisteredClient_DoesNotFollowRedirectToPrivateAddress(bool oauthClient)
	{
		using var target = new TcpListener(IPAddress.Loopback, 0);
		target.Start();
		var port = ((IPEndPoint)target.LocalEndpoint).Port;
		var network = new LocalWireNetwork(port, [IPAddress.Parse("93.184.216.34")]);
		await using var provider = Provider(network);
		var handler = provider.GetRequiredService<SocketsHttpHandler>();
		Assert.False(handler.AllowAutoRedirect);
		Assert.False(handler.UseProxy);
		Assert.False(handler.UseCookies);
		await WithResponseAsync(target, $"HTTP/1.1 302 Found\r\nLocation: http://127.0.0.1:{port}/internal\r\nContent-Length: 0\r\nConnection: close\r\n\r\n", async () =>
		{
			using var client = Client(provider, oauthClient);
			using var response = await client.GetAsync("http://public.example/redirect", TestContext.Current.CancellationToken);
			Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
		});
		Assert.Single(network.Connected);
		Assert.False(target.Pending());
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task RegisteredClient_PublicConnectionSucceeds_ReboundPrivateAddressNeverConnects(bool oauthClient)
	{
		using var target = new TcpListener(IPAddress.Loopback, 0);
		target.Start();
		var address = IPAddress.Parse("93.184.216.34");
		var network = new LocalWireNetwork(((IPEndPoint)target.LocalEndpoint).Port,
			[address], [IPAddress.Loopback]);
		await using var provider = Provider(network);
		using var client = Client(provider, oauthClient);
		await WithResponseAsync(target, "HTTP/1.1 200 OK\r\nContent-Length: 2\r\nConnection: close\r\n\r\nOK", async () =>
		{
			using var first = await client.GetAsync("http://public.example/allowed", TestContext.Current.CancellationToken);
			Assert.Equal("OK", await first.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
		});
		await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync(
			"http://public.example/rebound", TestContext.Current.CancellationToken));
		Assert.Equal(2, network.Resolutions);
		Assert.Equal(address, Assert.Single(network.Connected).Address);
		Assert.False(target.Pending());
	}

	[Theory]
	[InlineData("::ffff:127.0.0.1")]
	[InlineData("::ffff:10.0.0.1")]
	[InlineData("fc00::1")]
	[InlineData("fd00::1")]
	[InlineData("fe80::1")]
	public async Task RegisteredClient_PrivateIpv6NeverReachesSocket(string address)
	{
		var network = new LocalWireNetwork(1, [IPAddress.Parse(address)]);
		await using var provider = Provider(network);
		await Assert.ThrowsAsync<HttpRequestException>(() => provider.GetRequiredService<HttpClient>()
			.GetAsync("http://public.example/", TestContext.Current.CancellationToken));
		Assert.Empty(network.Connected);
	}

	private static ServiceProvider Provider(IConnectorNetworkTransport network)
	{
		var services = new ServiceCollection();
		services.AddSingleton(network);
		services.AddServiceTaskHandlers();
		return services.BuildServiceProvider();
	}

	private static HttpClient Client(IServiceProvider provider, bool oauth) => oauth
		? provider.GetRequiredService<IHttpClientFactory>().CreateClient("VertexBPMN.PublicEndpoints")
		: provider.GetRequiredService<HttpClient>();

	private static async Task WithResponseAsync(TcpListener listener, string response, Func<Task> request)
	{
		using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
		var serving = RespondAsync(listener, response, cancellation.Token);
		try
		{
			await request();
			await serving;
		}
		finally
		{
			await cancellation.CancelAsync();
			try
			{
				await serving;
			}
			catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
			{
				// The request may fail before opening the socket; join the cancelled responder.
			}
		}
	}

	private static async Task RespondAsync(TcpListener listener, string response, CancellationToken cancellationToken)
	{
		using var accepted = await listener.AcceptTcpClientAsync(cancellationToken);
		await using var stream = accepted.GetStream();
		using var reader = new StreamReader(stream, leaveOpen: true);
		while (await reader.ReadLineAsync(cancellationToken) is { Length: > 0 })
		{
			// Consume request headers until the blank line before writing the response.
		}
		await stream.WriteAsync(Encoding.ASCII.GetBytes(response), cancellationToken);
	}

	// Controlled DNS and routing only. Actual HTTP travels over a real local socket;
	// the production handler still validates every resolved IP before connecting.
	private sealed class LocalWireNetwork(int localPort, params IPAddress[][] answers) : IConnectorNetworkTransport
	{
		public int Resolutions { get; private set; }
		public List<IPEndPoint> Connected { get; } = [];
		public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
		{
			Assert.Equal("public.example", host);
			return Task.FromResult(answers[Math.Min(Resolutions++, answers.Length - 1)]);
		}
		public ValueTask<Stream> ConnectAsync(IPEndPoint endpoint, CancellationToken cancellationToken)
		{
			Connected.Add(endpoint);
			return new ConnectorNetworkTransport().ConnectAsync(new IPEndPoint(IPAddress.Loopback, localPort), cancellationToken);
		}
	}
}
