using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

public sealed class GitHubPullRequestResponseTests
{
    private static readonly GitCommitId Head = new(new string('a', 40));
    private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), "tenant-a",
        new Uri("https://github.com/VertexBPMN/acceptance.git"), "credential", "master", "release", ["models"]);
    private static readonly PullRequestCommand Command = new(Guid.NewGuid(), new("pr-test-key"),
        "vertex/work", "master", Head, "Review model", "Accepted snapshot");

    [Theory]
    [InlineData("open", false, PullRequestState.Open)]
    [InlineData("closed", false, PullRequestState.Closed)]
    [InlineData("closed", true, PullRequestState.Merged)]
    public void Read_BindsReceiptAndOnlyExposesActualMerge(string state, bool merged, PullRequestState expected)
    {
        var result = GitHubPullRequestResponse.Read(Response(state: state, merged: merged), Binding, Command);
        Assert.Equal(expected, result.State);
        Assert.Equal(Command.OperationId, result.OperationId);
        Assert.Equal(Head, result.HeadCommit);
        Assert.Equal(merged ? new GitCommitId(new string('b', 40)) : null, result.MergeCommit);
    }

    [Theory]
    [InlineData("other/repo", "vertex/work", "master", "https://github.com/VertexBPMN/acceptance/pull/7")]
    [InlineData("VertexBPMN/acceptance", "vertex/other", "master", "https://github.com/VertexBPMN/acceptance/pull/7")]
    [InlineData("VertexBPMN/acceptance", "vertex/work", "release", "https://github.com/VertexBPMN/acceptance/pull/7")]
    [InlineData("VertexBPMN/acceptance", "vertex/work", "master", "https://evil.invalid/pull/7")]
    public void Read_RejectsForeignContext(string repository, string branch, string target, string url)
    {
        Assert.Throws<SourceControlSecurityException>(() => GitHubPullRequestResponse.Read(
            Response(repository, branch, target, url), Binding, Command));
    }

    [Fact]
    public void Read_RejectsChangedHeadAndInconsistentState()
    {
        Assert.Throws<SourceControlSecurityException>(() => GitHubPullRequestResponse.Read(
            Response(), Binding, Command with { HeadCommit = new GitCommitId(new string('c', 40)) }));
        Assert.Throws<SourceControlSecurityException>(() => GitHubPullRequestResponse.Read(
            Response(merged: true), Binding, Command));
    }

    private static JsonElement Response(string repository = "VertexBPMN/acceptance", string branch = "vertex/work",
        string target = "master", string url = "https://github.com/VertexBPMN/acceptance/pull/7",
        string state = "open", bool merged = false) => JsonSerializer.SerializeToElement(new
        {
            number = 7, html_url = url, state, merged, merge_commit_sha = new string('b', 40),
            head = new { sha = Head.Value, @ref = branch, repo = new { full_name = repository } },
            @base = new { @ref = target, repo = new { full_name = "VertexBPMN/acceptance" } }
        });
}
