namespace VertexBPMN.Domain.Entities;

public record JsonRpcResponse(string Jsonrpc, object Result, JsonRpcError Error);