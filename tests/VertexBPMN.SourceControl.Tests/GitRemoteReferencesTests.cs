using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitRemoteReferencesTests
{
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), "tenant", new("https://github.com/example/models.git"),
		null, "master", "release", ["models"]);
	private static readonly SourceControlLimits Limits = new();

	[Theory]
	[InlineData("not-an-oid\trefs/heads/master\n")]
	[InlineData("1111111111111111111111111111111111111111\trefs/tags/tag\n")]
	[InlineData("1111111111111111111111111111111111111111\trefs/heads/master")]
	[InlineData("1111111111111111111111111111111111111111\trefs/heads/master\n1111111111111111111111111111111111111111\trefs/heads/Master\n")]
	public void Malformed_remote_heads_are_not_accepted(string text)
	{
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
			GitRemoteReferences.ReadPage(Encoding.UTF8.GetBytes(text), Binding, 10, null, Limits, TestContext.Current.CancellationToken)).Code);
	}

	[Fact]
	public void Cursor_detects_head_reset_even_when_branch_names_are_unchanged()
	{
		var original = Encoding.UTF8.GetBytes("1111111111111111111111111111111111111111\trefs/heads/a\n2222222222222222222222222222222222222222\trefs/heads/b\n");
		var first = GitRemoteReferences.ReadPage(original, Binding, 1, null, Limits, TestContext.Current.CancellationToken);
		Assert.NotNull(first.NextCursor);
		var reset = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original).Replace('2', '3'));
		Assert.Equal(SourceControlErrorCode.RevisionConflict, Assert.Throws<SourceControlSecurityException>(() =>
			GitRemoteReferences.ReadPage(reset, Binding, 1, first.NextCursor, Limits, TestContext.Current.CancellationToken)).Code);
		Assert.Equal(SourceControlErrorCode.InvalidInput, Assert.Throws<SourceControlSecurityException>(() =>
			GitRemoteReferences.ReadPage(original, Binding, 1, new string('x', 1025), Limits, TestContext.Current.CancellationToken)).Code);
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
			GitRemoteReferences.ReadPage([0xff], Binding, 1, null, Limits, TestContext.Current.CancellationToken)).Code);
		Assert.Empty(GitRemoteReferences.ReadPage([], Binding, 10, null, Limits, TestContext.Current.CancellationToken).Items);
	}
}
