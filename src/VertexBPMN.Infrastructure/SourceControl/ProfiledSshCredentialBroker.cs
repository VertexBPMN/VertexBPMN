using System.Text.Json;
using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class ProfiledSshCredentialBroker(PersistentSourceControlStore store, ICredentialService credentials,
	ISourceControlActorResolver actors, IOptions<SourceControlOptions> options, IOptionsMonitor<SourceControlOptions>? monitor = null)
{
	internal async Task<GitSshCredentialLease> IssueAsync(SourceControlContext context, ApprovedGitSshTarget approved, CancellationToken cancellationToken)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			await DemandCurrentAsync(context, approved, deadline.Token);
			var reference = approved.Target.Profile.GitCredentialReference!;
			var before = await MetadataAsync(context, reference, deadline.Token);
			var username = await credentials.ResolveSecretAsync(context.TenantId, reference, "username", deadline.Token);
			await DemandCurrentAsync(context, approved, deadline.Token);
			var key = await credentials.ResolveSecretAsync(context.TenantId, reference, "privateKey", deadline.Token);
			string? passphrase = null;
			if (before.SecretKeys.Contains("passphrase", StringComparer.Ordinal))
			{
				await DemandCurrentAsync(context, approved, deadline.Token);
				passphrase = await credentials.ResolveSecretAsync(context.TenantId, reference, "passphrase", deadline.Token);
				if (string.IsNullOrEmpty(passphrase))
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
				}
			}
			var after = await MetadataAsync(context, reference, deadline.Token);
			if (before.LastModified != after.LastModified || !before.SecretKeys.SequenceEqual(after.SecretKeys, StringComparer.Ordinal))
			{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
			await DemandCurrentAsync(context, approved, deadline.Token);
			return new(approved.Target.Remote, username ?? "", key ?? "", DateTimeOffset.UtcNow.AddMinutes(2), passphrase);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut); }
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
	}

	internal async Task DemandCurrentAsync(SourceControlContext context, ApprovedGitSshTarget approved, CancellationToken cancellationToken)
	{
		var target = approved.Target;
		var kind = target.RequiredCapabilities switch
		{
			SourceControlCapability.Read => SourceControlOperationKind.Read,
			SourceControlCapability.Commit => SourceControlOperationKind.Commit,
			SourceControlCapability.Push => SourceControlOperationKind.Push,
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput)
		};
		var roles = await actors.ResolveAsync(context, cancellationToken);
		var current = await store.CaptureConnectionTargetAsync(context, target.RepositoryId, roles, kind, cancellationToken);
		if (current != target || current.Remote.AbsoluteUri != target.Remote.AbsoluteUri || current.Profile.Transport != GitTransportKind.Ssh)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict); }
		var configuration = monitor?.CurrentValue ?? options.Value;
		if (!configuration.Enabled)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.Disabled); }
		var network = configuration.SshNetworkPolicies.SingleOrDefault(policy => policy.TenantId == context.TenantId && policy.Id == approved.Network.Id);
		var trust = configuration.SshTrustProfiles.SingleOrDefault(profile => profile.TenantId == context.TenantId && profile.Id == approved.Trust.Id);
		if (network is null || trust is null || network.IsRevoked || trust.IsRevoked
			|| JsonSerializer.Serialize(GitSshConfigurationPolicy.Snapshot(network)) != JsonSerializer.Serialize(approved.Network)
			|| JsonSerializer.Serialize(GitSshConfigurationPolicy.Snapshot(trust)) != JsonSerializer.Serialize(approved.Trust))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict); }
	}

	private async Task<CredentialMetadata> MetadataAsync(SourceControlContext context, string reference, CancellationToken cancellationToken)
	{
		var metadata = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
		if (metadata is null || metadata.Id != reference || metadata.TenantId != context.TenantId || metadata.Type != "GitSshKey"
			|| !metadata.SecretKeys.Contains("username", StringComparer.Ordinal) || !metadata.SecretKeys.Contains("privateKey", StringComparer.Ordinal))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		return metadata;
	}
}
