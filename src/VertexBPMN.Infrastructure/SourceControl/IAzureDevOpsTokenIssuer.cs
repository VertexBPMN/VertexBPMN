using Azure.Core;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Explicit application identity; no ambient credential chain.</summary>
internal interface IAzureDevOpsTokenIssuer
{
	Task<AccessToken> IssueAsync(Guid tenantId, Guid clientId, string clientSecret, CancellationToken cancellationToken);
}
