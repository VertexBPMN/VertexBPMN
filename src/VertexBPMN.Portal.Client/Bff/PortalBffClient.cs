using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Portal.Client.Bff;

/// <summary>
/// Thin HTTP client for the Portal BFF. All tenant/assignee/revision resolution happens
/// server-side; the client only drives the released-runtime endpoints. State-changing calls
/// send an antiforgery request token (X-CSRF-TOKEN) and a fresh Idempotency-Key, and server
/// problem-detail codes surface as <see cref="BffResult{T}.ErrorCode"/>.
/// </summary>
public sealed class PortalBffClient
{
    private readonly HttpClient _http;
    private readonly IBffAntiforgeryTokenProvider _antiforgery;
    private readonly Func<string> _idempotencyKeyFactory;

    public PortalBffClient(HttpClient http, IBffAntiforgeryTokenProvider antiforgery, Func<string>? idempotencyKeyFactory = null)
    {
        _http = http;
        _antiforgery = antiforgery;
        _idempotencyKeyFactory = idempotencyKeyFactory ?? (() => Guid.NewGuid().ToString("N"));
    }

    public Task<BffResult<ReleasedAppMeta>> GetReleasedAsync(string applicationId, CancellationToken ct = default)
        => GetJsonAsync<ReleasedAppMeta>($"api/bff/applications/{applicationId}/released", ct);

    public Task<BffResult<DataSourceReadResult>> ReadDataSourceAsync(string applicationId, string binding, CancellationToken ct = default)
        => GetJsonAsync<DataSourceReadResult>($"api/bff/applications/{applicationId}/data-sources/{binding}", ct);

    public Task<BffResult<ActionOutcome>> StartAsync(string applicationId, IDictionary<string, object?>? variables = null, CancellationToken ct = default)
        => PostJsonAsync<ActionOutcome>($"api/bff/applications/{applicationId}/actions/start", variables, ct);

    public Task<BffResult> ClaimAsync(string applicationId, Guid taskId, CancellationToken ct = default)
        => PostNoContentAsync($"api/bff/applications/{applicationId}/actions/claim/{taskId}", null, ct);

    public Task<BffResult> CompleteAsync(string applicationId, Guid taskId, IDictionary<string, object?>? variables = null, CancellationToken ct = default)
        => PostNoContentAsync($"api/bff/applications/{applicationId}/actions/complete/{taskId}", variables, ct);

    public Task<BffResult<TaskStatusView>> GetTaskStatusAsync(string applicationId, Guid taskId, CancellationToken ct = default)
        => GetJsonAsync<TaskStatusView>($"api/bff/applications/{applicationId}/tasks/{taskId}/status", ct);

    public Task<BffResult<TaskInboxPage>> GetInboxAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
        => GetJsonAsync<TaskInboxPage>($"api/task/inbox?page={page}&pageSize={pageSize}", ct);

    public Task<BffResult> ClaimTaskAsync(Guid taskId, CancellationToken ct = default)
        => PostNoContentAsync($"api/task/inbox/{taskId}/claim", null, ct);

    public Task<BffResult> CompleteTaskAsync(Guid taskId, IDictionary<string, object?>? variables = null, CancellationToken ct = default)
        => PostNoContentAsync($"api/task/inbox/{taskId}/complete", variables, ct);

    private async Task<BffResult<T>> GetJsonAsync<T>(string path, CancellationToken ct)
    {
        using var response = await _http.GetAsync(path, ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return await ToFailureAsync<T>(response, ct).ConfigureAwait(false);
        }

        var value = await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);
        return BffResult<T>.Success(value!);
    }

    private async Task<BffResult<T>> PostJsonAsync<T>(string path, IDictionary<string, object?>? variables, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        await ApplyStateChangingHeadersAsync(request, ct).ConfigureAwait(false);
        request.Content = JsonContent.Create(variables);
        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            var value = await response.Content.ReadFromJsonAsync<T>(ct).ConfigureAwait(false);
            return BffResult<T>.Success(value!);
        }

        return await ToFailureAsync<T>(response, ct).ConfigureAwait(false);
    }

    private async Task<BffResult> PostNoContentAsync(string path, IDictionary<string, object?>? variables, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        await ApplyStateChangingHeadersAsync(request, ct).ConfigureAwait(false);
        if (variables is not null)
        {
            request.Content = JsonContent.Create(variables);
        }

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        return response.IsSuccessStatusCode ? BffResult.Success() : await ToFailureAsync(response, ct).ConfigureAwait(false);
    }

    private async Task ApplyStateChangingHeadersAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var token = await _antiforgery.GetTokenAsync(ct).ConfigureAwait(false);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        }

        request.Headers.TryAddWithoutValidation("Idempotency-Key", _idempotencyKeyFactory());
    }

    private static async Task<BffResult<T>> ToFailureAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var (code, status, detail) = await ParseFailurePartsAsync(response, ct).ConfigureAwait(false);
        return BffResult<T>.Failure(code, status, detail);
    }

    private static async Task<BffResult> ToFailureAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var (code, status, detail) = await ParseFailurePartsAsync(response, ct).ConfigureAwait(false);
        return BffResult.Failure(code, status, detail);
    }

    private static async Task<(string Code, int Status, string? Detail)> ParseFailurePartsAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var status = (int)response.StatusCode;
        string? code = null;
        string? detail = null;
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
            if (json.TryGetProperty("code", out var codeElement) && codeElement.ValueKind == JsonValueKind.String)
            {
                code = codeElement.GetString();
            }

            if (json.TryGetProperty("detail", out var detailElement) && detailElement.ValueKind == JsonValueKind.String)
            {
                detail = detailElement.GetString();
            }
        }
        catch (JsonException)
        {
            // No structured problem-detail body; fall back to a derived code.
        }

        return (code ?? $"http_{status}", status, detail);
    }
}
