using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace VertexBPMN.AgentWorker;

public sealed class OAuthClientCredentialsTokenProvider(
    IHttpClientFactory clients,
    IOptions<ExternalTaskWorkerOptions> options,
    TimeProvider? clock = null) : IWorkerAccessTokenProvider
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _token;
    private DateTimeOffset _refreshAt;

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        if (_token is not null && now < _refreshAt) return _token;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            now = (clock ?? TimeProvider.System).GetUtcNow();
            if (_token is not null && now < _refreshAt) return _token;
            var settings = options.Value;
            using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenEndpoint)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "client_credentials", ["client_id"] = settings.ClientId,
                    ["client_secret"] = settings.ClientSecret, ["scope"] = settings.Scope
                })
            };
            using var response = await clients.CreateClient("VertexBPMN.WorkerToken")
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new ExternalTaskTransportException("Worker token endpoint rejected the request.");
            var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (!payload.TryGetProperty("access_token", out var tokenElement)
                || string.IsNullOrWhiteSpace(tokenElement.GetString()))
                throw new ExternalTaskTransportException("Worker token response is invalid.");
            var expiresIn = payload.TryGetProperty("expires_in", out var expiresElement)
                && expiresElement.TryGetInt32(out var seconds) ? seconds : 60;
            _token = tokenElement.GetString();
            _refreshAt = now.AddSeconds(Math.Max(1, expiresIn - 30));
            return _token!;
        }
        catch (ExternalTaskTransportException) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            throw new ExternalTaskTransportException("Worker token request failed.", inner: exception);
        }
        finally { _gate.Release(); }
    }
}
