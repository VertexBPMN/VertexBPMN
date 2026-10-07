namespace VertexBPMN.Domain.Entities;

public record Error(string Id, ErrorType Type, string Description);
