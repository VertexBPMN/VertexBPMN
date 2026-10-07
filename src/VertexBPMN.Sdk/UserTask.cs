namespace VertexBPMN.Sdk;

public sealed record UserTask(
    Guid Id,
    string Name,
    string Assignee,
    DateTime Created,
    string? FormKey,
    string? FormSchema);
