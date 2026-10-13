namespace VertexBPMN.Studio;

internal static class StudioAuthenticationRedirects
{
	public static string LocalTarget(string? returnUrl, string fallback) =>
		!string.IsNullOrWhiteSpace(returnUrl) && returnUrl[0] == '/'
		&& (returnUrl.Length == 1 || (returnUrl[1] != '/' && returnUrl[1] != '\\'))
		&& !returnUrl.Contains('\r', StringComparison.Ordinal) && !returnUrl.Contains('\n', StringComparison.Ordinal)
			? returnUrl : fallback;
}
