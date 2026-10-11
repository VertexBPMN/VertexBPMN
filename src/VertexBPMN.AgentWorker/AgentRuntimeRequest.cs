using System.Text.Json;

namespace VertexBPMN.AgentWorker;

internal sealed record AgentRuntimeRequest(
    IReadOnlyList<AgentChatMessage> Messages,
    IReadOnlyList<AgentToolDefinition> Tools,
    JsonElement? OutputSchema,
    int MaximumOutputTokens);
