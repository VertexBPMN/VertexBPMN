using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledGitCredentialBroker
{
	private async Task<IGitCredentialLease> IssueEntraAsync(SourceControlContext context, SourceControlExecutionTarget target,
		SourceControlOperationKind kind, CredentialMetadata metadata, CancellationToken cancellationToken)
	{
		// Azure DevOps Server and foreign endpoints must never receive this cloud audience.
		if (target.Remote.Scheme != Uri.UriSchemeHttps || target.Remote.IdnHost != "dev.azure.com"
			|| !target.Remote.IsDefaultPort || metadata.Type != "GitAzureDevOpsEntra"
			|| !metadata.SecretKeys.Contains("tenantId", StringComparer.Ordinal)
			|| !metadata.SecretKeys.Contains("clientId", StringComparer.Ordinal)
			|| !metadata.SecretKeys.Contains("clientSecret", StringComparer.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		var reference = target.Profile.GitCredentialReference!;
		var tenant = await credentials.ResolveSecretAsync(context.TenantId, reference, "tenantId", cancellationToken);
		var client = await credentials.ResolveSecretAsync(context.TenantId, reference, "clientId", cancellationToken);
		if (!Guid.TryParseExact(tenant, "D", out var tenantId) || tenantId == Guid.Empty
			|| !Guid.TryParseExact(client, "D", out var clientId) || clientId == Guid.Empty)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		await DemandCurrentAsync(context, target, kind, cancellationToken);
		var secret = await credentials.ResolveSecretAsync(context.TenantId, reference, "clientSecret", cancellationToken);
		if (string.IsNullOrWhiteSpace(secret) || secret.Length > 16_384 || secret.Any(char.IsControl))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		await DemandEntraMetadataAsync(context, reference, metadata, cancellationToken);
		await DemandCurrentAsync(context, target, kind, cancellationToken);
		var token = await (entraTokens ?? new AzureDevOpsTokenIssuer()).IssueAsync(tenantId, clientId, secret, cancellationToken);
		await DemandEntraMetadataAsync(context, reference, metadata, cancellationToken);
		await DemandCurrentAsync(context, target, kind, cancellationToken);
		var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
		return new GitHttpBearerCredentialLease(target.Remote, token.Token, token.ExpiresOn < deadline ? token.ExpiresOn : deadline);
	}

	private async Task DemandEntraMetadataAsync(SourceControlContext context, string reference, CredentialMetadata expected,
		CancellationToken cancellationToken)
	{
		var current = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
		if (current is null || current.Id != reference || current.TenantId != context.TenantId
			|| current.Type != expected.Type || current.LastModified != expected.LastModified
			|| !current.SecretKeys.SequenceEqual(expected.SecretKeys, StringComparer.Ordinal))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
	}
}
