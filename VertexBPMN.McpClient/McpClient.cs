using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Mcp;

public class McpClient
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public McpClient(string baseUrl, HttpClient? httpClient = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<JsonElement> CallJsonRpcAsync(string method, object? @params = null, string? authToken = null)
    {
        var req = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method,
            @params
        };
        var msg = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/mcp/jsonrpc")
        {
            Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(authToken))
            msg.Headers.Add("Authorization", $"Bearer {authToken}");
        var resp = await _httpClient.SendAsync(msg);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }

    // WebSocket: Beispiel für Event-Stream
    public async Task ConnectWebSocketAsync(string instanceId, string? authToken = null, Action<JsonElement>? onEvent = null, CancellationToken cancellationToken = default)
    {
        using var ws = new ClientWebSocket();
        if (!string.IsNullOrEmpty(authToken))
            ws.Options.SetRequestHeader("Authorization", $"Bearer {authToken}");
        var uri = new Uri(_baseUrl.Replace("http", "ws") + "/mcp/ws");
        await ws.ConnectAsync(uri, cancellationToken);
        // Subscribe
        var req = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method = "bpmn.instanceEvent",
            @params = new { instanceId }
        };
        var reqJson = JsonSerializer.Serialize(req);
        var reqBytes = Encoding.UTF8.GetBytes(reqJson);
        await ws.SendAsync(new ArraySegment<byte>(reqBytes), WebSocketMessageType.Text, true, cancellationToken);
        var buffer = new byte[4096];
        while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            var respJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
            var evt = JsonDocument.Parse(respJson).RootElement;
            onEvent?.Invoke(evt);
        }
    }
}
