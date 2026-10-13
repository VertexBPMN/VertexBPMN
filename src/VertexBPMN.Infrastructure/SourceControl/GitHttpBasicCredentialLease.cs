using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>
/// Short-lived, exact-repository-bound Basic/PAT credentials. Caller resolves the
/// tenant credential and current grants; this lease neither resolves nor caches them.
/// </summary>
internal sealed class GitHttpBasicCredentialLease : IGitCredentialLease
{
	private readonly Uri _remote;
	private readonly string _username;
	private readonly DateTimeOffset _expiresAt;
	private char[]? _password;

	internal GitHttpBasicCredentialLease(Uri remote, string username, string password, DateTimeOffset expiresAt)
	{
		ArgumentNullException.ThrowIfNull(remote);
		if (!remote.IsAbsoluteUri || remote.Scheme != Uri.UriSchemeHttps
			|| remote.UserInfo.Length != 0 || remote.Query.Length != 0 || remote.Fragment.Length != 0
			|| string.IsNullOrWhiteSpace(username) || username.Length > 128 || username != username.Trim()
			|| username.Any(char.IsControl) || username.Contains(':', StringComparison.Ordinal)
			|| string.IsNullOrEmpty(password) || password.Length > 16 * 1024
			|| password.Any(char.IsControl) || expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)
			|| expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}

		_remote = remote;
		_username = username;
		_password = password.ToCharArray();
		_expiresAt = expiresAt;
	}

	public async Task WriteCredentialAsync(Uri remote, StreamWriter writer, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(remote);
		ArgumentNullException.ThrowIfNull(writer);
		if (!remote.IsAbsoluteUri || _password is null || _expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)
			|| remote.AbsoluteUri != _remote.AbsoluteUri)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}

		cancellationToken.ThrowIfCancellationRequested();
		await writer.WriteLineAsync(_username.AsMemory(), cancellationToken);
		await writer.WriteLineAsync(_password.AsMemory(), cancellationToken);
	}

	public void Dispose()
	{
		if (_password is not null)
		{
			Array.Clear(_password);
		}

		_password = null;
	}

	public override string ToString() => "Git HTTP Basic credential (redacted)";
}
