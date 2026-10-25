using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VertexBPMN.Portal.Client.Bff;

/// <summary>Provides the antiforgery request token needed by the BFF's state-changing endpoints.</summary>
public interface IBffAntiforgeryTokenProvider
{
    Task<string?> GetTokenAsync(CancellationToken ct = default);
}

/// <summary>
/// Default token provider: fetches the antiforgery request token from the BFF's
/// <c>api/bff/antiforgery/token</c> endpoint once and caches it for the session.
/// </summary>
public sealed class BffAntiforgeryTokenProvider(HttpClient http) : IBffAntiforgeryTokenProvider
{
    private string? _token;

    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (_token is null)
        {
            using var response = await http.GetAsync("api/bff/antiforgery/token", ct).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct).ConfigureAwait(false);
                _token = json.TryGetProperty("requestToken", out var token) ? token.GetString() : null;
            }
        }

        return _token;
    }
}
