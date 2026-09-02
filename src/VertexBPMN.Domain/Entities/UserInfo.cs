namespace VertexBPMN.Domain.Entities;

public record UserInfo(string Id, string Username, string? Email = null, string? FirstName = null, string? LastName = null);
