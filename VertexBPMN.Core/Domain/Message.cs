namespace VertexBPMN.Core.Domain;

public record Message(string Name, string Payload,  Dictionary<string, object> Variables);