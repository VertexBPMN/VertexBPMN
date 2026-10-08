using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Unknown writes are read-only reconciled; absence never authorizes another create.</summary>
internal static class GitHubPullRequestReconciliation
{
	internal static string OperationMarker(PullRequestCommand command) =>
		$"<!-- vertexbpmn-pr-operation:{command.OperationId:N} -->";

	internal static string RequestBody(PullRequestCommand command) =>
		$"{command.Description}\n\n{OperationMarker(command)}";

	// The caller supplies all fetched PR detail responses, not incomplete list entries.
	internal static PullRequestReceipt AfterUnknownWrite(IReadOnlyCollection<JsonElement> details,
		RepositoryBinding binding, PullRequestCommand command)
	{
		PullRequestReceipt? found = null;
		foreach (var detail in details)
		{
			if (detail.ValueKind != JsonValueKind.Object)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
			}
			if (!detail.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.String
				|| !body.GetString()!.Split('\n').Any(line => line.TrimEnd('\r') == OperationMarker(command)))
			{
				continue;
			}
			var candidate = GitHubPullRequestResponse.Read(detail, binding, command);
			if (found is not null)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
			}
			found = candidate;
		}
		return found ?? throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
	}
}
