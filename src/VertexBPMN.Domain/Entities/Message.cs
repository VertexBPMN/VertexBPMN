namespace VertexBPMN.Domain.Entities;

public record Message(string Name, string Payload,  Dictionary<string, object> Variables);