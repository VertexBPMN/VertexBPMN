using System.Globalization;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Hosting uses its own explicit credential reference and current PR authorization.</summary>
internal sealed partial class ProfiledHostingCredentialBroker(PersistentSourceControlStore store, ICredentialService credentials,
	ISourceControlActorResolver actors, IOptions<SourceControlOptions> options, IAzureDevOpsTokenIssuer? entraTokens = null)
{
	internal async Task<GitHubTokenLease> IssueGitHubAsync(SourceControlContext context, AcceptedHostingExecution execution,
		ApprovedGitHttpsTarget approved, CancellationToken cancellationToken)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			await DemandAsync(context, execution, deadline.Token);
			var hosting = execution.Target.Profile.Hosting!;
			if (execution.HostingTarget.ProviderId != "github" || approved.Remote.AbsoluteUri != execution.HostingTarget.ApiRepositoryUri.AbsoluteUri
				|| approved.Profile != HostingHttpsContext.ApiProfile(execution.Target.Profile)
				|| approved.AdministrativePolicy?.Matches(execution.ApiPolicy) != true)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			var reference = hosting.CredentialReference;
			var metadata = await MetadataAsync(context, reference, deadline.Token);
			var expiry = await credentials.ResolveSecretAsync(context.TenantId, reference, "expiresAt", deadline.Token);
			if (expiry is null || expiry.Length > 64 || !DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture,
				DateTimeStyles.None, out var expiresAt) || expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
			}
			await DemandAsync(context, execution, deadline.Token);
			var token = await credentials.ResolveSecretAsync(context.TenantId, reference, "token", deadline.Token);
			var current = await MetadataAsync(context, reference, deadline.Token);
			if (string.IsNullOrWhiteSpace(token) || token.Length > 16 * 1024
				|| token.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
				|| current.LastModified != metadata.LastModified || !current.SecretKeys.SequenceEqual(metadata.SecretKeys, StringComparer.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
			}
			await DemandAsync(context, execution, deadline.Token);
			var limit = DateTimeOffset.UtcNow.AddMinutes(2);
			return new GitHubTokenLease(token, expiresAt < limit ? expiresAt : limit);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
	}

	private async Task<CredentialMetadata> MetadataAsync(SourceControlContext context, string reference, CancellationToken cancellationToken)
	{
		var metadata = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
		if (metadata is null || metadata.Id != reference || metadata.TenantId != context.TenantId || metadata.Type != "HostingHttpBearer"
			|| !metadata.SecretKeys.Contains("token", StringComparer.Ordinal) || !metadata.SecretKeys.Contains("expiresAt", StringComparer.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		return metadata;
	}

	private async Task DemandAsync(SourceControlContext context, AcceptedHostingExecution execution, CancellationToken cancellationToken) =>
		await store.DemandCurrentHostingAsync(context, execution, await actors.ResolveAsync(context, cancellationToken), cancellationToken);
}
