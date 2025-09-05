namespace VertexBPMN.Core.Domain;

public record JsonRpcError(int Code, string Message, object Data);