using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class GitSshCredentialLease : IDisposable
{
	private char[]? key;
	internal string Username { get; }
	internal Uri Remote { get; }
	internal DateTimeOffset ExpiresAt { get; }
	internal GitSshCredentialLease(Uri remote, string username, string privateKey, DateTimeOffset expiresAt)
	{
		if (string.IsNullOrEmpty(username) || username.Length > 64 || username[0] == '-'
			|| username.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.'))
			|| privateKey.Length is < 128 or > 65_536 || !privateKey.StartsWith("-----BEGIN OPENSSH PRIVATE KEY-----\n", StringComparison.Ordinal)
			|| !privateKey.TrimEnd('\n').EndsWith("-----END OPENSSH PRIVATE KEY-----", StringComparison.Ordinal)
			|| expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		GitSshConfigurationPolicy.ValidateScope(remote);
		Remote = remote;
		Username = username;
		key = privateKey.ToCharArray();
		ExpiresAt = expiresAt;
	}
	internal byte[] CopyKey(Uri remote)
	{
		if (key is null || remote.AbsoluteUri != Remote.AbsoluteUri || ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		return Encoding.UTF8.GetBytes(key);
	}
	public void Dispose()
	{
		if (key is not null)
		{ Array.Clear(key); key = null; }
	}
	public override string ToString() => "SSH private key (redacted)";
}
