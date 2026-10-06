using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace VertexBPMN.McpClient;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "MA0049",
    Justification = "Existing public package namespace and type contract; renaming breaks consumer code unrelated to analyzer migration.")]
public class McpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly bool _ownsHttpClient;
    private bool _disposed;

    public McpClient(string baseUrl, HttpClient? httpClient = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(baseUrl);
        _baseUrl = baseUrl.TrimEnd('/');
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    public McpClient(Uri baseUrl, HttpClient? httpClient = null)
        : this(baseUrl?.OriginalString ?? throw new ArgumentNullException(nameof(baseUrl)), httpClient) { }

    public async Task<JsonElement> CallJsonRpcAsync(string method, object? @params = null, string? authToken = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var req = new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid().ToString(),
            method,
            @params
        };
        using var msg = new HttpRequestMessage(HttpMethod.Post, _baseUrl + "/mcp/jsonrpc")
        {
            Content = new StringContent(JsonSerializer.Serialize(req), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrEmpty(authToken))
		{
			msg.Headers.Add("Authorization", $"Bearer {authToken}");
		}

		using var resp = await _httpClient.SendAsync(msg, cancellationToken);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    // WebSocket: Beispiel für Event-Stream
    public async Task ConnectWebSocketAsync(string instanceId, string? authToken = null, Action<JsonElement>? onEvent = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        using var ws = new ClientWebSocket();
        if (!string.IsNullOrEmpty(authToken))
		{
			ws.Options.SetRequestHeader("Authorization", $"Bearer {authToken}");
		}

        var uri = new Uri(_baseUrl.Replace("http", "ws", StringComparison.Ordinal) + "/mcp/ws");
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
            using var document = JsonDocument.Parse(respJson);
            var evt = document.RootElement.Clone();
            onEvent?.Invoke(evt);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }
        if (disposing && _ownsHttpClient)
        {
            _httpClient.Dispose();
        }
        _disposed = true;
    }
}
