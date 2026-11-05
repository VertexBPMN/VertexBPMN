using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Portal.Bff;

/// <summary>
/// Production <see cref="IBffRuntimeGateway"/> that forwards server-resolved BFF
/// calls to the Vertex REST API at <c>Portal:ApiBaseUrl</c>, presenting the user's
/// server-held OIDC access token. It performs no authorization decisions of its own;
/// the controllers resolve the actor/tenant/released revision before calling here.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1515", Justification = "Public gateway implementation registered directly in the portal host.")]
public sealed class VertexBffRuntimeGateway : IBffRuntimeGateway
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly OidcSessionTokenStore _tokenStore;
    private readonly string _apiBaseUrl;

    public VertexBffRuntimeGateway(IHttpClientFactory httpClientFactory, IHttpContextAccessor httpContextAccessor, OidcSessionTokenStore tokenStore, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _httpContextAccessor = httpContextAccessor;
        _tokenStore = tokenStore;
        _apiBaseUrl = configuration["Portal:ApiBaseUrl"] ?? string.Empty;
    }

    public async Task<ReleasedApplication?> GetReleasedAsync(string tenantId, string applicationId, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/applications/{Uri.EscapeDataString(applicationId)}/released-revision?tenantId={Uri.EscapeDataString(tenantId)}", null);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        var key = root.TryGetProperty("applicationDefinitionId", out var appId) && appId.ValueKind == JsonValueKind.String
            ? appId.GetString()!
            : applicationId;
        var version = root.TryGetProperty("version", out var versionElement) && versionElement.TryGetInt32(out var v) ? v : 0;
        var content = root.TryGetProperty("contentJson", out var contentElement) && contentElement.ValueKind == JsonValueKind.String
            ? contentElement.GetString()!
            : "{}";
        return new ReleasedApplication(key, version, content);
    }

    public async Task<DataSourceResult> ExecuteDataSourceReadAsync(string tenantId, string applicationId, string bindingName, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/applications/{Uri.EscapeDataString(applicationId)}/released/data-sources/{Uri.EscapeDataString(bindingName)}?tenantId={Uri.EscapeDataString(tenantId)}", null);
        using var response = await SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        return new DataSourceResult(
            bindingName,
            ReadString(root, "status") ?? "success",
            ReadInt(root, "rowCount") ?? 0,
            DateTimeOffset.UtcNow);
    }

    public async Task StartProcessAsync(string tenantId, string processDefinitionId, IDictionary<string, object?>? variables, string idempotencyKey, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { processDefinitionKey = processDefinitionId, variables });
        using var request = CreateRequest(HttpMethod.Post, "/api/runtime/start", payload, idempotencyKey);
        using var response = await SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task ClaimAsync(string tenantId, string applicationId, Guid taskId, string idempotencyKey, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Post, $"/api/task/inbox/{taskId}/claim", null, idempotencyKey);
        using var response = await SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteAsync(string tenantId, string applicationId, Guid taskId, string idempotencyKey, IReadOnlyDictionary<string, object?>? variables, CancellationToken ct = default)
    {
        var payload = JsonSerializer.Serialize(new { variables });
        using var request = CreateRequest(HttpMethod.Post, $"/api/task/inbox/{taskId}/complete", payload, idempotencyKey);
        using var response = await SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<BffTaskState> GetTaskStateAsync(string tenantId, string applicationId, Guid taskId, CancellationToken ct = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"/api/task/inbox/{taskId}", null);
        using var response = await SendAsync(request, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException("The task is not visible to the current user.");
        }

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = document.RootElement;
        var created = TryDateTime(root, "created");
        var completed = TryDateTime(root, "completed");
        return new BffTaskState(taskId, ReadString(root, "status"), null, created, completed);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url, string? body, string? idempotencyKey = null)
    {
        var request = new HttpRequestMessage(method, _apiBaseUrl.TrimEnd('/') + url);
        var accessToken = ResolveAccessToken();
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        if (idempotencyKey is not null)
        {
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        }

        if (body is not null)
        {
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        return request;
    }

    private Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        => _httpClientFactory.CreateClient("portal-api").SendAsync(request, ct);

    private string? ResolveAccessToken()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user is null ? null : _tokenStore.GetAccessTokenAsync(user, null, CancellationToken.None).GetAwaiter().GetResult();
    }

    private static string? ReadString(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String ? element.GetString() : null;

    private static int? ReadInt(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.TryGetInt32(out var value) ? value : null;

    private static DateTimeOffset? TryDateTime(JsonElement root, string property)
        => root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value) ? value : null;
}
