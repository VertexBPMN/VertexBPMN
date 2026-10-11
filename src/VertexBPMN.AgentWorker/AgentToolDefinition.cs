using System.Text.Json;

namespace VertexBPMN.AgentWorker;

internal sealed record AgentToolDefinition(string Name, string Description, JsonElement Parameters);
