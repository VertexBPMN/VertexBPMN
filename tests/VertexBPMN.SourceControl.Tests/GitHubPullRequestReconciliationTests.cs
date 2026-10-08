using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitHubPullRequestReconciliationTests
{
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), "tenant-a",
		new Uri("https://github.com/example/models.git"), null, "master", "release", ["models"]);
	private static readonly PullRequestCommand Command = new(Guid.NewGuid(), new("reconcile-pr"),
		"vertex/work", "master", new(new string('a', 40)), "Review", "Model change");

	[Fact]
	public void Lost_response_is_reconciled_to_exact_operation_and_commit()
	{
		var receipt = GitHubPullRequestReconciliation.AfterUnknownWrite([Detail()], Binding, Command);
		Assert.Equal(Command.OperationId, receipt.OperationId);
		Assert.Equal(Command.HeadCommit, receipt.HeadCommit);
		Assert.Equal(7, receipt.Number);
	}

	[Fact]
	public void Absent_or_ambiguous_result_does_not_authorize_creation()
	{
		Assert.Equal(SourceControlErrorCode.ResultUnknown, Assert.Throws<SourceControlSecurityException>(() =>
			GitHubPullRequestReconciliation.AfterUnknownWrite([], Binding, Command)).Code);
		Assert.Equal(SourceControlErrorCode.ResultUnknown, Assert.Throws<SourceControlSecurityException>(() =>
			GitHubPullRequestReconciliation.AfterUnknownWrite([Detail(), Detail()], Binding, Command)).Code);
	}

	[Fact]
	public void Foreign_operation_and_embedded_marker_are_not_matches()
	{
		foreach (var body in new[] { GitHubPullRequestReconciliation.RequestBody(Command with { OperationId = Guid.NewGuid() }),
			"prefix " + GitHubPullRequestReconciliation.OperationMarker(Command) })
		{
			Assert.Throws<SourceControlSecurityException>(() =>
				GitHubPullRequestReconciliation.AfterUnknownWrite([Detail(body)], Binding, Command));
		}
	}

	[Fact]
	public void Marker_cannot_override_changed_remote_head()
	{
		Assert.Throws<SourceControlSecurityException>(() => GitHubPullRequestReconciliation.AfterUnknownWrite(
			[Detail()], Binding, Command with { HeadCommit = new(new string('b', 40)) }));
	}

	private static JsonElement Detail(string? body = null) => JsonSerializer.SerializeToElement(new
	{
		number = 7, html_url = "https://github.com/example/models/pull/7", state = "open", merged = false,
		body = body ?? GitHubPullRequestReconciliation.RequestBody(Command),
		head = new { sha = Command.HeadCommit.Value, @ref = Command.WorkBranch, repo = new { full_name = "example/models" } },
		@base = new { @ref = Command.BaseBranch, repo = new { full_name = "example/models" } }
	});
}
