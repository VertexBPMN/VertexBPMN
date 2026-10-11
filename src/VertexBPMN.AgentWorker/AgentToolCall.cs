using System.Text.Json;

namespace VertexBPMN.AgentWorker;

internal sealed record AgentToolCall(string Name, JsonElement Arguments);
