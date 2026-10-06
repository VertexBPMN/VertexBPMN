namespace VertexBPMN.Domain.Interfaces;

public record AuthorizationInfo(string Id, string UserId, string GroupId, string Resource, string Permissions);
