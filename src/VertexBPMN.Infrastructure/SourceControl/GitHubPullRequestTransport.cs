using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Bounded single-attempt HTTP operations. Durable orchestration owns retries and reconciliation.</summary>
internal sealed class GitHubPullRequestTransport(HttpClient client)
{
	private const int MaximumResponseBytes = 1024 * 1024;

	internal async Task<PullRequestReceipt> ReconcileAsync(RepositoryBinding binding, PullRequestCommand command,
		GitHubTokenLease token, CancellationToken cancellationToken)
	{
		var details = new List<JsonElement>();
		var seen = new HashSet<long>();
		var path = RepositoryPath(binding);
		for (var page = 1; page <= 10; page++)
		{
			using var request = Request(HttpMethod.Get, path + "/pulls?state=all&per_page=100&page="
				+ page.ToString(CultureInfo.InvariantCulture), token);
			var list = await SendAsync(request, cancellationToken);
			if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() > 100)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
			}
			foreach (var item in list.EnumerateArray())
			{
				if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("number", out var numberElement)
					|| numberElement.ValueKind != JsonValueKind.Number
					|| !numberElement.TryGetInt64(out var number) || number <= 0 || !seen.Add(number))
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
				}
				if (!item.TryGetProperty("body", out var body) || body.ValueKind != JsonValueKind.String
					|| !body.GetString()!.Split('\n').Any(line => line.TrimEnd('\r') == GitHubPullRequestReconciliation.OperationMarker(command)))
				{
					continue;
				}
				using var detailRequest = Request(HttpMethod.Get, path + "/pulls/" + number.ToString(CultureInfo.InvariantCulture), token);
				var detail = await SendAsync(detailRequest, cancellationToken);
				var receipt = GitHubPullRequestResponse.Read(detail, binding, command);
				if (receipt.Number != number) throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
				details.Add(detail);
			}
			if (list.GetArrayLength() < 100)
			{
				return GitHubPullRequestReconciliation.AfterUnknownWrite(details, binding, command);
			}
		}
		// A truncated search cannot establish uniqueness, even if an earlier page matched.
		throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
	}

	internal async Task<PullRequestReceipt> CreateAsync(RepositoryBinding binding, PullRequestCommand command,
		GitHubTokenLease token, CancellationToken cancellationToken)
	{
		using var request = Request(HttpMethod.Post, RepositoryPath(binding) + "/pulls", token);
		request.Content = JsonContent.Create(new { title = command.Title, head = command.WorkBranch,
			@base = command.BaseBranch, body = GitHubPullRequestReconciliation.RequestBody(command) });
		// Any failure after sending may follow a server-side write; never retry POST here.
		try
		{
			var response = await SendAsync(request, cancellationToken);
			return GitHubPullRequestResponse.Read(response, binding, command);
		}
		catch (Exception error) when (error is HttpRequestException or OperationCanceledException or SourceControlSecurityException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
		}
	}

	internal async Task<PullRequestReceipt> ReadAsync(RepositoryBinding binding, PullRequestCommand command,
		long number, GitHubTokenLease token, CancellationToken cancellationToken)
	{
		if (number <= 0)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		using var request = Request(HttpMethod.Get, RepositoryPath(binding) + "/pulls/" + number.ToString(CultureInfo.InvariantCulture), token);
		var response = await SendAsync(request, cancellationToken);
		var receipt = GitHubPullRequestResponse.Read(response, binding, command);
		if (receipt.Number != number)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		return receipt;
	}

	private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
			}
			await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			using var buffer = new MemoryStream();
			var chunk = new byte[8192];
			int count;
			while ((count = await stream.ReadAsync(chunk, cancellationToken)) != 0)
			{
				if (buffer.Length + count > MaximumResponseBytes)
				{
					throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
				}
				buffer.Write(chunk, 0, count);
			}
			using var document = JsonDocument.Parse(buffer.ToArray());
			return document.RootElement.Clone();
		}
		catch (Exception error) when (error is HttpRequestException or JsonException or IOException)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
	}

	private static HttpRequestMessage Request(HttpMethod method, string path, GitHubTokenLease token)
	{
		var request = new HttpRequestMessage(method, "https://api.github.com/repos/" + path);
		request.Headers.Authorization = token.Authorization();
		request.Headers.UserAgent.ParseAdd("VertexBPMN/1.0");
		request.Headers.Accept.ParseAdd("application/vnd.github+json");
		request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
		return request;
	}

	private static string RepositoryPath(RepositoryBinding binding)
	{
		SourceControlHttps.ValidateTarget(binding.Remote, ["github.com"]);
		var path = binding.Remote.AbsolutePath.Trim('/');
		if (path.EndsWith(".git", StringComparison.Ordinal)) path = path[..^4];
		var parts = path.Split('/');
		if (parts.Length != 2 || parts.Any(part => part.Length == 0 || part is "." or ".."
			|| part.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.'))))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
		}
		return path;
	}
}
