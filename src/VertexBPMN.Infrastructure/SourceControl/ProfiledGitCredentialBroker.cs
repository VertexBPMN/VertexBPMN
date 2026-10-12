using Microsoft.Extensions.Options;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Fresh profile credentials only after network approval and current actor authorization.</summary>
internal sealed partial class ProfiledGitCredentialBroker(PersistentSourceControlStore store, ICredentialService credentials,
	ISourceControlActorResolver actors, IOptions<SourceControlOptions> options, IAzureDevOpsTokenIssuer? entraTokens = null)
{
	internal async Task<IGitCredentialLease?> IssueAsync(SourceControlContext context, SourceControlExecutionTarget target,
		ApprovedGitHttpsTarget approved, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(approved);
		if (!options.Value.Enabled)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Disabled);
		}
		var kind = target.RequiredCapabilities switch
		{
			SourceControlCapability.Read => SourceControlOperationKind.Read,
			SourceControlCapability.Commit => SourceControlOperationKind.Commit,
			SourceControlCapability.Push => SourceControlOperationKind.Push,
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable)
		};
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			await DemandCurrentAsync(context, target, kind, deadline.Token);
			if (target.Profile.Transport != GitTransportKind.Https || target.Remote.AbsoluteUri != approved.Remote.AbsoluteUri
				|| target.RequiredCapabilities != approved.RequiredCapabilities
				|| target.Profile != approved.Profile
				|| target.Profile.GitEndpoint.BaseUri.AbsoluteUri != approved.Profile.GitEndpoint.BaseUri.AbsoluteUri
				|| target.Profile.Hosting?.ApiEndpoint.BaseUri.AbsoluteUri != approved.Profile.Hosting?.ApiEndpoint.BaseUri.AbsoluteUri)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
			}
			if (target.Profile.Authentication == GitAuthenticationKind.Anonymous)
			{
				return null;
			}
			if (target.Profile.Authentication == GitAuthenticationKind.HttpBearer)
			{
				return await IssueBearerAsync(context, target, kind, deadline.Token);
			}
			if (target.Profile.Authentication != GitAuthenticationKind.HttpBasic)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
			}
			return await IssueBasicAsync(context, target, kind, deadline.Token);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut);
		}
	}

	private async Task<IGitCredentialLease> IssueBasicAsync(SourceControlContext context, SourceControlExecutionTarget target,
		SourceControlOperationKind kind, CancellationToken cancellationToken)
	{
		var reference = target.Profile.GitCredentialReference!;
		try
		{
			var metadata = await ReadMetadataAsync(context, reference, cancellationToken);
			var username = await credentials.ResolveSecretAsync(context.TenantId, reference, "username", cancellationToken);
			await DemandCurrentAsync(context, target, kind, cancellationToken);
			var password = await credentials.ResolveSecretAsync(context.TenantId, reference, "password", cancellationToken);
			var current = await ReadMetadataAsync(context, reference, cancellationToken);
			if (username is null || password is null || metadata.LastModified != current.LastModified
				|| !metadata.SecretKeys.SequenceEqual(current.SecretKeys, StringComparer.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
			}
			await DemandCurrentAsync(context, target, kind, cancellationToken);
			return new GitHttpBasicCredentialLease(target.Remote, username, password, DateTimeOffset.UtcNow.AddMinutes(2));
		}
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
	}

	private async Task<CredentialMetadata> ReadMetadataAsync(SourceControlContext context, string reference,
		CancellationToken cancellationToken)
	{
		var metadata = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
		if (metadata is null || metadata.Id != reference || metadata.TenantId != context.TenantId
			|| metadata.Type != "GitHttpBasic" || !metadata.SecretKeys.Contains("username", StringComparer.Ordinal)
			|| !metadata.SecretKeys.Contains("password", StringComparer.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		return metadata;
	}

	private async Task DemandCurrentAsync(SourceControlContext context, SourceControlExecutionTarget target,
		SourceControlOperationKind kind, CancellationToken cancellationToken)
	{
		var roles = await actors.ResolveAsync(context, cancellationToken);
		await store.DemandConnectionTargetAsync(context, target, roles, kind, cancellationToken);
	}
}
