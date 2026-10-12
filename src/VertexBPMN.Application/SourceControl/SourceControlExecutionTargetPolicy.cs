using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>
/// Acceptance and execution checks for immutable targets. Callers must additionally
/// check current actor grants, profile revocation and credential status before I/O.
/// </summary>
public static class SourceControlExecutionTargetPolicy
{
	public static SourceControlExecutionTarget Capture(SourceControlContext context,
		RepositoryAccessSnapshot access, RepositoryConnectionProfile profile,
		SourceControlCapability requiredCapabilities)
	{
		ArgumentNullException.ThrowIfNull(access);
		RepositoryConnectionProfilePolicy.DemandRepository(context, access.Binding, profile);
		if (access.Binding.Id == Guid.Empty || access.Revision <= 0)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		DemandCapabilities(profile, requiredCapabilities);
		return new(access.Binding.Id, access.Revision, access.Binding.Remote, profile, requiredCapabilities);
	}

	public static void DemandCurrent(SourceControlContext context, RepositoryAccessSnapshot access,
		RepositoryConnectionProfile currentProfile, SourceControlExecutionTarget accepted)
	{
		ArgumentNullException.ThrowIfNull(access);
		ArgumentNullException.ThrowIfNull(accepted);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(currentProfile);
		if (accepted.Profile is null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}

		if (accepted.Profile.TenantId != context.TenantId || currentProfile.TenantId != context.TenantId
			|| access.Binding.TenantId != context.TenantId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}

		if (accepted.SchemaVersion != SourceControlExecutionTarget.CurrentSchemaVersion
			|| accepted.RepositoryId == Guid.Empty || accepted.BindingRevision <= 0 || accepted.Remote is null)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}

		RepositoryConnectionProfilePolicy.DemandRepository(context,
			access.Binding with { Id = accepted.RepositoryId, Remote = accepted.Remote }, accepted.Profile);
		DemandCapabilities(accepted.Profile, accepted.RequiredCapabilities);
		var current = Capture(context, access, currentProfile, accepted.RequiredCapabilities);
		// Compare the entire secret-free contract, including credentials, trust, API identity
		// and policy references. Reusing an ID/revision must not silently retarget a job.
		if (current != accepted || current.Remote.AbsoluteUri != accepted.Remote.AbsoluteUri
			|| current.Profile.GitEndpoint.BaseUri.AbsoluteUri != accepted.Profile.GitEndpoint.BaseUri.AbsoluteUri
			|| current.Profile.Hosting?.ApiEndpoint.BaseUri.AbsoluteUri != accepted.Profile.Hosting?.ApiEndpoint.BaseUri.AbsoluteUri)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.RevisionConflict);
		}
	}

	private static void DemandCapabilities(RepositoryConnectionProfile profile, SourceControlCapability required)
	{
		if (required == SourceControlCapability.None)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}

		var available = profile.GitCapabilities | (profile.Hosting?.Capabilities ?? SourceControlCapability.None);
		if ((available & required) != required)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
	}
}
