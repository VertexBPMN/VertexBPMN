using System.Net.Http.Headers;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class GitHubTokenLease(string token, DateTimeOffset expiresAt) : IDisposable
{
    private char[]? _token = token.ToCharArray();
    internal DateTimeOffset ExpiresAt { get; } = expiresAt;
    internal AuthenticationHeaderValue Authorization()
    {
        if (_token is null || ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}

		return new("Bearer", new string(_token));
    }
    public void Dispose()
    {
        if (_token is not null)
		{
			Array.Clear(_token);
		}

		_token = null;
    }
    public override string ToString() => "GitHub installation token (redacted)";
}
