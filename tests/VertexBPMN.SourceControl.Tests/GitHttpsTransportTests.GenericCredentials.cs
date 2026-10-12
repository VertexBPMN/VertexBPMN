using System.Text;
using VertexBPMN.Infrastructure.SourceControl;

namespace VertexBPMN.SourceControl.Tests;

public sealed partial class GitHttpsTransportTests
{
	[Theory]
	[InlineData("service-account")]
	[InlineData("azure-pat-user")]
	public async Task Basic_credentials_fetch_exact_bytes_through_the_real_challenged_helper(string username)
	{
		await using var fixture = await HttpsFixture.CreateAsync(username: username);
		using var lease = new GitHttpBasicCredentialLease(fixture.Remote, username, fixture.Token, DateTimeOffset.UtcNow.AddMinutes(2));
		await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", lease,
			fixture.CaFile, TestContext.Current.CancellationToken);
		Assert.True(fixture.Challenges > 0);
		Assert.True(fixture.AuthenticatedRequests > 0);
		Assert.Equal(0, fixture.AnonymousRequests);
		Assert.Equal(fixture.ModelBytes, await fixture.GitAsync(["--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git"),
			"show", "refs/heads/vertex-source:models/example.bpmn"]));
		foreach (var path in Directory.EnumerateFiles(fixture.Workspace.Directory, "*", SearchOption.AllDirectories))
		{
			Assert.DoesNotContain(fixture.Token, Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path,
				TestContext.Current.CancellationToken)), StringComparison.Ordinal);
		}
	}

	[Fact]
	public async Task Anonymous_https_fetch_sends_no_credentials_and_preserves_exact_bytes()
	{
		await using var fixture = await HttpsFixture.CreateAsync(username: null);
		await fixture.Runner.FetchLocalAcceptanceAsync(fixture.Workspace, fixture.Remote, "master", null,
			fixture.CaFile, TestContext.Current.CancellationToken);
		Assert.True(fixture.AnonymousRequests > 0);
		Assert.Equal(0, fixture.Challenges);
		Assert.Equal(0, fixture.AuthenticatedRequests);
		Assert.Equal(fixture.ModelBytes, await fixture.GitAsync(["--git-dir=" + Path.Combine(fixture.Workspace.Directory, "repository.git"),
			"show", "refs/heads/vertex-source:models/example.bpmn"]));
	}
}
