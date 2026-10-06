using Microsoft.Extensions.Options;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Configuration gate, not a proof of executable version or private filesystem ACLs.</summary>
public sealed class SourceControlOptionsValidator : IValidateOptions<SourceControlOptions>
{
	public ValidateOptionsResult Validate(string? name, SourceControlOptions options)
	{
		if (!options.Enabled)
		{
			return ValidateOptionsResult.Success;
		}

		var failures = new List<string>();
		if (!SafeAbsolutePath(options.GitExecutablePath))
		{
			failures.Add("An absolute Git executable path is required.");
		}

		if (options.AuthHelperExecutablePath is not null && (!SafeAbsolutePath(options.AuthHelperExecutablePath)
			|| options.AuthHelperExecutablePath.Any(c => c is '\'' or '"' or '$' or '`')))
		{
			failures.Add("The optional authentication helper must be an absolute trusted executable path.");
		}

		if (!SafeAbsolutePath(options.WorkspaceRoot)
			|| string.Equals(Path.GetPathRoot(options.WorkspaceRoot), options.WorkspaceRoot, StringComparison.OrdinalIgnoreCase))
		{
			failures.Add("A dedicated absolute workspace directory is required.");
		}

		if (options.WorkBranchPrefix != "vertex/")
		{
			failures.Add("The work branch prefix must be vertex/.");
		}

		if (options.AllowedHosts is null || options.AllowedHosts.Count == 0
			|| options.AllowedHosts.Any(h => string.IsNullOrWhiteSpace(h) || h != h.Trim()
				|| h.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '.' and not '-')
				|| Uri.CheckHostName(h) != UriHostNameType.Dns || !h.Contains('.')
				|| h.EndsWith('.') || h.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
				|| h.EndsWith(".local", StringComparison.OrdinalIgnoreCase)))
		{
			failures.Add("Explicit canonical HTTPS DNS hosts are required; no wildcards, IPs, ports or URLs.");
		}

		var limits = options.Limits;
		if (limits is null || limits.MaxRepositoryBytes <= 0 || limits.MaxModelBytes <= 0
			|| limits.MaxModelFiles <= 0 || limits.MaxDiffBytes <= 0 || limits.MaxPageSize <= 0
			|| limits.MaxCommitFiles <= 0 || limits.MaxCommitMessageCharacters <= 0
			|| limits.MaxConcurrentJobsPerTenant <= 0 || limits.MaxConcurrentJobsTotal <= 0
			|| limits.MaxWorkspaceBytesTotal <= 0 || limits.ReadTimeout <= TimeSpan.Zero
			|| limits.WriteTimeout <= TimeSpan.Zero || limits.SessionIdleRetention <= TimeSpan.Zero
			|| limits.CompletedJobRetention <= TimeSpan.Zero)
		{
			failures.Add("All source control limits and timeouts must be positive.");
		}
		else if (limits.MaxModelBytes > limits.MaxRepositoryBytes || limits.MaxCommitFiles > limits.MaxModelFiles
			|| limits.MaxConcurrentJobsPerTenant > limits.MaxConcurrentJobsTotal
			|| limits.MaxRepositoryBytes > limits.MaxWorkspaceBytesTotal)
		{
			failures.Add("Source control limits must be internally consistent.");
		}

		return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
	}

	private static bool SafeAbsolutePath(string? path) => !string.IsNullOrWhiteSpace(path)
		&& path == path.Trim() && !path.Any(char.IsControl) && Path.IsPathFullyQualified(path)
		&& !path.StartsWith("\\", StringComparison.Ordinal)
		&& !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(p => p is "." or "..");
}
