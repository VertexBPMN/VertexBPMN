using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using VertexBPMN.Core;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Bpmn;
using System.Text.Json;
using System.Net.WebSockets;
using System.Threading.Tasks;
using System.Text;

namespace VertexBPMN.McpAdapter
{
public partial class Program
{
    public static void Main(string[] args)
    {
    // ...existing code...
        var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, lc) => lc.WriteTo.Console());
    builder.Services.AddVertexTelemetry();
    builder.Services.AddSingleton<IDistributedTokenEngine, DistributedTokenEngine>();
    builder.Services.AddSingleton<IProcessEngine, DistributedTokenEngineAdapter>();
    builder.Services.AddSingleton<IBpmnParser, BpmnParser>();
    builder.Services.AddSingleton<IBpmnEngine, BpmnEngine>();
    builder.Services.AddSingleton<McpServer>();
        var app = builder.Build();

        app.UseWebSockets();

        app.Map("/mcp/ws", async (HttpContext ctx, McpServer mcp, IBpmnEngine engine) =>
        {
            // Test-Bypass: Authentifizierung nur, wenn TEST_NO_AUTH nicht gesetzt ist
            var noAuth = Environment.GetEnvironmentVariable("TEST_NO_AUTH") == "1";
            if (!noAuth)
            {
                const string JwtIssuer = "https://your-oidc-provider"; // TODO: Konfiguration
                const string JwtAudience = "vertexbpmn-api";
                const string JwtSigningKey = "dev-signing-key-please-change";
                var authHeader = ctx.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer "))
                {
                    ctx.Response.StatusCode = 401;
                    await ctx.Response.WriteAsync("Missing or invalid Authorization header");
                    return;
                }
                var token = authHeader.Substring("Bearer ".Length).Trim();
                var principal = JwtHelper.ValidateJwt(token, JwtIssuer, JwtAudience, JwtSigningKey);
                if (principal == null)
                {
                    ctx.Response.StatusCode = 401;
                    await ctx.Response.WriteAsync("Invalid or expired JWT token");
                    return;
                }
            }
            if (ctx.WebSockets.IsWebSocketRequest)
            {
                using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
                var buffer = new byte[4096];
                string? subscribedInstanceId = null;
                ChannelReader<JsonElement>? eventStream = null;
                var receiveTask = Task.Run(async () =>
                {
                    while (ws.State == WebSocketState.Open)
                    {
                        var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
                        var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        await mcp.HandleJsonRpcAsync(json,
                            async resp =>
                            {
                                var respJson = JsonSerializer.Serialize(resp);
                                var respBytes = Encoding.UTF8.GetBytes(respJson);
                                await ws.SendAsync(respBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                                // Wenn der Client sich für einen Event-Stream subscribed hat, merken
                                if (json.Contains("bpmn.instanceEvent"))
                                {
                                    try
                                    {
                                        var doc = JsonDocument.Parse(json);
                                        var root = doc.RootElement;
                                        var @params = root.TryGetProperty("params", out var p) ? p : default;
                                        var instId = @params.GetProperty("instanceId").GetString();
                                        subscribedInstanceId = instId;
                                        eventStream = mcp.GetInstanceEventStream(instId!);
                                    }
                                    catch { }
                                }
                            });
                    }
                });

                // Event-Stream-Task: Sende alle Events aus dem Channel an den Client
                var eventTask = Task.Run(async () =>
                {
                    while (ws.State == WebSocketState.Open)
                    {
                        if (eventStream is not null)
                        {
                            while (await eventStream!.WaitToReadAsync())
                            {
                                while (eventStream!.TryRead(out var evt))
                                {
                                    // Alle Event-Typen werden als JSON übertragen
                                    var evtJson = JsonSerializer.Serialize(evt);
                                    var evtBytes = Encoding.UTF8.GetBytes(evtJson);
                                    await ws.SendAsync(evtBytes, WebSocketMessageType.Text, true, CancellationToken.None);
                                }
                            }
                        }
                        await Task.Delay(100); // Polling-Intervall
                    }
                });

                await Task.WhenAny(receiveTask, eventTask);
            }
            else
            {
                ctx.Response.StatusCode = 400;
            }
        });

        app.MapPost("/mcp/jsonrpc", async (HttpContext ctx, McpServer mcp) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var json = await reader.ReadToEndAsync();
            await mcp.HandleJsonRpcAsync(json,
                async resp =>
                {
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.WriteAsync(JsonSerializer.Serialize(resp));
                });
        });

        app.Run();
    }
}

namespace VertexBPMN.McpAdapter
{
    public partial class Program { }
}
}
