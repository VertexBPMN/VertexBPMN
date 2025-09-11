namespace VertexBPMN.Core.Domain;

public record JsonRpcResponse(string Jsonrpc, object Result, JsonRpcError Error);