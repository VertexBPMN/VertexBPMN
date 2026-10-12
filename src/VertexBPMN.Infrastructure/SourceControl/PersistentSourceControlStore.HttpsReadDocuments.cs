using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task<SourceControlReadResult> ReadHttpsResultDocumentAsync(SourceControlContext context, Guid id,
		IReadOnlyCollection<string> currentRoles, GitHttpsPolicyRegistry policies, CancellationToken cancellationToken)
	{
		var roles = currentRoles.ToArray();
		var work = await ReadHttpsRevisionReadWorkAsync(context, id, roles, policies, cancellationToken);
		var kind = work.Query?.Kind ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		object result;
		if (kind == GitReadQueryKind.File)
		{
			var snapshot = await ReadHttpsResultAsync<ModelSnapshot>(context, id, roles, policies, cancellationToken);
			result = new { snapshot.Path, Generation = snapshot.DocumentGeneration, snapshot.LocalRevision,
				Bytes = snapshot.CopyContent(), snapshot.ContentSha256 };
		}
		else
		{
			result = kind switch
			{
				GitReadQueryKind.ListFiles => await ReadHttpsResultAsync<SourceControlPage<RepositoryFile>>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.History => await ReadHttpsResultAsync<SourceControlPage<CommitSummary>>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.Diff => await ReadHttpsResultAsync<ModelDiff>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.Branches => await ReadHttpsResultAsync<SourceControlPage<string>>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.ResolveBranch => await ReadHttpsResultAsync<RevisionSelection>(context, id, roles, policies, cancellationToken),
				_ => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe)
			};
		}
		return new(id, kind.ToString(), JsonSerializer.SerializeToElement(result, result.GetType()));
	}
}
