using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>API network scoping only; this synthetic profile is never passed to a Git credential broker.</summary>
internal static class HostingHttpsContext
{
	internal static RepositoryConnectionProfile ApiProfile(RepositoryConnectionProfile profile)
	{
		var hosting = profile.Hosting ?? throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		return profile with
		{
			Transport = GitTransportKind.Https,
			Authentication = GitAuthenticationKind.HttpBearer,
			GitEndpoint = hosting.ApiEndpoint,
			GitCredentialReference = hosting.CredentialReference,
			GitCapabilities = SourceControlCapability.Read,
			Hosting = null
		};
	}

	internal static RepositoryBinding ApiBinding(RepositoryBinding binding, RepositoryHostingTarget target) =>
		binding with { Remote = target.ApiRepositoryUri, CredentialReference = null };
}
