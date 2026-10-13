using System.Globalization;
using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal static class AzureDevOpsPullRequestResponse
{
	internal static string Marker(PullRequestCommand command) => $"<!-- vertexbpmn-pr-operation:{command.OperationId:N} -->";
	internal static string Body(PullRequestCommand command) => command.Description + "\n\n" + Marker(command);

	internal static PullRequestReceipt Read(JsonElement payload, RepositoryHostingTarget target, PullRequestCommand command)
	{
		try
		{
			var identity = target.RepositoryId.Split('/');
			var remotePath = target.WebRepositoryUri.AbsolutePath.Split('/');
			var repository = payload.GetProperty("repository");
			var number = payload.GetProperty("pullRequestId").GetInt64();
			var description = payload.GetProperty("description").GetString();
			var api = payload.GetProperty("url").GetString();
			var suffix = number.ToString(CultureInfo.InvariantCulture);
			if (identity.Length != 3 || !Guid.TryParseExact(repository.GetProperty("id").GetString(), "D", out var id) || id == Guid.Empty
				|| id.ToString("D") != identity[2] || repository.GetProperty("project").GetProperty("id").GetString() != identity[1]
				|| remotePath.Length != 5 || repository.GetProperty("name").GetString() != Uri.UnescapeDataString(remotePath[4])
				|| repository.GetProperty("project").GetProperty("name").GetString() != Uri.UnescapeDataString(remotePath[2])
				|| repository.GetProperty("remoteUrl").GetString() != target.WebRepositoryUri.AbsoluteUri
				|| !RepositoryApiMatches(repository, target, id, api, suffix)
				|| payload.GetProperty("sourceRefName").GetString() != "refs/heads/" + command.WorkBranch
				|| payload.GetProperty("targetRefName").GetString() != "refs/heads/" + command.BaseBranch
				|| payload.GetProperty("title").GetString() != command.Title
				|| description != Body(command))
			{ Unsafe(); }
			var head = new GitCommitId(payload.GetProperty("lastMergeSourceCommit").GetProperty("commitId").GetString()!);
			if (head != command.HeadCommit)
			{ Unsafe(); }
			var state = payload.GetProperty("status").GetString() switch
			{
				"active" => PullRequestState.Open,
				"abandoned" => PullRequestState.Closed,
				"completed" => PullRequestState.Merged,
				_ => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe)
			};
			GitCommitId? merge = null;
			if (state == PullRequestState.Merged)
			{
				if (payload.GetProperty("mergeStatus").GetString() != "succeeded")
				{ Unsafe(); }
				merge = new(payload.GetProperty("lastMergeCommit").GetProperty("commitId").GetString()!);
			}
			var receipt = new PullRequestReceipt(command.OperationId, target.ProviderId, number,
				new(target.WebRepositoryUri.AbsoluteUri + "/pullrequest/" + suffix), state, head, merge)
			{ CurrentVotes = ReadVotes(payload) };
			RepositoryHostingReceiptPolicy.Demand(target, command, receipt);
			return receipt;
		}
		catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
	}

	private static System.Collections.ObjectModel.ReadOnlyCollection<PullRequestVote> ReadVotes(JsonElement payload)
	{
		var reviewers = payload.GetProperty("reviewers");
		if (reviewers.ValueKind != JsonValueKind.Array || reviewers.GetArrayLength() > 1000)
		{ Unsafe(); }
		var votes = new List<PullRequestVote>();
		var seen = new HashSet<Guid>();
		foreach (var reviewer in reviewers.EnumerateArray())
		{
			var name = reviewer.GetProperty("displayName").GetString();
			var vote = reviewer.GetProperty("vote").GetInt32();
			if (!Guid.TryParseExact(reviewer.GetProperty("id").GetString(), "D", out var id) || id == Guid.Empty || !seen.Add(id)
				|| string.IsNullOrWhiteSpace(name) || name.Length > 512 || name.Any(char.IsControl)
				|| vote is not (-10 or -5 or 0 or 5 or 10))
			{ Unsafe(); }
			votes.Add(new(id.ToString("D"), name, vote));
		}
		return votes.AsReadOnly();
	}

	private static bool RepositoryApiMatches(JsonElement repository, RepositoryHostingTarget target, Guid id, string? api, string number)
	{
		var identity = target.RepositoryId.Split('/');
		var projectId = repository.GetProperty("project").GetProperty("id").GetString();
		if (!Guid.TryParseExact(projectId, "D", out var project) || project == Guid.Empty)
		{ return false; }
		var root = "https://dev.azure.com/" + identity[0];
		var candidates = new[] { target.ApiRepositoryUri.AbsoluteUri,
			root + "/" + project.ToString("D") + "/_apis/git/repositories/" + id.ToString("D"),
			root + "/_apis/git/repositories/" + id.ToString("D") };
		var repositoryApi = repository.GetProperty("url").GetString();
		return candidates.Contains(repositoryApi, StringComparer.Ordinal)
			&& (api == repositoryApi + "/pullRequests/" + number || api == repositoryApi + "/pullrequests/" + number);
	}

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Unsafe() => throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe);
}
