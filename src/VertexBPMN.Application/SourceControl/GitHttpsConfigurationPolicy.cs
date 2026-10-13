using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

/// <summary>Pure validation and immutable snapshots of deployment-owned HTTPS policies.</summary>
public static class GitHttpsConfigurationPolicy
{
	public static void Validate(SourceControlOptions options)
	{
		if (options.HttpsNetworkPolicies is null || options.HttpsTrustProfiles is null
			|| options.HttpsNetworkPolicies.Count > 128 || options.HttpsTrustProfiles.Count > 128)
		{
			Reject();
		}
		var networks = new HashSet<(string Tenant, string Id)>();
		foreach (var policy in options.HttpsNetworkPolicies)
		{
			var snapshot = Snapshot(policy);
			if (!networks.Add((snapshot.TenantId, snapshot.Id)))
			{
				Reject();
			}
		}
		var trusts = new HashSet<(string Tenant, string Id)>();
		foreach (var profile in options.HttpsTrustProfiles)
		{
			var snapshot = Snapshot(profile);
			if (!trusts.Add((snapshot.TenantId, snapshot.Id)))
			{
				Reject();
			}
		}
	}

	public static GitHttpsNetworkPolicy Snapshot(GitHttpsNetworkPolicy policy)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ValidateIdentity(policy.Id, policy.TenantId, policy.Revision, policy.RepositoryScope);
		return policy with
		{
			AllowedAddressRanges = CopyRanges(policy.AllowedAddressRanges),
			DeniedAddressRanges = CopyRanges(policy.DeniedAddressRanges)
		};
	}

	public static GitHttpsTrustProfile Snapshot(GitHttpsTrustProfile profile)
	{
		ArgumentNullException.ThrowIfNull(profile);
		ValidateIdentity(profile.Id, profile.TenantId, profile.Revision, profile.RepositoryScope);
		ValidateCertificateAuthorities(profile.CertificateAuthorityPem);
		return profile;
	}

	private static void ValidateIdentity(string id, string tenant, long revision, Uri scope)
	{
		if (!ValidReference(id, 240) || !ValidReference(tenant, 64) || tenant == "$global" || revision <= 0)
		{
			Reject();
		}
		RepositoryConnectionProfilePolicy.ValidateHttpsRepositoryScope(scope);
	}

	private static bool ValidReference(string value, int limit) => !string.IsNullOrWhiteSpace(value)
		&& value.Length <= limit && value == value.Trim() && !value.Any(char.IsControl);

	private static System.Collections.ObjectModel.ReadOnlyCollection<string> CopyRanges(IReadOnlyList<string> ranges)
	{
		if (ranges is null || ranges.Count > 64)
		{
			Reject();
		}
		var copy = ranges.ToArray();
		if (copy.Any(range => string.IsNullOrWhiteSpace(range) || range != range.Trim()
			|| !IPNetwork.TryParse(range, out _)))
		{
			Reject();
		}
		return Array.AsReadOnly(copy);
	}

	private static void ValidateCertificateAuthorities(string pem)
	{
		if (string.IsNullOrWhiteSpace(pem) || pem.Length > 64 * 1024)
		{
			Reject();
		}
		try
		{
			var remaining = pem.AsSpan();
			var count = 0;
			while (!remaining.Trim().IsEmpty)
			{
				if (++count > 16)
				{
					Reject();
				}
				if (!PemEncoding.TryFind(remaining, out var fields)
					|| !remaining[fields.Label].SequenceEqual("CERTIFICATE")
					|| !remaining[..fields.Location.Start.GetOffset(remaining.Length)].Trim().IsEmpty)
				{
					Reject();
				}
				using var certificate = X509CertificateLoader.LoadCertificate(
					Convert.FromBase64String(remaining[fields.Base64Data].ToString()));
				var constraints = certificate.Extensions.OfType<X509BasicConstraintsExtension>().SingleOrDefault();
				var usage = certificate.Extensions.OfType<X509KeyUsageExtension>().SingleOrDefault();
				if (certificate.HasPrivateKey || constraints is not { CertificateAuthority: true }
					|| (usage is not null && (usage.KeyUsages & X509KeyUsageFlags.KeyCertSign) == X509KeyUsageFlags.None))
				{
					Reject();
				}
				remaining = remaining[fields.Location.End.GetOffset(remaining.Length)..];
			}
		}
		catch (Exception error) when (error is CryptographicException or FormatException or InvalidOperationException)
		{
			Reject();
		}
	}

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
