using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

internal static class ApiResponseErrors
{
    public static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
            if (problem.ValueKind == JsonValueKind.Object && problem.TryGetProperty("title", out var title)
                && title.ValueKind == JsonValueKind.String)
            {
                var detail = problem.TryGetProperty("detail", out var value) && value.ValueKind == JsonValueKind.String
                    ? value.GetString() : null;
                throw new HttpRequestException(string.IsNullOrWhiteSpace(detail) ? title.GetString()
                    : $"{title.GetString()} {detail}", null, response.StatusCode);
            }
        }
        catch (JsonException) { }
        response.EnsureSuccessStatusCode();
    }
}
