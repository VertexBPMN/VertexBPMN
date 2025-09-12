namespace VertexBPMN.Domain;

public record JsonRpcResponse(string Jsonrpc, object Result, JsonRpcError Error);