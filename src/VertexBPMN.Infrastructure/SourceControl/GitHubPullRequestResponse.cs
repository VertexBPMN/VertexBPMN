using System.Globalization;
using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Validates hosting responses against the accepted operation; never grants release approval.</summary>
internal static class GitHubPullRequestResponse
{
    internal static PullRequestReceipt Read(JsonElement response, RepositoryBinding binding,
        PullRequestCommand command)
    {
        ArgumentNullException.ThrowIfNull(binding);
        ArgumentNullException.ThrowIfNull(command);
        try
        {
            var repository = binding.Remote.AbsolutePath.Trim('/');
            if (repository.EndsWith(".git", StringComparison.Ordinal))
            {
                repository = repository[..^4];
            }
            if (binding.Remote.Scheme != Uri.UriSchemeHttps || binding.Remote.Host != "github.com"
                || repository.Split('/').Length != 2 || !string.IsNullOrEmpty(binding.Remote.Query)
                || !string.IsNullOrEmpty(binding.Remote.Fragment) || !string.IsNullOrEmpty(binding.Remote.UserInfo))
            {
                throw InvalidResponse();
            }

            var head = response.GetProperty("head");
            var target = response.GetProperty("base");
            var commit = new GitCommitId(head.GetProperty("sha").GetString()!);
            if (!string.Equals(head.GetProperty("repo").GetProperty("full_name").GetString(), repository, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(target.GetProperty("repo").GetProperty("full_name").GetString(), repository, StringComparison.OrdinalIgnoreCase)
                || head.GetProperty("ref").GetString() != command.WorkBranch
                || target.GetProperty("ref").GetString() != command.BaseBranch || commit != command.HeadCommit)
            {
                throw InvalidResponse();
            }

            var number = response.GetProperty("number").GetInt64();
            if (number <= 0)
            {
                throw InvalidResponse();
            }
            var url = new Uri(response.GetProperty("html_url").GetString()!, UriKind.Absolute);
            var expectedPath = $"/{repository}/pull/{number.ToString(CultureInfo.InvariantCulture)}";
            if (url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com" || !url.IsDefaultPort
                || !string.IsNullOrEmpty(url.UserInfo) || !string.IsNullOrEmpty(url.Query)
                || !string.IsNullOrEmpty(url.Fragment) || !string.Equals(url.AbsolutePath, expectedPath, StringComparison.OrdinalIgnoreCase))
            {
                throw InvalidResponse();
            }

            var state = response.GetProperty("state").GetString();
            var merged = response.GetProperty("merged").GetBoolean();
            if (state is not ("open" or "closed") || (merged && state != "closed"))
            {
                throw InvalidResponse();
            }
            // GitHub also exposes a speculative merge SHA on open PRs. Never
            // treat that SHA as the actual merged revision or release evidence.
            var mergeCommit = merged ? new GitCommitId(response.GetProperty("merge_commit_sha").GetString()!) : null;
            return new PullRequestReceipt(command.OperationId, "github", number, url,
                merged ? PullRequestState.Merged : state == "open" ? PullRequestState.Open : PullRequestState.Closed,
                commit, mergeCommit);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException or FormatException)
        {
            throw InvalidResponse();
        }
    }

    private static SourceControlSecurityException InvalidResponse() =>
        new(SourceControlErrorCode.ProviderUnavailable);
}
