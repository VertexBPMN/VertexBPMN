namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed record GitWorkspace(string Directory, string ControlDirectory, Guid OperationId, long Fence);
