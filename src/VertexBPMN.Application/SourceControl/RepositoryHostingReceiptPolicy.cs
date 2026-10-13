using System.Globalization;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Common receipt invariants independent of a provider's response decoder.</summary>
public static class RepositoryHostingReceiptPolicy
{
	public static void Demand(RepositoryHostingTarget target, PullRequestCommand command, PullRequestReceipt receipt)
	{
		ArgumentNullException.ThrowIfNull(target);
		ArgumentNullException.ThrowIfNull(command);
		ArgumentNullException.ThrowIfNull(receipt);
		if (receipt.OperationId != command.OperationId || receipt.HeadCommit != command.HeadCommit
			|| receipt.ProviderId != target.ProviderId || receipt.Number <= 0 || receipt.Url?.IsAbsoluteUri != true
			|| !Enum.IsDefined(receipt.State)
			|| (receipt.State == PullRequestState.Merged && receipt.MergeCommit is null)
			|| (receipt.State != PullRequestState.Merged && receipt.MergeCommit is not null))
		{
			Reject();
		}
		var suffix = target.ProviderId switch
		{
			"github" => "/pull/",
			AzureDevOpsHostingTargetPolicy.ProviderId => "/pullrequest/",
			_ => throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable)
		};
		var expected = target.WebRepositoryUri.AbsoluteUri + suffix + receipt.Number.ToString(CultureInfo.InvariantCulture);
		// AbsoluteUri comparison also excludes extra fragments, query parameters and userinfo.
		if (receipt.Url.AbsoluteUri != expected)
		{
			Reject();
		}
	}

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
}
