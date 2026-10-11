using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Serilog;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.McpAdapter;

internal static class Program
{
	public static void Main(string[] args)
	{
		// ...existing code...
		var builder = WebApplication.CreateBuilder(args);
		builder.Host.UseSerilog((_, lc) => lc.WriteTo.Console());
		builder.Services.AddMcpAdapterTelemetry(builder.Configuration); // updated telemetry setup
		builder.Services.AddSingleton<IDistributedProcessEngine, DistributedProcessEngine>();
		builder.Services.AddSingleton<IBpmnParser, BpmnParser>();
		builder.Services.AddSingleton<IBpmnEngine, BpmnEngine>();
		builder.Services.AddSingleton<McpServer>();
		var app = builder.Build();

		app.UseWebSockets();

		app.Map("/mcp/ws", HandleWebSocketAsync);

		app.MapPost("/mcp/jsonrpc", async (HttpContext ctx, McpServer mcp) =>
		{
			using var reader = new StreamReader(ctx.Request.Body);
			var json = await reader.ReadToEndAsync(ctx.RequestAborted);
			await mcp.HandleJsonRpcAsync(json,
				async resp =>
				{
					ctx.Response.ContentType = "application/json";
					await ctx.Response.WriteAsync(JsonSerializer.Serialize(resp), ctx.RequestAborted);
				});
		});

		app.Run();
	}
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2025", Justification = "Both concurrent loops are explicitly awaited in nested finally blocks after cancellation, before the socket and send semaphore are disposed; the analyzer does not model this shutdown flow.")]
	private static async Task HandleWebSocketAsync(HttpContext ctx, McpServer mcp)
	{
		if (!await AuthorizeWebSocketAsync(ctx))
		{
			return;
		}
		if (ctx.WebSockets.IsWebSocketRequest)
		{
			using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
			ChannelReader<JsonElement>? eventStream = null;
			using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);
			using var sendGate = new SemaphoreSlim(1, 1);
			var receiveTask = ReceiveRequestsAsync(ws, mcp, stream => Volatile.Write(ref eventStream, stream), sendGate, lifetime.Token);
			var eventTask = StreamEventsAsync(ws, () => Volatile.Read(ref eventStream), sendGate, lifetime.Token);
			try
			{
				await await Task.WhenAny(receiveTask, eventTask);
			}
			finally
			{
				await lifetime.CancelAsync();
				try
				{
					await receiveTask;
				}
				catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
				{
					// Expected when the other loop completes or the request is aborted.
				}
				finally
				{
					try
					{
						await eventTask;
					}
					catch (OperationCanceledException) when (lifetime.IsCancellationRequested)
					{
						// Both loops are observed before disposing the socket or send gate.
					}
				}
			}
		}
		else
		{
			ctx.Response.StatusCode = 400;
		}
	}

	private static async Task<bool> AuthorizeWebSocketAsync(HttpContext ctx)
	{
		// Test-Bypass: Authentifizierung nur, wenn TEST_NO_AUTH nicht gesetzt ist
		var noAuth = Environment.GetEnvironmentVariable("TEST_NO_AUTH") == "1";
		if (!noAuth)
		{
			const string JwtIssuer = "https://your-oidc-provider"; // TODO: Konfiguration
			const string JwtAudience = "vertexbpmn-api";
			const string JwtSigningKey = "dev-signing-key-please-change";
			var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
			if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
			{
				ctx.Response.StatusCode = 401;
				await ctx.Response.WriteAsync("Missing or invalid Authorization header", ctx.RequestAborted);
				return false;
			}
			var token = authHeader.Substring("Bearer ".Length).Trim();
			var principal = JwtHelper.ValidateJwt(token, JwtIssuer, JwtAudience, JwtSigningKey);
			if (principal == null)
			{
				ctx.Response.StatusCode = 401;
				await ctx.Response.WriteAsync("Invalid or expired JWT token", ctx.RequestAborted);
				return false;
			}
		}
		return true;
	}

	private static async Task StreamEventsAsync(WebSocket ws, Func<ChannelReader<JsonElement>?> getEventStream, SemaphoreSlim sendGate, CancellationToken cancellationToken)
	{

		while (ws.State == WebSocketState.Open)
		{
			var eventStream = getEventStream();
			if (eventStream is not null)
			{
				while (await eventStream.WaitToReadAsync(cancellationToken))
				{
					while (eventStream.TryRead(out var evt))
					{
						// Alle Event-Typen werden als JSON übertragen
						var evtJson = JsonSerializer.Serialize(evt);
						var evtBytes = Encoding.UTF8.GetBytes(evtJson);
						await SendBytesAsync(ws, evtBytes, sendGate, cancellationToken);
					}
				}
			}
			await Task.Delay(100, cancellationToken); // Polling-Intervall
		}

	}



	private static async Task ReceiveRequestsAsync(WebSocket ws, McpServer mcp, Action<ChannelReader<JsonElement>?> updateSubscription, SemaphoreSlim sendGate, CancellationToken cancellationToken)
	{
		var buffer = new byte[4096];
		while (ws.State == WebSocketState.Open)
		{
			var result = await ws.ReceiveAsync(buffer, cancellationToken);
			if (result.MessageType == WebSocketMessageType.Close)
			{ return; }
			var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
			await mcp.HandleJsonRpcAsync(json,
				async resp =>
				{
					var respJson = JsonSerializer.Serialize(resp);
					var respBytes = Encoding.UTF8.GetBytes(respJson);
					await SendBytesAsync(ws, respBytes, sendGate, cancellationToken);
					// Wenn der Client sich für einen Event-Stream subscribed hat, merken
					if (json.Contains("bpmn.instanceEvent", StringComparison.Ordinal))
					{
						try
						{
							using var doc = JsonDocument.Parse(json);
							var root = doc.RootElement;
							var @params = root.TryGetProperty("params", out var p) ? p : default;
							var instId = @params.GetProperty("instanceId").GetString();
							updateSubscription(mcp.GetInstanceEventStream(instId!));
						}
						catch (JsonException) { /* Malformed subscription JSON is rejected by the RPC handler; keep the existing subscription. */ }
					}
				});
		}
	}

	private static async Task SendBytesAsync(WebSocket socket, byte[] bytes, SemaphoreSlim sendGate, CancellationToken cancellationToken)
	{
		await sendGate.WaitAsync(cancellationToken);
		try
		{ await socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken); }
		finally { sendGate.Release(); }
	}
}
