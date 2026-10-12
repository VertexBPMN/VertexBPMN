using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	public Task<SourceControlReadResult> GetProfiledReadResultAsync(SourceControlContext context, Guid id,
		IReadOnlyCollection<string> roles, IOptionsMonitor<SourceControlOptions> configuration, CancellationToken cancellationToken) =>
		ReadHttpsResultDocumentAsync(context, id, roles, new GitHttpsPolicyRegistry(configuration), cancellationToken);

	public async Task<Guid> EnqueueProfiledReadAsync(SourceControlContext context, Guid repositoryId,
		IReadOnlyCollection<string> roles, SourceControlIdempotencyKey key, SourceControlReadSubmission submission,
		IOptionsMonitor<SourceControlOptions> configuration, CancellationToken cancellationToken)
	{
		var snapshot = submission.Snapshot;
		var query = new AcceptedGitReadQuery(submission.Kind)
		{
			Path = submission.Path, PageSize = submission.PageSize, Cursor = submission.Cursor, Generation = submission.Generation,
			Snapshot = snapshot is null ? null : new(snapshot.Path, snapshot.Kind, snapshot.DocumentGeneration,
				snapshot.LocalRevision, snapshot.ContentSha256, snapshot.CopyContent())
		};
		var currentRoles = roles.ToArray();
		var target = await CaptureConnectionTargetAsync(context, repositoryId, currentRoles, SourceControlOperationKind.Read, cancellationToken);
		var access = await FindAsync(context.TenantId, repositoryId, cancellationToken)
			?? throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		query.Validate(access.Binding, submission.Revision, configuration.CurrentValue.Limits);
		return await EnqueueProfiledReadAsync(context, target, currentRoles, key,
			JsonSerializer.SerializeToUtf8Bytes(new AcceptedGitRevisionRead(submission.Revision) { Query = query }),
			new GitHttpsPolicyRegistry(configuration), cancellationToken);
	}
}
