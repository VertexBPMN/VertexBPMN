using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitHubPullRequestTransportTests
{
	[Theory]
	[InlineData("open", false, PullRequestState.Open)]
	[InlineData("closed", false, PullRequestState.Closed)]
	[InlineData("closed", true, PullRequestState.Merged)]
	public async Task Status_read_is_get_only_and_exposes_only_actual_merge(string state, bool merged, PullRequestState expected)
	{
		var binding = new RepositoryBinding(Guid.NewGuid(), "tenant-a", new("https://github.com/example/models.git"),
			null, "master", "release", ["models"]);
		var command = new PullRequestCommand(Guid.NewGuid(), new("status-read"), "vertex/work", "master",
			new(new string('a', 40)), "Review", "Description");
		using var handler = new StatusHandler(command, state, merged);
		using var client = new HttpClient(handler);
		using var token = new GitHubTokenLease("isolated-test-token", DateTimeOffset.UtcNow.AddMinutes(10));
		var receipt = await new GitHubPullRequestTransport(client).ReadAsync(binding, command, 7, token,
			TestContext.Current.CancellationToken);
		Assert.Equal(expected, receipt.State);
		Assert.Equal(merged ? new GitCommitId(new string('b', 40)) : null, receipt.MergeCommit);
		Assert.Equal(command.HeadCommit, receipt.HeadCommit);
		Assert.Equal(1, handler.Calls);
	}

	private sealed class StatusHandler(PullRequestCommand command, string state, bool merged) : HttpMessageHandler
	{
		internal int Calls { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;
			Assert.Equal(HttpMethod.Get, request.Method);
			Assert.Equal("https://api.github.com/repos/example/models/pulls/7", request.RequestUri!.AbsoluteUri);
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{
				Content = System.Net.Http.Json.JsonContent.Create(new
				{
					number = 7, html_url = "https://github.com/example/models/pull/7", state, merged,
					merge_commit_sha = new string('b', 40),
					head = new { sha = command.HeadCommit.Value, @ref = command.WorkBranch, repo = new { full_name = "example/models" } },
					@base = new { @ref = command.BaseBranch, repo = new { full_name = "example/models" } }
				})
			});
		}
	}
	[Theory]
	[InlineData("https://api.github.com/repos/example/models/pulls?state=all&per_page=100&page=1", true)]
	[InlineData("https://api.github.com/repos/example/models/pulls?state=all&per_page=100&page=10", true)]
	[InlineData("https://api.github.com/repos/example/models/pulls?state=all&per_page=100&page=11", false)]
	[InlineData("https://api.github.com/repos/example/models/pulls?state=all&per_page=100&page=1&token=secret", false)]
	[InlineData("https://github.com/repos/example/models/pulls?state=all&per_page=100&page=1", false)]
	public void Only_bounded_fixed_listing_queries_are_allowed(string url, bool allowed)
	{
		if (allowed) SourceControlHttps.ValidateRequestTarget(new(url), ["api.github.com", "github.com"]);
		else Assert.Throws<SourceControlSecurityException>(() =>
			SourceControlHttps.ValidateRequestTarget(new(url), ["api.github.com", "github.com"]));
	}

	[Fact]
	public async Task Reconciliation_reads_detail_and_never_posts()
	{
		var binding = new RepositoryBinding(Guid.NewGuid(), "tenant-a", new("https://github.com/example/models.git"),
			null, "master", "release", ["models"]);
		var command = new PullRequestCommand(Guid.NewGuid(), new("http-pr"), "vertex/work", "master",
			new(new string('a', 40)), "Review", "Description");
		using var handler = new ReconciliationHandler(command);
		using var client = new HttpClient(handler);
		using var token = new GitHubTokenLease("isolated-test-token", DateTimeOffset.UtcNow.AddMinutes(10));
		var receipt = await new GitHubPullRequestTransport(client).ReconcileAsync(binding, command, token, TestContext.Current.CancellationToken);
		Assert.Equal(7, receipt.Number);
		Assert.Equal(3, handler.Calls);
	}

	private sealed class ReconciliationHandler(PullRequestCommand command) : HttpMessageHandler
	{
		internal int Calls { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;
			Assert.Equal(HttpMethod.Get, request.Method);
			var body = GitHubPullRequestReconciliation.RequestBody(command);
			object payload = request.RequestUri!.Query.EndsWith("page=1", StringComparison.Ordinal)
				? Enumerable.Range(101, 100).Select(number => new { number, body = "unrelated review" }).ToArray()
				: request.RequestUri.Query.Length > 0 ? new[] { new { number = 7, body } } : new
			{
				number = 7, body, html_url = "https://github.com/example/models/pull/7", state = "open", merged = false,
				head = new { sha = command.HeadCommit.Value, @ref = command.WorkBranch, repo = new { full_name = "example/models" } },
				@base = new { @ref = command.BaseBranch, repo = new { full_name = "example/models" } }
			};
			return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
			{ Content = System.Net.Http.Json.JsonContent.Create(payload) });
		}
	}
	[Fact]
	public async Task Lost_create_response_is_unknown_and_never_retried()
	{
		using var handler = new LostResponseHandler();
		using var client = new HttpClient(handler);
		using var token = new GitHubTokenLease("isolated-test-token", DateTimeOffset.UtcNow.AddMinutes(10));
		var binding = new RepositoryBinding(Guid.NewGuid(), "tenant-a", new("https://github.com/example/models.git"),
			null, "master", "release", ["models"]);
		var command = new PullRequestCommand(Guid.NewGuid(), new("http-pr"), "vertex/work", "master",
			new(new string('a', 40)), "Review", "Description");
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
			new GitHubPullRequestTransport(client).CreateAsync(binding, command, token, TestContext.Current.CancellationToken));
		Assert.Equal(SourceControlErrorCode.ResultUnknown, error.Code);
		Assert.Equal(1, handler.Calls);
	}

	[Fact]
	public async Task Rate_limit_response_is_sanitized_and_not_retried()
	{
		using var handler = new RateLimitHandler();
		using var client = new HttpClient(handler);
		using var token = new GitHubTokenLease("isolated-test-token", DateTimeOffset.UtcNow.AddMinutes(10));
		var binding = new RepositoryBinding(Guid.NewGuid(), "tenant-a", new("https://github.com/example/models.git"),
			null, "master", "release", ["models"]);
		var command = new PullRequestCommand(Guid.NewGuid(), new("http-pr"), "vertex/work", "master",
			new(new string('a', 40)), "Review", "Description");
		var error = await Assert.ThrowsAsync<SourceControlSecurityException>(() =>
			new GitHubPullRequestTransport(client).ReadAsync(binding, command, 7, token, TestContext.Current.CancellationToken));
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable, error.Code);
		Assert.DoesNotContain("sensitive", error.ToString());
	}

	private sealed class LostResponseHandler : HttpMessageHandler
	{
		internal int Calls { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			Calls++;
			Assert.Equal(HttpMethod.Post, request.Method);
			Assert.Equal("https://api.github.com/repos/example/models/pulls", request.RequestUri!.AbsoluteUri);
			throw new HttpRequestException("Lost response after server write");
		}
	}

	private sealed class RateLimitHandler : HttpMessageHandler
	{
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
			Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests) { Content = new StringContent("sensitive provider response") });
	}
}
