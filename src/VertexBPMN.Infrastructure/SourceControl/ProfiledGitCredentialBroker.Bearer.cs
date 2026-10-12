using System.Globalization;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitCredentialBroker
{
	private async Task<IGitCredentialLease> IssueBearerAsync(SourceControlContext context, SourceControlExecutionTarget target,
		SourceControlOperationKind kind, CancellationToken cancellationToken)
	{
		var reference = target.Profile.GitCredentialReference!;
		try
		{
			var candidate = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
			if (candidate is { Type: "GitAzureDevOpsEntra" } && candidate.Id == reference && candidate.TenantId == context.TenantId)
			{
				return await IssueEntraAsync(context, target, kind, candidate, cancellationToken);
			}
			var metadata = await ReadBearerMetadataAsync(context, reference, cancellationToken);
			var expiry = await credentials.ResolveSecretAsync(context.TenantId, reference, "expiresAt", cancellationToken);
			await DemandCurrentAsync(context, target, kind, cancellationToken);
			if (expiry is null || expiry.Length > 64
				|| !DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresAt)
				|| expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
			}
			var token = await credentials.ResolveSecretAsync(context.TenantId, reference, "token", cancellationToken);
			var current = await ReadBearerMetadataAsync(context, reference, cancellationToken);
			if (token is null || metadata.LastModified != current.LastModified
				|| !metadata.SecretKeys.SequenceEqual(current.SecretKeys, StringComparer.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
			}
			await DemandCurrentAsync(context, target, kind, cancellationToken);
			var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
			return new GitHttpBearerCredentialLease(target.Remote, token, expiresAt < deadline ? expiresAt : deadline);
		}
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
	}

	private async Task<CredentialMetadata> ReadBearerMetadataAsync(SourceControlContext context, string reference, CancellationToken cancellationToken)
	{
		var metadata = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
		if (metadata is null || metadata.Id != reference || metadata.TenantId != context.TenantId || metadata.Type != "GitHttpBearer"
			|| !metadata.SecretKeys.Contains("token", StringComparer.Ordinal) || !metadata.SecretKeys.Contains("expiresAt", StringComparer.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		return metadata;
	}
}
