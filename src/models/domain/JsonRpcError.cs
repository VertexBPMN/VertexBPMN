namespace VertexBPMN.Domain;

public record JsonRpcError(int Code, string Message, object Data);