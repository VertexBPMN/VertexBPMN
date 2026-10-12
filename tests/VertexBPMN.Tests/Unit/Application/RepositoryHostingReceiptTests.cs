using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class RepositoryHostingReceiptTests
{
	private static readonly RepositoryHostingTarget Target = new("github", "team/repository",
		new Uri("https://api.github.com/repos/team/repository"), new Uri("https://github.com/team/repository"));
	private static readonly PullRequestCommand Command = new(Guid.NewGuid(), new("hosting-receipt"), "vertex/work", "main",
		new(new string('a', 40)), "title", "body");
	private static readonly PullRequestReceipt Receipt = new(Command.OperationId, "github", 17,
		new Uri("https://github.com/team/repository/pull/17"), PullRequestState.Open, Command.HeadCommit, null);

	[Theory]
	[InlineData(PullRequestState.Open)]
	[InlineData(PullRequestState.Closed)]
	[InlineData(PullRequestState.Merged)]
	public void Consistent_provider_operation_head_and_state_are_accepted(PullRequestState state)
	{
		var receipt = Receipt with
		{
			State = state,
			MergeCommit = state == PullRequestState.Merged ? new(new string('b', 40)) : null
		};
		var error = Record.Exception(() => RepositoryHostingReceiptPolicy.Demand(Target, Command, receipt));
		Assert.Null(error);
	}

	[Theory]
	[InlineData("provider")]
	[InlineData("operation")]
	[InlineData("head")]
	[InlineData("number")]
	[InlineData("state")]
	[InlineData("unmerged-commit")]
	[InlineData("merged-missing-commit")]
	public void Contradictory_response_cannot_become_a_receipt(string change)
	{
		var receipt = change switch
		{
			"provider" => Receipt with { ProviderId = "unknown" },
			"operation" => Receipt with { OperationId = Guid.NewGuid() },
			"head" => Receipt with { HeadCommit = new(new string('b', 40)) },
			"number" => Receipt with { Number = 0 },
			"state" => Receipt with { State = (PullRequestState)999 },
			"unmerged-commit" => Receipt with { MergeCommit = new(new string('b', 40)) },
			_ => Receipt with { State = PullRequestState.Merged }
		};
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(
			() => RepositoryHostingReceiptPolicy.Demand(Target, Command, receipt)).Code);
	}

	[Theory]
	[InlineData("https://github.com/team/other/pull/17")]
	[InlineData("https://github.com/team/repository/pull/18")]
	[InlineData("https://github.com/team/repository/pull/17?token=synthetic")]
	[InlineData("https://github.com/team/repository/pull/17#fragment")]
	[InlineData("https://github.com.evil.example/team/repository/pull/17")]
	[InlineData("https://github.com:8443/team/repository/pull/17")]
	[InlineData("/team/repository/pull/17")]
	public void Repository_and_link_identity_are_exact(string input)
	{
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(
			() => RepositoryHostingReceiptPolicy.Demand(Target, Command, Receipt with { Url = new Uri(input, UriKind.RelativeOrAbsolute) })).Code);
	}
}
