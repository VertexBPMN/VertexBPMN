using VertexBPMN.SourceControl.Abstractions;
using VertexBPMN.Application.SourceControl;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed partial class ControlledGitProcess
{
	internal Task<GitCommitId?> ReadApprovedProfilePushHeadAsync(GitWorkspace workspace, SourceControlContext context,
		RepositoryBinding binding, ApprovedGitHttpsTarget approved, PushCommand command, IGitCredentialLease lease,
		CancellationToken cancellationToken)
	{
		DemandApprovedPush(approved);
		ValidatePush(binding, command);
		return WithApprovedProfileAsync(workspace, context, binding, approved, lease,
			(certificateFile, token) => ReadRemoteHeadCoreAsync(workspace, binding.Remote, command.WorkBranch, lease,
				approved.CopyAddresses(), certificateFile, token), cancellationToken);
	}

	internal async Task PushApprovedProfileAsync(GitWorkspace workspace, SourceControlContext context, RepositoryBinding binding,
		ApprovedGitHttpsTarget approved, PushCommand command, IGitCredentialLease lease, CancellationToken cancellationToken)
	{
		DemandApprovedPush(approved);
		ValidatePush(binding, command);
		_ = await WithApprovedProfileAsync(workspace, context, binding, approved, lease, async (certificateFile, token) =>
		{
			await PushCoreAsync(workspace, binding, command, lease, approved.CopyAddresses(), certificateFile, token);
			return true;
		}, cancellationToken);
	}

	private static void DemandApprovedPush(ApprovedGitHttpsTarget approved)
	{
		if (approved.RequiredCapabilities != SourceControlCapability.Push || (approved.Profile.GitCapabilities & SourceControlCapability.Push) == SourceControlCapability.None)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.Forbidden);
		}
	}
}
