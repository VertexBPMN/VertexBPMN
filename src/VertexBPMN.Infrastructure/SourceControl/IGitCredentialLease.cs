namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Operation-scoped credential IPC; Bearer requires explicit helper capability negotiation.</summary>
internal interface IGitCredentialLease : IDisposable
{
	Task WriteCredentialAsync(Uri remote, StreamWriter writer, CancellationToken cancellationToken);
}
