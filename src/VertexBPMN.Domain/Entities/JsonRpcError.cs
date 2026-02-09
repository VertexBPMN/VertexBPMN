namespace VertexBPMN.Domain.Entities;

public record JsonRpcError(int Code, string Message, object Data);