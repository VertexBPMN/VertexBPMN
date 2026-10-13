using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	internal async Task<SourceControlReadResult> ReadSshResultDocumentAsync(SourceControlContext context, Guid id,
		IReadOnlyCollection<string> currentRoles, GitSshPolicyRegistry policies, CancellationToken cancellationToken)
	{
		var roles = currentRoles.ToArray();
		var work = await ReadAcceptedSshReadAsync(context, id, roles, policies, cancellationToken);
		var kind = work.Request.Query?.Kind ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		object result;
		if (kind == GitReadQueryKind.File)
		{
			var snapshot = await ReadSshResultAsync<ModelSnapshot>(context, id, roles, policies, cancellationToken);
			result = new
			{
				snapshot.Path,
				Generation = snapshot.DocumentGeneration,
				snapshot.LocalRevision,
				Bytes = snapshot.CopyContent(),
				snapshot.ContentSha256
			};
		}
		else
		{
			result = kind switch
			{
				GitReadQueryKind.ListFiles => await ReadSshResultAsync<SourceControlPage<RepositoryFile>>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.History => await ReadSshResultAsync<SourceControlPage<CommitSummary>>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.Diff => await ReadSshResultAsync<ModelDiff>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.Branches => await ReadSshResultAsync<SourceControlPage<string>>(context, id, roles, policies, cancellationToken),
				GitReadQueryKind.ResolveBranch => await ReadSshResultAsync<RevisionSelection>(context, id, roles, policies, cancellationToken),
				_ => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe)
			};
		}
		return new(id, kind.ToString(), JsonSerializer.SerializeToElement(result, result.GetType()));
	}
}
