using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Entities;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

public sealed partial class PersistentSourceControlStore
{
	/// <summary>Internal exporter: reads immutable evidence without requiring a now-revoked user's live authorization.</summary>
	internal AuditLog ReadCompletedAudit(SourceControlOperationRecord row)
	{
		var context = new SourceControlContext(row.TenantId, row.ActorId);
		if (!row.AuditPending || row.ProtectedResult is null || row.Id == Guid.Empty)
		{ UnsafeAudit(); }
		try
		{
			var result = Unprotect(context, row.RepositoryId, row.ProtectedResult);
			var evidence = AuditEvidence(row, result);
			return new()
			{
				SourceControlEventId = row.Id,
				Timestamp = new(row.UpdatedUtcTicks, TimeSpan.Zero),
				Action = "SourceControl." + ((SourceControlOperationKind)row.Kind).ToString(),
				Resource = "Repository",
				ResourceId = row.RepositoryId.ToString("D"),
				TenantId = row.TenantId,
				UserId = row.ActorId,
				CorrelationId = row.Id.ToString("D"),
				StatusCode = 200,
				DetailsJson = JsonSerializer.Serialize(evidence)
			};
		}
		catch (Exception error) when (error is JsonException or ArgumentException or InvalidOperationException or NullReferenceException)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
	}

	private CompletedAuditEvidence AuditEvidence(SourceControlOperationRecord row, byte[] result)
	{
		string commit;
		Guid? sourcePush = null;
		var provider = "git";
		SourceControlExecutionTarget? target = null;
		if (row.Kind == (int)SourceControlOperationKind.PullRequest && row.State == (int)SourceControlOperationState.Succeeded)
		{
			var saved = JsonSerializer.Deserialize<StoredPullRequest>(result);
			if (saved?.Receipt is null || saved.RepositoryId != row.RepositoryId || saved.Receipt.OperationId != row.Id)
			{ UnsafeAudit(); }
			var hosting = row.ExecutionContractVersion == AcceptedHostingExecution.ContractVersion ? ReadHistoricalHostingExecution(new(row.TenantId, row.ActorId), row) : null;
			RepositoryHostingReceiptPolicy.Demand(hosting?.HostingTarget ?? LegacyAuditTarget(row, saved), saved.Command, saved.Receipt);
			target = hosting?.Target;
			provider = saved.Receipt.ProviderId;
			commit = saved.Receipt.HeadCommit.Value;
			var accepted = JsonSerializer.Deserialize<AcceptedPullRequest>(Unprotect(new(row.TenantId, row.ActorId), row.RepositoryId, row.ProtectedRequest));
			if (accepted is null || accepted.PushOperationId == Guid.Empty)
			{ UnsafeAudit(); }
			sourcePush = accepted.PushOperationId;
		}
		else if (row.Kind == (int)SourceControlOperationKind.Push && row.State == (int)SourceControlOperationState.Pushed)
		{
			var saved = JsonSerializer.Deserialize<StoredRemotePush>(result);
			if (saved?.Receipt is null || saved.SchemaVersion != 1 || saved.RepositoryId != row.RepositoryId || saved.Receipt.OperationId != row.Id)
			{ UnsafeAudit(); }
			commit = saved.Receipt.Commit.Value;
		}
		else if (row.Kind == (int)SourceControlOperationKind.Commit && row.State == (int)SourceControlOperationState.CommittedLocal)
		{
			var saved = JsonSerializer.Deserialize<StoredLocalCommit>(result);
			if (saved?.Receipt is null || saved.SchemaVersion != 1 || saved.RepositoryId != row.RepositoryId || saved.Receipt.OperationId != row.Id)
			{ UnsafeAudit(); }
			commit = saved.Receipt.Commit.Value;
		}
		else
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
		if (row.ExecutionContractVersion == AcceptedGitHttpsExecution.ContractVersion)
		{ target = new GitHttpsExecutionProtection(protection).Unprotect(row).Target; }
		return new(row.RepositoryId, row.Fence, commit, sourcePush, provider, target?.BindingRevision,
			target?.Profile.Id, target?.Profile.Revision, "VertexBPMN <source-control@vertexbpmn.invalid>",
			target?.Profile.GitCredentialReference, target?.Profile.Hosting?.CredentialReference);
	}

	private sealed record CompletedAuditEvidence(Guid RepositoryId, long Fence, string Commit, Guid? PushOperationId,
		string ProviderId, long? BindingRevision, Guid? ProfileId, long? ProfileRevision, string GitAuthor,
		string? GitCredentialReference, string? HostingIdentityReference);

	private static RepositoryHostingTarget LegacyAuditTarget(SourceControlOperationRecord row, StoredPullRequest saved)
	{
		if (row.ExecutionContractVersion != 0 || saved.SchemaVersion != 1 || saved.HostingTarget is not null
			|| saved.Receipt?.Url.IsAbsoluteUri != true || saved.Receipt.ProviderId != "github")
		{ UnsafeAudit(); }
		var segments = saved.Receipt.Url.AbsolutePath.Split('/');
		if (segments.Length != 5 || segments[3] != "pull")
		{ UnsafeAudit(); }
		var binding = new RepositoryBinding(row.RepositoryId, row.TenantId,
			new UriBuilder(saved.Receipt.Url) { Path = "/" + segments[1] + "/" + segments[2] }.Uri, null, "main", "release", ["models"]);
		return RepositoryHostingTargetPolicy.LegacyGitHub(binding);
	}

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void UnsafeAudit() => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
}
