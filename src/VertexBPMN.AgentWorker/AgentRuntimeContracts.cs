using System.Text.Json;

namespace VertexBPMN.AgentWorker;

public sealed record AgentToolDefinition(string Name, string Description, JsonElement Parameters);

public sealed record AgentToolCall(string Name, JsonElement Arguments);

public sealed record AgentChatMessage(
    string Role,
    string Content,
    string? ToolName = null,
    IReadOnlyList<AgentToolCall>? ToolCalls = null);

public sealed record AgentRuntimeRequest(
    IReadOnlyList<AgentChatMessage> Messages,
    IReadOnlyList<AgentToolDefinition> Tools,
    JsonElement? OutputSchema,
    int MaximumOutputTokens);

public sealed record AgentRuntimeResponse(
    string Content,
    IReadOnlyList<AgentToolCall> ToolCalls,
    int PromptTokens,
    int OutputTokens,
    string? FinishReason);

/// <summary>
/// Provider-neutral, bounded model runtime. It has no file, shell, network-tool or process API.
/// </summary>
public interface IAgentRuntime
{
    ValueTask<AgentRuntimeResponse> CompleteAsync(
        AgentRuntimeRequest request,
        CancellationToken cancellationToken);
}

public sealed class AgentRuntimeException(string code, string message, Exception? inner = null)
    : InvalidOperationException(message, inner)
{
    public string Code { get; } = code;
}
