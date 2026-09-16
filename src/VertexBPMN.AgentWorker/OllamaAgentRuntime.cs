using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace VertexBPMN.AgentWorker;

/// <summary>Ollama adapter for the documented non-streaming POST /api/chat contract.</summary>
public sealed class OllamaAgentRuntime(
    HttpClient client,
    IOptions<ContractReviewerOptions> options) : IAgentRuntime
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async ValueTask<AgentRuntimeResponse> CompleteAsync(
        AgentRuntimeRequest request, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var payload = new OllamaChatRequest(
            settings.Model,
            request.Messages.Select(MapMessage).ToArray(),
            request.Tools.Count == 0 ? null : request.Tools.Select(MapTool).ToArray(),
            request.OutputSchema,
            new OllamaOptions(0, request.MaximumOutputTokens),
            false,
            false,
            "0");
        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload, JsonOptions);
        if (payloadBytes.Length > settings.MaximumDocumentBytes * 2 + 32_768)
            throw new AgentRuntimeException("budget_exhausted", "Local model request exceeded its byte budget.");
        var endpoint = new Uri(settings.Endpoint, UriKind.Absolute);
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(endpoint, "api/chat"));
        message.Content = new ByteArrayContent(payloadBytes);
        message.Content.Headers.ContentType = new("application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.MaximumRuntimeSeconds));
        try
        {
            using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new AgentRuntimeException("provider_rate_limited", "Local model runtime rate limited the request.");
            if (!response.IsSuccessStatusCode)
                throw new AgentRuntimeException("provider_unavailable", "Local model runtime rejected the request.");
            if (response.Content.Headers.ContentLength > settings.MaximumResponseBytes)
                throw new AgentRuntimeException("budget_exhausted", "Local model response exceeded its byte budget.");
            await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var body = new MemoryStream();
            var buffer = new byte[8_192];
            while (true)
            {
                var read = await source.ReadAsync(buffer, timeout.Token);
                if (read == 0) break;
                if (body.Length + read > settings.MaximumResponseBytes)
                    throw new AgentRuntimeException("budget_exhausted", "Local model response exceeded its byte budget.");
                body.Write(buffer, 0, read);
            }
            body.Position = 0;
            var result = await JsonSerializer.DeserializeAsync<OllamaChatResponse>(body, JsonOptions, timeout.Token)
                ?? throw new AgentRuntimeException("provider_unavailable", "Local model response was empty.");
            if (!result.Done || result.Message is null)
                throw new AgentRuntimeException("provider_unavailable", "Local model response was incomplete.");
            var calls = result.Message.ToolCalls?.Select(call => new AgentToolCall(
                call.Function.Name, call.Function.Arguments.Clone())).ToArray() ?? [];
            return new AgentRuntimeResponse(result.Message.Content ?? string.Empty, calls,
                Math.Max(0, result.PromptEvalCount), Math.Max(0, result.EvalCount), result.DoneReason);
        }
        catch (AgentRuntimeException) { throw; }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AgentRuntimeException("provider_unavailable", "Local model runtime timed out.");
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or NotSupportedException)
        {
            throw new AgentRuntimeException("provider_unavailable", "Local model runtime is unavailable or returned invalid JSON.", exception);
        }
    }

    private static OllamaMessage MapMessage(AgentChatMessage message) => new(
        message.Role, message.Content, message.ToolName,
        message.ToolCalls?.Select(call => new OllamaToolCall(new OllamaFunctionCall(call.Name, call.Arguments))).ToArray());

    private static OllamaTool MapTool(AgentToolDefinition tool) =>
        new("function", new OllamaFunctionDefinition(tool.Name, tool.Description, tool.Parameters));

    private sealed record OllamaChatRequest(
        string Model,
        OllamaMessage[] Messages,
        OllamaTool[]? Tools,
        JsonElement? Format,
        OllamaOptions Options,
        bool Think,
        bool Stream,
        [property: JsonPropertyName("keep_alive")] string KeepAlive);
    private sealed record OllamaOptions(double Temperature, [property: JsonPropertyName("num_predict")] int NumPredict);
    private sealed record OllamaMessage(
        string Role,
        string Content,
        [property: JsonPropertyName("tool_name")] string? ToolName = null,
        [property: JsonPropertyName("tool_calls")] OllamaToolCall[]? ToolCalls = null);
    private sealed record OllamaTool(string Type, OllamaFunctionDefinition Function);
    private sealed record OllamaFunctionDefinition(string Name, string Description, JsonElement Parameters);
    private sealed record OllamaToolCall(OllamaFunctionCall Function);
    private sealed record OllamaFunctionCall(string Name, JsonElement Arguments);
    private sealed record OllamaChatResponse(
        OllamaMessage? Message,
        bool Done,
        [property: JsonPropertyName("done_reason")] string? DoneReason,
        [property: JsonPropertyName("prompt_eval_count")] int PromptEvalCount,
        [property: JsonPropertyName("eval_count")] int EvalCount);
}
