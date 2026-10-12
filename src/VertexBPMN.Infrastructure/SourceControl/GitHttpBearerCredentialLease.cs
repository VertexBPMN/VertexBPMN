using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Bearer material is released only through capability-negotiated, repository-bound IPC.</summary>
internal sealed class GitHttpBearerCredentialLease : IGitCredentialLease
{
	internal const string ProtocolMarker = ":vertex-bearer-v1:";
	private readonly Uri acceptedRemote;
	private readonly DateTimeOffset expiresAt;
	private char[]? token;

	internal GitHttpBearerCredentialLease(Uri remote, string token, DateTimeOffset expiresAt)
	{
		if (!remote.IsAbsoluteUri || remote.Scheme != Uri.UriSchemeHttps || remote.UserInfo.Length != 0
			|| remote.Query.Length != 0 || remote.Fragment.Length != 0 || string.IsNullOrWhiteSpace(token)
			|| token.Length > 16 * 1024 || token.Any(character => char.IsControl(character) || char.IsWhiteSpace(character))
			|| expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30) || expiresAt > DateTimeOffset.UtcNow.AddMinutes(5))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		acceptedRemote = remote;
		this.token = token.ToCharArray();
		this.expiresAt = expiresAt;
	}

	public async Task WriteCredentialAsync(Uri remote, StreamWriter writer, CancellationToken cancellationToken)
	{
		if (token is null || remote.AbsoluteUri != acceptedRemote.AbsoluteUri || expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		await writer.WriteLineAsync(ProtocolMarker.AsMemory(), cancellationToken);
		await writer.WriteLineAsync(token.AsMemory(), cancellationToken);
	}

	public void Dispose()
	{
		if (token is not null)
		{
			Array.Clear(token);
		}
		token = null;
	}

	public override string ToString() => "Git HTTP Bearer credential (redacted)";
}
