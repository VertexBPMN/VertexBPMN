namespace VertexBPMN.Core.Domain;

public record Message(string Name, Dictionary<string, object> Variables);