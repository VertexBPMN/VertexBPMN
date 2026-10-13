using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class GitHubHostingHttpPolicyTests
{
	private static readonly Uri Repository = new("https://api.github.com/repos/team/repository");

	[Theory]
	[InlineData("POST", "/pulls")]
	[InlineData("GET", "/pulls?state=all&per_page=100&page=1")]
	[InlineData("GET", "/pulls?state=all&per_page=100&page=10")]
	[InlineData("GET", "/pulls/17")]
	[InlineData("GET", "/pulls/17/reviews?per_page=100&page=1")]
	[InlineData("GET", "/pulls/17/reviews?per_page=100&page=10")]
	public void Implemented_PR_routes_are_allowed(string method, string suffix)
	{
		using var request = new HttpRequestMessage(new HttpMethod(method), Repository.AbsoluteUri + suffix);
		Assert.Null(Record.Exception(() => GitHubHostingHttpPolicy.Demand(request, Repository)));
	}

	[Theory]
	[InlineData("POST", "/pulls")]
	[InlineData("GET", "/pulls?state=all&per_page=100&page=1")]
	[InlineData("GET", "/pulls/17")]
	[InlineData("GET", "/pulls/17/reviews?per_page=100&page=1")]
	public async Task Allowed_routes_reach_the_approved_DNS_boundary(string method, string suffix)
	{
		var resolutions = 0;
		using var client = SourceControlHttps.CreateClient(["api.github.com"], TimeSpan.FromSeconds(1), (host, _) =>
		{
			Assert.Equal("api.github.com", host);
			resolutions++;
			throw new InvalidOperationException("isolated-dns-stop");
		}, Repository);
		using var request = new HttpRequestMessage(new HttpMethod(method), Repository.AbsoluteUri + suffix);
		var error = await Record.ExceptionAsync(() => client.SendAsync(request, TestContext.Current.CancellationToken));
		Assert.NotNull(error);
		Assert.Contains("isolated-dns-stop", error.ToString(), StringComparison.Ordinal);
		Assert.Equal(1, resolutions);
	}

	[Theory]
	[InlineData("GET", "https://api.github.com/repos/team/other/pulls/17")]
	[InlineData("GET", "https://api.github.com/repos/team/repository-extra/pulls/17")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/actions/secrets")]
	[InlineData("GET", "https://api.github.com/repos/team/repository")]
	[InlineData("DELETE", "https://api.github.com/repos/team/repository/pulls/17")]
	[InlineData("PATCH", "https://api.github.com/repos/team/repository/pulls/17")]
	[InlineData("POST", "https://api.github.com/repos/team/repository/pulls/17/reviews")]
	[InlineData("POST", "https://api.github.com/repos/team/repository/pulls?state=all&per_page=100&page=1")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls/017")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls/0")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls/9223372036854775808")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls/17/merge")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls/17/reviews")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls/17?state=all&per_page=100&page=1")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls?state=all&per_page=100&page=11")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls?state=all&per_page=100&page=1&extra=secret")]
	[InlineData("GET", "https://api.github.com/repos/team/repository/pulls/17#fragment")]
	[InlineData("GET", "https://other.example/repos/team/repository/pulls/17")]
	[InlineData("GET", "https://user@api.github.com/repos/team/repository/pulls/17")]
	[InlineData("GET", "https://api.github.com:444/repos/team/repository/pulls/17")]
	[InlineData("GET", "http://api.github.com/repos/team/repository/pulls/17")]
	public async Task Profiled_client_rejects_unimplemented_routes_before_connecting(string method, string input)
	{
		var resolutions = 0;
		using var client = SourceControlHttps.CreateClient(["api.github.com"], TimeSpan.FromSeconds(1), (_, _) =>
		{
			resolutions++;
			throw new InvalidOperationException("No DNS or socket access expected for rejected routes.");
		}, Repository);
		using var request = new HttpRequestMessage(new HttpMethod(method), input);
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() => client.SendAsync(request, TestContext.Current.CancellationToken));
		Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
		Assert.Equal(0, resolutions);
	}
}
