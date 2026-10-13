using System.Globalization;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Allows only the PR operations implemented by the profile-bound GitHub adapter.</summary>
internal static class GitHubHostingHttpPolicy
{
	internal static void Demand(HttpRequestMessage request, Uri repository)
	{
		var target = request.RequestUri;
		if (target?.IsAbsoluteUri != true)
		{
			Reject();
		}
		SourceControlHttps.ValidateRequestTarget(target, ["api.github.com"]);
		SourceControlHttps.ValidateTarget(repository, ["api.github.com"]);
		var root = repository.AbsolutePath;
		var segments = root.Split('/');
		if (segments.Length != 4 || segments[1] != "repos"
			|| segments.Skip(2).Any(segment => segment.Length == 0 || segment is "." or ".."
				|| segment.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.')))
			|| target.IdnHost != repository.IdnHost || target.Port != repository.Port
			|| !target.AbsolutePath.StartsWith(root + "/pulls", StringComparison.Ordinal))
		{
			Reject();
		}
		var suffix = target.AbsolutePath[(root.Length + "/pulls".Length)..];
		if (suffix.Length == 0)
		{
			if ((request.Method == HttpMethod.Get && target.Query.Length > 0)
				|| (request.Method == HttpMethod.Post && target.Query.Length == 0))
			{
				return;
			}
			Reject();
		}
		var path = suffix.Split('/');
		if (request.Method != HttpMethod.Get || path.Length is < 2 or > 3 || path[0].Length != 0
			|| !long.TryParse(path[1], NumberStyles.None, CultureInfo.InvariantCulture, out var number) || number <= 0
			|| path[1] != number.ToString(CultureInfo.InvariantCulture)
			|| (path.Length == 2 && target.Query.Length != 0)
			|| (path.Length == 3 && (path[2] != "reviews" || target.Query.Length == 0)))
		{
			Reject();
		}
	}

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
