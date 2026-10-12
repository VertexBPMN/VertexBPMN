using Azure.Core;
using Azure.Identity;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class AzureDevOpsTokenIssuer : IAzureDevOpsTokenIssuer
{
	internal const string Scope = "499b84ac-1321-427f-aa17-267ca6975798/.default";

	public async Task<AccessToken> IssueAsync(Guid tenantId, Guid clientId, string clientSecret, CancellationToken cancellationToken)
	{
		var credential = new ClientSecretCredential(tenantId.ToString("D"), clientId.ToString("D"), clientSecret,
			new ClientSecretCredentialOptions { AuthorityHost = AzureAuthorityHosts.AzurePublicCloud });
		return await credential.GetTokenAsync(new TokenRequestContext([Scope]), cancellationToken);
	}
}
