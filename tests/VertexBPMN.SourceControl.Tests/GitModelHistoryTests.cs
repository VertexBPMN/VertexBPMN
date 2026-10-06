using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitModelHistoryTests
{
	private static readonly GitCommitId Commit = new(new string('1', 40));
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), "tenant", new("https://github.com/example/models.git"),
		null, "master", "release", ["models"]);
	private static readonly HistoryRequest Request = new("models/example.bpmn", Commit, 1, null);

	[Theory]
	[InlineData("invalid\0" + "1800000000\0subject\0")]
	[InlineData("1111111111111111111111111111111111111111\0not-a-date\0subject\0")]
	[InlineData("1111111111111111111111111111111111111111\09999999999999\0subject\0")]
	[InlineData("1111111111111111111111111111111111111111\01800000000\0subject")]
	[InlineData("1111111111111111111111111111111111111111\01800000000\0sub\tject\0")]
	public void Malformed_history_metadata_fails_closed(string text)
	{
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
			GitModelHistory.ReadPage(Encoding.UTF8.GetBytes(text), Binding, Request, new(), TestContext.Current.CancellationToken)).Code);
	}

	[Fact]
	public void History_cursor_is_repository_path_and_revision_bound_and_limits_are_not_page_bypassed()
	{
		var text = Commit.Value + "\01800000000\0first\0" + new string('2', 40) + "\01800000001\0second\0";
		var bytes = Encoding.UTF8.GetBytes(text);
		var first = GitModelHistory.ReadPage(bytes, Binding, Request, new(), TestContext.Current.CancellationToken);
		Assert.Single(first.Items);
		Assert.NotNull(first.NextCursor);
		Assert.Equal(SourceControlErrorCode.InvalidInput, Assert.Throws<SourceControlSecurityException>(() =>
			GitModelHistory.ReadPage(bytes, Binding, Request with { Path = "models/other.bpmn", Cursor = first.NextCursor }, new(), TestContext.Current.CancellationToken)).Code);
		Assert.Equal(SourceControlErrorCode.InvalidInput, Assert.Throws<SourceControlSecurityException>(() =>
			GitModelHistory.ReadPage(bytes, Binding with { Id = Guid.NewGuid() }, Request with { Cursor = first.NextCursor }, new(), TestContext.Current.CancellationToken)).Code);
		Assert.Equal(SourceControlErrorCode.PayloadTooLarge, Assert.Throws<SourceControlSecurityException>(() =>
			GitModelHistory.ReadPage(bytes, Binding, Request, new() { MaxHistoryCommits = 1 }, TestContext.Current.CancellationToken)).Code);
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
			GitModelHistory.ReadPage([0xff], Binding, Request, new(), TestContext.Current.CancellationToken)).Code);
		Assert.Empty(GitModelHistory.ReadPage([], Binding, Request, new(), TestContext.Current.CancellationToken).Items);
	}
}
