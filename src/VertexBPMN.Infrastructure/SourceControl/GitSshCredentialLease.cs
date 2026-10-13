using System.Text;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class GitSshCredentialLease : IDisposable
{
	private char[]? key;
	private char[]? passphrase;
	internal bool HasPassphrase => passphrase is not null;
	internal string Username { get; }
	internal Uri Remote { get; }
	internal DateTimeOffset ExpiresAt { get; }
	internal GitSshCredentialLease(Uri remote, string username, string privateKey, DateTimeOffset expiresAt, string? keyPassphrase = null)
	{
		if (string.IsNullOrEmpty(username) || username.Length > 64 || username[0] == '-'
			|| username.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '.'))
			|| privateKey.Length is < 128 or > 65_536 || !privateKey.StartsWith("-----BEGIN OPENSSH PRIVATE KEY-----\n", StringComparison.Ordinal)
			|| !privateKey.TrimEnd('\n').EndsWith("-----END OPENSSH PRIVATE KEY-----", StringComparison.Ordinal)
			|| expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30)
			|| (keyPassphrase is not null && (keyPassphrase.Length is < 1 or > 4096 || keyPassphrase.Any(char.IsControl))))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		GitSshConfigurationPolicy.ValidateScope(remote);
		Remote = remote;
		Username = username;
		key = privateKey.ToCharArray();
		passphrase = keyPassphrase?.ToCharArray();
		ExpiresAt = expiresAt;
	}
	internal byte[] CopyKey(Uri remote)
	{
		if (key is null || remote.AbsoluteUri != Remote.AbsoluteUri || ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{ throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable); }
		return Encoding.UTF8.GetBytes(key);
	}
	internal async Task WritePassphraseAsync(TextWriter writer, CancellationToken cancellationToken)
	{
		if (key is null || passphrase is null || ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		await writer.WriteLineAsync(passphrase.AsMemory(), cancellationToken);
	}
	public void Dispose()
	{
		if (key is not null)
		{ Array.Clear(key); key = null; }
		if (passphrase is not null)
		{ Array.Clear(passphrase); passphrase = null; }
	}
	public override string ToString() => "SSH private key (redacted)";
}
