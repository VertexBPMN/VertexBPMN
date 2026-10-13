using System.Net.Http.Headers;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class HostingHttpCredentialLease : IDisposable
{
	private char[]? value;
	private readonly string scheme;
	internal DateTimeOffset ExpiresAt { get; }

	internal HostingHttpCredentialLease(string scheme, string encodedValue, DateTimeOffset expiresAt)
	{
		if (scheme is not ("Bearer" or "Basic") || string.IsNullOrWhiteSpace(encodedValue) || encodedValue.Length > 32_768
			|| encodedValue.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
			|| expiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		this.scheme = scheme;
		value = encodedValue.ToCharArray();
		ExpiresAt = expiresAt;
	}

	internal AuthenticationHeaderValue Authorization()
	{
		if (value is null || ExpiresAt <= DateTimeOffset.UtcNow.AddSeconds(30))
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.CredentialUnavailable);
		}
		return new(scheme, new string(value));
	}

	public void Dispose()
	{
		if (value is not null)
		{ Array.Clear(value); value = null; }
	}
	public override string ToString() => "Hosting credential (redacted)";
}
