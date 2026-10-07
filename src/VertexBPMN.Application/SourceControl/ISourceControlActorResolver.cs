using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Trusted authority adapter; resolves current roles and rejects disabled or tenant-moved actors.</summary>
public interface ISourceControlActorResolver
{
	Task<IReadOnlyCollection<string>> ResolveAsync(SourceControlContext context, CancellationToken cancellationToken);
}
