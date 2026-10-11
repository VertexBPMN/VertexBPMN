using System.Text.Json;

namespace VertexBPMN.AgentWorker;

internal sealed record AgentChatMessage(
    string Role,
    string Content,
    string? ToolName = null,
    IReadOnlyList<AgentToolCall>? ToolCalls = null);
