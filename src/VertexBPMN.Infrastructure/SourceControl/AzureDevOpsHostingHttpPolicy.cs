using System.Globalization;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal static class AzureDevOpsHostingHttpPolicy
{
	internal static void Demand(HttpRequestMessage request, Uri repository)
	{
		var target = request.RequestUri;
		if (target?.IsAbsoluteUri != true)
		{ Reject(); }
		SourceControlHttps.ValidateTarget(new UriBuilder(target) { Query = "" }.Uri, ["dev.azure.com"]);
		SourceControlHttps.ValidateTarget(repository, ["dev.azure.com"]);
		var prefix = repository.AbsolutePath + "/pullrequests";
		if (!target.AbsolutePath.StartsWith(prefix, StringComparison.Ordinal))
		{ Reject(); }
		var suffix = target.AbsolutePath[prefix.Length..];
		if (request.Method == HttpMethod.Post && suffix.Length == 0 && target.Query == "?api-version=7.1")
		{ return; }
		if (request.Method != HttpMethod.Get)
		{ Reject(); }
		if (suffix.Length != 0)
		{
			var parts = suffix.Split('/');
			if (parts.Length is < 2 or > 3 || parts[0].Length != 0
				|| !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0
				|| parts[1] != number.ToString(CultureInfo.InvariantCulture)
				|| (parts.Length == 3 && parts[2] != "iterations") || target.Query != "?api-version=7.1")
			{ Reject(); }
			return;
		}
		var query = target.Query.TrimStart('?').Split('&');
		if (query.Length != 6 || query[0] != "searchCriteria.status=all" || query[3] != "$top=100"
			|| query[5] != "api-version=7.1" || !query[4].StartsWith("$skip=", StringComparison.Ordinal)
			|| !int.TryParse(query[4][6..], NumberStyles.None, CultureInfo.InvariantCulture, out var skip)
			|| skip is < 0 or > 900 || skip % 100 != 0)
		{ Reject(); }
		DemandBranchQuery(query[1], "searchCriteria.sourceRefName=");
		DemandBranchQuery(query[2], "searchCriteria.targetRefName=");
	}

	private static void DemandBranchQuery(string value, string prefix)
	{
		if (!value.StartsWith(prefix, StringComparison.Ordinal))
		{ Reject(); }
		var reference = Uri.UnescapeDataString(value[prefix.Length..]);
		if (!reference.StartsWith("refs/heads/", StringComparison.Ordinal)
			|| value[prefix.Length..] != Uri.EscapeDataString(reference))
		{ Reject(); }
		SourceControlInputPolicy.ValidateBranch(reference["refs/heads/".Length..]);
	}

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
