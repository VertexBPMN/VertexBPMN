using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Single-attempt Azure writes; uncertain writes are reconciled by the durable worker.</summary>
internal sealed class AzureDevOpsPullRequestTransport(HttpClient client, RepositoryHostingTarget target, HostingHttpCredentialLease credential)
{
	internal async Task<PullRequestReceipt> CreateAsync(PullRequestCommand command, CancellationToken cancellationToken)
	{
		using var request = Request(HttpMethod.Post, "/pullrequests?api-version=7.1");
		request.Content = JsonContent.Create(new
		{
			sourceRefName = "refs/heads/" + command.WorkBranch,
			targetRefName = "refs/heads/" + command.BaseBranch,
			title = command.Title,
			description = AzureDevOpsPullRequestResponse.Body(command)
		});
		try
		{ return AzureDevOpsPullRequestResponse.Read(await SendAsync(request, cancellationToken), target, command); }
		catch (Exception error) when (error is HttpRequestException or OperationCanceledException or SourceControlSecurityException)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown); }
	}

	internal async Task<PullRequestReceipt> ReadAsync(PullRequestCommand command, long number, CancellationToken cancellationToken)
	{
		if (number <= 0)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput); }
		using var request = Request(HttpMethod.Get, "/pullrequests/" + number.ToString(CultureInfo.InvariantCulture) + "?api-version=7.1");
		var payload = await SendAsync(request, cancellationToken);
		var receipt = AzureDevOpsPullRequestResponse.Read(payload, target, command);
		if (receipt.Number != number)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
		if (payload.TryGetProperty("supportsIterations", out var supported) && supported.ValueKind == JsonValueKind.True)
		{
			receipt = receipt with { Iterations = await ReadIterationsAsync(command, number, cancellationToken) };
		}
		return receipt;
	}

	internal async Task<PullRequestReceipt> ReconcileAsync(PullRequestCommand command, CancellationToken cancellationToken)
	{
		PullRequestReceipt? found = null;
		var seen = new HashSet<long>();
		for (var skip = 0; skip < 1000; skip += 100)
		{
			var query = "/pullrequests?searchCriteria.status=all&searchCriteria.sourceRefName=" + Uri.EscapeDataString("refs/heads/" + command.WorkBranch)
				+ "&searchCriteria.targetRefName=" + Uri.EscapeDataString("refs/heads/" + command.BaseBranch)
				+ "&$top=100&$skip=" + skip.ToString(CultureInfo.InvariantCulture) + "&api-version=7.1";
			using var request = Request(HttpMethod.Get, query);
			var page = ReadArray(await SendAsync(request, cancellationToken), 100);
			foreach (var item in page.EnumerateArray())
			{
				if (item.ValueKind != JsonValueKind.Object || !item.TryGetProperty("pullRequestId", out var value)
					|| value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out var number) || number <= 0 || !seen.Add(number))
				{ throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown); }
				// List descriptions are truncated by Azure; always fetch the detail before marker checks.
				using var detailRequest = Request(HttpMethod.Get, "/pullrequests/" + number.ToString(CultureInfo.InvariantCulture) + "?api-version=7.1");
				var detail = await SendAsync(detailRequest, cancellationToken);
				if (!detail.TryGetProperty("description", out var body) || body.ValueKind != JsonValueKind.String
					|| !body.GetString()!.Split('\n').Any(line => line.TrimEnd('\r') == AzureDevOpsPullRequestResponse.Marker(command)))
				{ continue; }
				var candidate = AzureDevOpsPullRequestResponse.Read(detail, target, command);
				if (candidate.Number != number || found is not null)
				{ throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown); }
				found = candidate;
			}
			if (page.GetArrayLength() < 100)
			{ return found ?? throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown); }
		}
		throw new SourceControlSecurityException(SourceControlErrorCode.ResultUnknown);
	}

	private async Task<IReadOnlyList<PullRequestIteration>> ReadIterationsAsync(PullRequestCommand command, long number, CancellationToken cancellationToken)
	{
		using var request = Request(HttpMethod.Get, "/pullrequests/" + number.ToString(CultureInfo.InvariantCulture) + "/iterations?api-version=7.1");
		var values = ReadArray(await SendAsync(request, cancellationToken), 1000);
		var result = new List<PullRequestIteration>();
		var seen = new HashSet<long>();
		try
		{
			foreach (var item in values.EnumerateArray())
			{
				var id = item.GetProperty("id").GetInt64();
				if (id <= 0 || !seen.Add(id))
				{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
				result.Add(new(id, new(item.GetProperty("sourceRefCommit").GetProperty("commitId").GetString()!),
					new(item.GetProperty("targetRefCommit").GetProperty("commitId").GetString()!)));
			}
		}
		catch (Exception error) when (error is KeyNotFoundException or InvalidOperationException or ArgumentException or FormatException)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
		if (result.Count == 0 || result.MaxBy(item => item.Id)!.SourceCommit != command.HeadCommit)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
		return result.OrderBy(item => item.Id).ToArray();
	}

	private static JsonElement ReadArray(JsonElement payload, int maximum)
	{
		if (payload.ValueKind != JsonValueKind.Object || !payload.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array || value.GetArrayLength() > maximum
			|| !payload.TryGetProperty("count", out var count) || count.ValueKind != JsonValueKind.Number
			|| !count.TryGetInt32(out var size) || size != value.GetArrayLength())
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ContentUnsafe); }
		return value;
	}

	private HttpRequestMessage Request(HttpMethod method, string suffix)
	{
		var request = new HttpRequestMessage(method, target.ApiRepositoryUri.AbsoluteUri + suffix);
		AzureDevOpsHostingHttpPolicy.Demand(request, target.ApiRepositoryUri);
		request.Headers.Authorization = credential.Authorization();
		request.Headers.UserAgent.ParseAdd("VertexBPMN/1.0");
		return request;
	}

	private async Task<JsonElement> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		try
		{
			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
			if (!response.IsSuccessStatusCode || response.Headers.Contains("x-ms-continuationtoken"))
			{ throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
			await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
			await using var buffer = new MemoryStream();
			var chunk = new byte[8192];
			int read;
			while ((read = await stream.ReadAsync(chunk, cancellationToken)) != 0)
			{
				if (buffer.Length + read > 1024 * 1024)
				{ throw new SourceControlSecurityException(SourceControlErrorCode.PayloadTooLarge); }
				await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
			}
			using var document = JsonDocument.Parse(buffer.ToArray());
			return document.RootElement.Clone();
		}
		catch (Exception error) when (error is HttpRequestException or IOException or JsonException)
		{ throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable); }
	}
}
