using System.Globalization;
using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ProfiledHostingCredentialBroker
{
	internal async Task<HostingHttpCredentialLease> IssueAzureAsync(SourceControlContext context, AcceptedHostingExecution execution,
		ApprovedGitHttpsTarget approved, CancellationToken cancellationToken)
	{
		using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		deadline.CancelAfter(options.Value.Limits.ReadTimeout);
		try
		{
			await DemandAsync(context, execution, deadline.Token);
			if (execution.HostingTarget.ProviderId != AzureDevOpsHostingTargetPolicy.ProviderId
				|| approved.Remote.AbsoluteUri != execution.HostingTarget.ApiRepositoryUri.AbsoluteUri
				|| approved.Remote.IdnHost != "dev.azure.com" || !approved.Remote.IsDefaultPort
				|| approved.Profile != HostingHttpsContext.ApiProfile(execution.Target.Profile)
				|| approved.AdministrativePolicy?.Matches(execution.ApiPolicy) != true)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
			var reference = execution.Target.Profile.Hosting!.CredentialReference;
			var metadata = await AzureMetadataAsync(context, reference, deadline.Token);
			var issued = metadata.Type == "HostingAzureDevOpsEntra"
				? await AzureEntraAsync(context, execution, reference, metadata, deadline.Token)
				: await AzureExternalAsync(context, execution, reference, metadata.Type, deadline.Token);
			var current = await AzureMetadataAsync(context, reference, deadline.Token);
			if (current.Type != metadata.Type || current.LastModified != metadata.LastModified
				|| !current.SecretKeys.SequenceEqual(metadata.SecretKeys, StringComparer.Ordinal))
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
			}
			await DemandAsync(context, execution, deadline.Token);
			var limit = DateTimeOffset.UtcNow.AddMinutes(2);
			return new(issued.Scheme, issued.Value, issued.Expiry < limit ? issued.Expiry : limit);
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.TimedOut); }
		catch (OperationCanceledException) { throw; }
		catch (SourceControlSecurityException) { throw; }
		catch { throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
	}

	private async Task<CredentialMetadata> AzureMetadataAsync(SourceControlContext context, string reference, CancellationToken cancellationToken)
	{
		var metadata = await credentials.GetAsync(context.TenantId, reference, cancellationToken);
		if (metadata is null || metadata.Id != reference || metadata.TenantId != context.TenantId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		string[] keys = metadata.Type switch
		{
			"HostingHttpBearer" or "HostingAzureDevOpsPat" => ["token", "expiresAt"],
			"HostingAzureDevOpsEntra" => ["tenantId", "clientId", "clientSecret"],
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable)
		};
		if (keys.Any(key => !metadata.SecretKeys.Contains(key, StringComparer.Ordinal)))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		return metadata;
	}

	private async Task<(string Scheme, string Value, DateTimeOffset Expiry)> AzureExternalAsync(SourceControlContext context,
		AcceptedHostingExecution execution, string reference, string type, CancellationToken cancellationToken)
	{
		var expiry = await credentials.ResolveSecretAsync(context.TenantId, reference, "expiresAt", cancellationToken);
		if (expiry is null || expiry.Length > 64 || !DateTimeOffset.TryParseExact(expiry, "O", CultureInfo.InvariantCulture,
			DateTimeStyles.None, out var expiresAt) || expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		await DemandAsync(context, execution, cancellationToken);
		var token = await credentials.ResolveSecretAsync(context.TenantId, reference, "token", cancellationToken);
		if (string.IsNullOrWhiteSpace(token) || token.Length > 16_384 || token.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		if (type == "HostingHttpBearer")
		{ return ("Bearer", token, expiresAt); }
		var bytes = Encoding.UTF8.GetBytes("VertexBPMN:" + token);
		try
		{ return ("Basic", Convert.ToBase64String(bytes), expiresAt); }
		finally { Array.Clear(bytes); }
	}

	private async Task<(string Scheme, string Value, DateTimeOffset Expiry)> AzureEntraAsync(SourceControlContext context,
		AcceptedHostingExecution execution, string reference, CredentialMetadata expected, CancellationToken cancellationToken)
	{
		var tenant = await credentials.ResolveSecretAsync(context.TenantId, reference, "tenantId", cancellationToken);
		var client = await credentials.ResolveSecretAsync(context.TenantId, reference, "clientId", cancellationToken);
		if (!Guid.TryParseExact(tenant, "D", out var tenantId) || tenantId == Guid.Empty
			|| !Guid.TryParseExact(client, "D", out var clientId) || clientId == Guid.Empty)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		await DemandAsync(context, execution, cancellationToken);
		var secret = await credentials.ResolveSecretAsync(context.TenantId, reference, "clientSecret", cancellationToken);
		if (string.IsNullOrWhiteSpace(secret) || secret.Length > 16_384 || secret.Any(char.IsControl))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		var current = await AzureMetadataAsync(context, reference, cancellationToken);
		if (current.Type != expected.Type || current.LastModified != expected.LastModified
			|| !current.SecretKeys.SequenceEqual(expected.SecretKeys, StringComparer.Ordinal))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		await DemandAsync(context, execution, cancellationToken);
		var issued = await (entraTokens ?? new AzureDevOpsTokenIssuer()).IssueAsync(tenantId, clientId, secret, cancellationToken);
		return ("Bearer", issued.Token, issued.ExpiresOn);
	}
}
