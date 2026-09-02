using System.Net.Http.Json;
using System.Text.Json;

namespace VertexBPMN.Studio.Services;

public sealed class HttpMessageSignalService(IHttpClientFactory httpClientFactory) : IMessageSignalService
{
    public Task<JsonElement> CorrelateMessageAsync(
        string messageName,
        string? processInstanceId = null,
        string? variablesJson = null,
        CancellationToken cancellationToken = default) =>
        PostAsync(
            "/api/vertex/message",
            new
            {
                messageName,
                processInstanceId,
                variables = ParseVariables(variablesJson)
            },
            cancellationToken);

    public Task<JsonElement> BroadcastSignalAsync(
        string signalName,
        string? variablesJson = null,
        CancellationToken cancellationToken = default) =>
        PostAsync(
            "/api/vertex/signal",
            new
            {
                signalName,
                variables = ParseVariables(variablesJson)
            },
            cancellationToken);

    private async Task<JsonElement> PostAsync(string requestUri, object requestBody, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("VertexBPMN.Api");
        using var response = await client.PostAsJsonAsync(requestUri, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(content)
            ? JsonSerializer.SerializeToElement(new { success = true })
            : JsonSerializer.Deserialize<JsonElement>(content);
    }

    private static JsonElement ParseVariables(string? variablesJson)
    {
        if (string.IsNullOrWhiteSpace(variablesJson))
        {
            return JsonSerializer.SerializeToElement(new Dictionary<string, object?>());
        }

        using var document = JsonDocument.Parse(variablesJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException("Variables must be a JSON object.");
        }

        return document.RootElement.Clone();
    }
}
