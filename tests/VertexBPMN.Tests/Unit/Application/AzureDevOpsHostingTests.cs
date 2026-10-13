using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class AzureDevOpsHostingTests
{
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), "tenant", new("https://dev.azure.com/organization/project/_git/repository"),
		null, "main", "release", ["models"]);
	private static readonly RepositoryHostingTarget Target = new(AzureDevOpsHostingTargetPolicy.ProviderId, "organization/d3321197-7da9-46ed-a7ec-5d5af8d0d73c/b38e16b1-e292-45c3-b697-a4a6f37a825f",
		new("https://dev.azure.com/organization/project/_apis/git/repositories/b38e16b1-e292-45c3-b697-a4a6f37a825f"), Binding.Remote)
	{ ApiVersion = AzureDevOpsHostingTargetPolicy.ApiVersion };
	private static readonly PullRequestCommand Command = new(Guid.NewGuid(), new("azure-operation"), "vertex/test", "main", new(new string('a', 40)), "Azure", "description");

	[Fact]
	public void Explicit_organization_project_repository_mapping_matches_both_endpoints()
	{
		var profile = new RepositoryConnectionProfile(Guid.NewGuid(), "tenant", 1, GitTransportKind.Https, GitAuthenticationKind.HttpBasic,
			new(Binding.Remote, "git-network"), "git-credential", SourceControlCapability.Read)
		{ Hosting = new(Target.ProviderId, new(Target.ApiRepositoryUri, "api-network"), Target.RepositoryId, "hosting-credential", SourceControlCapability.PullRequest) };
		Assert.Equal(Target, new RepositoryHostingTargetPolicy().Resolve(new("tenant", "actor"), Binding, profile));
		var error = Assert.Throws<SourceControlSecurityException>(() => new RepositoryHostingTargetPolicy().Resolve(new("tenant", "actor"), Binding,
			profile with { Hosting = profile.Hosting with { RepositoryId = "organization/other/repository" } }));
		Assert.Equal(SourceControlErrorCode.InvalidInput, error.Code);
	}

	[Theory]
	[InlineData("active", PullRequestState.Open)]
	[InlineData("completed", PullRequestState.Merged)]
	[InlineData("abandoned", PullRequestState.Closed)]
	public void Native_states_and_current_votes_preserve_the_actual_merge(string state, PullRequestState expected)
	{
		var receipt = AzureDevOpsPullRequestResponse.Read(Payload(state), Target, Command);
		Assert.Equal(expected, receipt.State);
		Assert.Equal(expected == PullRequestState.Merged, receipt.MergeCommit is not null);
		Assert.Equal("https://dev.azure.com/organization/project/_git/repository/pullrequest/17", receipt.Url.AbsoluteUri);
		Assert.Equal(10, Assert.Single(receipt.CurrentVotes!).Vote);
		Assert.Null(receipt.Reviews);
	}

	[Theory]
	[InlineData("sourceRefName", "refs/heads/other")]
	[InlineData("targetRefName", "refs/heads/other")]
	[InlineData("description", "different")]
	[InlineData("title", "different")]
	[InlineData("status", "unknown")]
	[InlineData("url", "https://other.example/pulls/17")]
	public void Contradictory_responses_are_rejected(string field, string value)
	{
		var payload = Payload("active").Deserialize<Dictionary<string, JsonElement>>()!;
		payload[field] = JsonSerializer.SerializeToElement(value);
		var error = Assert.Throws<SourceControlSecurityException>(() => AzureDevOpsPullRequestResponse.Read(JsonSerializer.SerializeToElement(payload), Target, Command));
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, error.Code);
	}

	[Theory]
	[InlineData("PATCH", "/pullrequests/17?api-version=7.1")]
	[InlineData("GET", "/pullrequests/17?api-version=6.0")]
	[InlineData("GET", "/pullrequests/17?api-version=7.1&extra=secret")]
	[InlineData("GET", "/pullrequests/017?api-version=7.1")]
	[InlineData("GET", "/pullrequests/17/reviewers?api-version=7.1")]
	[InlineData("POST", "/pullrequests/17?api-version=7.1")]
	public async Task API_routes_are_rejected_before_DNS(string method, string suffix)
	{
		var resolutions = 0;
		using var client = SourceControlHttps.CreateClient(["dev.azure.com"], TimeSpan.FromSeconds(1), (_, _) =>
		{ resolutions++; throw new InvalidOperationException("unexpected DNS"); }, null, request => AzureDevOpsHostingHttpPolicy.Demand(request, Target.ApiRepositoryUri));
		using var request = new HttpRequestMessage(new HttpMethod(method), Target.ApiRepositoryUri.AbsoluteUri + suffix);
		await Assert.ThrowsAsync<SourceControlSecurityException>(() => client.SendAsync(request, TestContext.Current.CancellationToken));
		Assert.Equal(0, resolutions);
	}

	[Theory]
	[InlineData("repository")]
	[InlineData("project")]
	public void Recreated_names_do_not_replace_the_accepted_stable_repository_identity(string changed)
	{
		var envelope = Payload("active").Deserialize<Dictionary<string, JsonElement>>()!;
		var repository = envelope["repository"].Deserialize<Dictionary<string, JsonElement>>()!;
		if (changed == "repository")
		{
			repository["id"] = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D"));
		}
		else
		{
			var project = repository["project"].Deserialize<Dictionary<string, JsonElement>>()!;
			project["id"] = JsonSerializer.SerializeToElement(Guid.NewGuid().ToString("D"));
			repository["project"] = JsonSerializer.SerializeToElement(project);
		}
		envelope["repository"] = JsonSerializer.SerializeToElement(repository);
		Assert.Equal(SourceControlErrorCode.ContentUnsafe, Assert.Throws<SourceControlSecurityException>(() =>
			AzureDevOpsPullRequestResponse.Read(JsonSerializer.SerializeToElement(envelope), Target, Command)).Code);
	}

	[Fact]
	public async Task Lost_create_is_reconciled_with_GET_details_even_when_list_descriptions_are_truncated()
	{
		using var handler = new ScriptedHandler(request =>
		{
			if (request.Method == HttpMethod.Post)
			{ throw new HttpRequestException("synthetic reply loss"); }
			return request.RequestUri!.AbsolutePath.EndsWith("/pullrequests", StringComparison.Ordinal)
				? Response(JsonSerializer.SerializeToElement(new { count = 1, value = new[] { new { pullRequestId = 17, description = "truncated" } } }))
				: Response(Payload("active"));
		});
		using var client = new HttpClient(handler);
		using var credential = new HostingHttpCredentialLease("Bearer", "synthetic-token", DateTimeOffset.UtcNow.AddMinutes(2));
		var transport = new AzureDevOpsPullRequestTransport(client, Target, credential);
		Assert.Equal(SourceControlErrorCode.ResultUnknown, (await Assert.ThrowsAsync<SourceControlSecurityException>(() => transport.CreateAsync(Command, TestContext.Current.CancellationToken))).Code);
		Assert.Equal(17, (await transport.ReconcileAsync(Command, TestContext.Current.CancellationToken)).Number);
		Assert.Equal(1, handler.Writes);
		Assert.Equal(2, handler.Reads);
	}

	[Fact]
	public async Task Duplicate_operation_markers_never_authorize_a_second_create()
	{
		using var handler = new ScriptedHandler(request =>
		{
			if (request.RequestUri!.AbsolutePath.EndsWith("/pullrequests", StringComparison.Ordinal))
			{ return Response(JsonSerializer.SerializeToElement(new { count = 2, value = new[] { new { pullRequestId = 17 }, new { pullRequestId = 18 } } })); }
			var number = request.RequestUri.AbsolutePath.EndsWith("/18", StringComparison.Ordinal) ? 18 : 17;
			return Response(Payload("active", number));
		});
		using var client = new HttpClient(handler);
		using var credential = new HostingHttpCredentialLease("Basic", "c3ludGhldGljOnRva2Vu", DateTimeOffset.UtcNow.AddMinutes(2));
		var transport = new AzureDevOpsPullRequestTransport(client, Target, credential);
		Assert.Equal(SourceControlErrorCode.ResultUnknown, (await Assert.ThrowsAsync<SourceControlSecurityException>(() => transport.ReconcileAsync(Command, TestContext.Current.CancellationToken))).Code);
		Assert.Equal(0, handler.Writes);
	}

	private static JsonElement Payload(string state, int number = 17) => JsonSerializer.SerializeToElement(new
	{
		repository = new
		{
			id = "b38e16b1-e292-45c3-b697-a4a6f37a825f",
			name = "repository",
			url = Target.ApiRepositoryUri.AbsoluteUri,
			project = new { id = "d3321197-7da9-46ed-a7ec-5d5af8d0d73c", name = "project" },
			remoteUrl = Binding.Remote.AbsoluteUri
		},
		pullRequestId = number,
		status = state,
		mergeStatus = "succeeded",
		sourceRefName = "refs/heads/" + Command.WorkBranch,
		targetRefName = "refs/heads/" + Command.BaseBranch,
		title = Command.Title,
		description = AzureDevOpsPullRequestResponse.Body(Command),
		lastMergeSourceCommit = new { commitId = Command.HeadCommit.Value },
		lastMergeCommit = new { commitId = new string('b', 40) },
		url = Target.ApiRepositoryUri.AbsoluteUri + "/pullRequests/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture),
		reviewers = new[] { new { id = "6ca25b4f-e58d-4090-91ae-d4c51cb01652", displayName = "Reviewer", vote = 10 } }
	});
	private static HttpResponseMessage Response(JsonElement payload) => new(HttpStatusCode.OK) { Content = JsonContent.Create(payload) };
	private sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
	{
		internal int Writes { get; private set; }
		internal int Reads { get; private set; }
		protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			if (request.Method == HttpMethod.Post)
			{ Writes++; }
			else
			{ Reads++; }
			return Task.FromResult(respond(request));
		}
	}
}
