using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	internal async Task FetchApprovedProfileRevisionAsync(GitWorkspace workspace, SourceControlContext context,
		RepositoryBinding binding, ApprovedGitHttpsTarget approved,
		GitCommitId revision, IGitCredentialLease? lease, CancellationToken cancellationToken)
		=> _ = await WithApprovedProfileAsync(workspace, context, binding, approved, lease, async (certificateFile, token) =>
		{
			await FetchCoreAsync(workspace, approved.Remote, "", lease, approved.CopyAddresses(), certificateFile, token, revision);
			return true;
		}, cancellationToken);

	internal async Task FetchApprovedProfileHistoryAsync(GitWorkspace workspace, SourceControlContext context,
		RepositoryBinding binding, ApprovedGitHttpsTarget approved, GitCommitId revision, IGitCredentialLease? lease,
		CancellationToken cancellationToken)
	{
		GitModelHistory.ValidateLimit(options.Value.Limits);
		_ = await WithApprovedProfileAsync(workspace, context, binding, approved, lease, async (certificateFile, token) =>
		{
			await FetchCoreAsync(workspace, approved.Remote, "", lease, approved.CopyAddresses(), certificateFile, token,
				revision, options.Value.Limits.MaxHistoryCommits + 1);
			return true;
		}, cancellationToken);
	}

	internal Task<SourceControlPage<string>> ListApprovedProfileBranchesAsync(GitWorkspace workspace, SourceControlContext context,
		RepositoryBinding binding, ApprovedGitHttpsTarget approved, IGitCredentialLease? lease, int pageSize, string? cursor,
		CancellationToken cancellationToken) => WithApprovedProfileAsync(workspace, context, binding, approved, lease,
		(certificateFile, token) => ListRemoteBranchesCoreAsync(workspace, binding, lease, pageSize, cursor,
			approved.CopyAddresses(), certificateFile, token), cancellationToken);

	internal Task<RevisionSelection> ResolveApprovedProfileBranchAsync(GitWorkspace workspace, SourceControlContext context,
		RepositoryBinding binding, ApprovedGitHttpsTarget approved, IGitCredentialLease? lease, string branch,
		CancellationToken cancellationToken)
	{
		DemandReadBinding(context, binding, branch);
		return WithApprovedProfileAsync(workspace, context, binding, approved, lease,
			(certificateFile, token) => ResolveRemoteBranchCoreAsync(workspace, binding, branch, lease,
				approved.CopyAddresses(), certificateFile, token), cancellationToken);
	}

	private async Task<T> WithApprovedProfileAsync<T>(GitWorkspace workspace, SourceControlContext context,
		RepositoryBinding binding, ApprovedGitHttpsTarget approved, IGitCredentialLease? lease,
		Func<string?, CancellationToken, Task<T>> execute, CancellationToken cancellationToken)
	{
		var configuration = DemandApprovedProfile(context, binding, approved, lease);
		var profile = approved.Profile;
		string? certificateFile = null;
		var created = false;
		var cleanupSucceeded = true;
		T result;
		try
		{
			if (configuration.Trust is { } trust)
			{
				_ = GitHttpsConfigurationPolicy.Snapshot(trust);
				RepositoryConnectionProfilePolicy.DemandRepository(context, binding,
					profile with { GitEndpoint = new(trust.RepositoryScope, configuration.Network.Id, trust.Id) });
				DemandControlDirectory(workspace);
				certificateFile = Path.Combine(workspace.ControlDirectory, "ca-" + Guid.NewGuid().ToString("N") + ".pem");
				await using var file = new FileStream(certificateFile, FileMode.CreateNew, FileAccess.Write, FileShare.None,
					4096, FileOptions.Asynchronous);
				created = true;
				await file.WriteAsync(Encoding.UTF8.GetBytes(trust.CertificateAuthorityPem), cancellationToken);
			}
			await VersionAsync(workspace, cancellationToken);
			result = await execute(certificateFile, cancellationToken);
		}
		catch (Exception error) when (error is IOException or UnauthorizedAccessException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		finally
		{
			if (created)
			{
				try
				{
					File.Delete(certificateFile!);
				}
				catch (Exception error) when (error is IOException or UnauthorizedAccessException)
				{
					cleanupSucceeded = false;
				}
			}
		}
		if (!cleanupSucceeded)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		return result;
	}

	private static ResolvedGitHttpsPolicy DemandApprovedProfile(SourceControlContext context, RepositoryBinding binding,
		ApprovedGitHttpsTarget approved, IGitCredentialLease? lease)
	{
		var profile = approved.Profile;
		var configuration = approved.AdministrativePolicy
			?? throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, profile);
		if (binding.ConnectionProfileId != profile.Id || binding.Remote.AbsoluteUri != approved.Remote.AbsoluteUri
			|| approved.RequiredCapabilities is not (SourceControlCapability.Read or SourceControlCapability.Commit or SourceControlCapability.Push)
			|| (profile.GitCapabilities & approved.RequiredCapabilities) != approved.RequiredCapabilities
			|| profile.Transport != GitTransportKind.Https || configuration.Network.IsRevoked
			|| configuration.Network.Id != profile.GitEndpoint.NetworkPolicyReference
			|| configuration.Network.TenantId != profile.TenantId
			|| profile.GitEndpoint.TrustProfileReference != configuration.Trust?.Id
			|| configuration.Trust is { IsRevoked: true }
			|| (configuration.Trust is not null && configuration.Trust.TenantId != profile.TenantId))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		_ = GitHttpsConfigurationPolicy.Snapshot(configuration.Network);
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding,
			profile with { GitEndpoint = new(configuration.Network.RepositoryScope, configuration.Network.Id) });
		var authenticationMatches = profile.Authentication switch
		{
			GitAuthenticationKind.Anonymous => lease is null,
			GitAuthenticationKind.HttpBasic => lease is GitHttpBasicCredentialLease,
			GitAuthenticationKind.HttpBearer => lease is GitHttpBearerCredentialLease,
			_ => false
		};
		if (!authenticationMatches)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		return configuration;
	}

	private static void DemandControlDirectory(GitWorkspace workspace)
	{
		if (!Path.IsPathFullyQualified(workspace.Directory) || !Path.IsPathFullyQualified(workspace.ControlDirectory)
			|| Path.GetFullPath(workspace.ControlDirectory) != Path.GetFullPath(Path.Combine(workspace.Directory, "control"))
			|| !Directory.Exists(workspace.ControlDirectory))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
		}
		for (var item = new DirectoryInfo(workspace.ControlDirectory); item is not null; item = item.Parent)
		{
			if ((item.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
			}
		}
	}
}
