using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitRemoteHeadTests
{
	[Theory]
	[InlineData(40)]
	[InlineData(64)]
	public void Exact_head_is_required_and_absent_head_is_not_invented(int length)
	{
		var oid = new string('a', length);
		Assert.Equal(new GitCommitId(oid), GitRemoteReferences.ReadHead(Encoding.UTF8.GetBytes(oid + "\trefs/heads/master\n"), "master"));
		Assert.Null(GitRemoteReferences.ReadHead([], "master"));
		foreach (var text in new[]
		{
			oid + "\trefs/heads/Master\n", oid + "\trefs/tags/master\n",
			oid + "\trefs/heads/master", oid + "\trefs/heads/master\n\n", "bad\trefs/heads/master\n",
			new string('0', length) + "\trefs/heads/master\n",
			oid + "\trefs/heads/master\n" + oid + "\trefs/heads/master\n"
		})
		{
			Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
				GitRemoteReferences.ReadHead(Encoding.UTF8.GetBytes(text), "master")).Code);
		}
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
			GitRemoteReferences.ReadHead([0xff], "master")).Code);
	}
}
