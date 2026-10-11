using System.Text.Json;

namespace VertexBPMN.AgentWorker;

internal sealed record AgentRuntimeResponse(
    string Content,
    IReadOnlyList<AgentToolCall> ToolCalls,
    int PromptTokens,
    int OutputTokens,
    string? FinishReason);
