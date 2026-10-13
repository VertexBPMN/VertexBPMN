using System.Net;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Application.SourceControl;

public static class GitSshConfigurationPolicy
{
	public static void Validate(SourceControlOptions options)
	{
		if (options.SshNetworkPolicies is null || options.SshTrustProfiles is null
			|| options.SshNetworkPolicies.Count > 128 || options.SshTrustProfiles.Count > 128)
		{ Reject(); }
		var networks = new HashSet<(string Tenant, string Id)>();
		foreach (var policy in options.SshNetworkPolicies)
		{
			var snapshot = Snapshot(policy);
			if (!networks.Add((snapshot.TenantId, snapshot.Id)))
			{ Reject(); }
		}
		var trusts = new HashSet<(string Tenant, string Id)>();
		foreach (var trust in options.SshTrustProfiles)
		{
			var snapshot = Snapshot(trust);
			if (!trusts.Add((snapshot.TenantId, snapshot.Id)))
			{ Reject(); }
		}
	}

	public static GitSshNetworkPolicy Snapshot(GitSshNetworkPolicy policy)
	{
		ArgumentNullException.ThrowIfNull(policy);
		Identity(policy.Id, policy.TenantId, policy.Revision, policy.RepositoryScope);
		return policy with { AllowedAddressRanges = Ranges(policy.AllowedAddressRanges), DeniedAddressRanges = Ranges(policy.DeniedAddressRanges) };
	}
	public static GitSshTrustProfile Snapshot(GitSshTrustProfile trust)
	{
		ArgumentNullException.ThrowIfNull(trust);
		Identity(trust.Id, trust.TenantId, trust.Revision, trust.RepositoryScope);
		if (trust.HostKeys is null || trust.HostKeys.Count is < 1 or > 16)
		{ Reject(); }
		var keys = trust.HostKeys.ToArray();
		foreach (var key in keys)
		{
			var parts = key.Split(' ');
			if (parts.Length != 2 || parts[0] is not ("ssh-ed25519" or "ecdsa-sha2-nistp256" or "ssh-rsa")
				|| parts[1].Length is < 16 or > 16_384 || parts[1].Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('+' or '/' or '=')))
			{ Reject(); }
			try
			{ _ = Convert.FromBase64String(parts[1]); }
			catch (FormatException) { Reject(); }
		}
		return trust with { HostKeys = Array.AsReadOnly(keys) };
	}

	public static void ValidateScope(Uri remote)
	{
		var profile = new RepositoryConnectionProfile(Guid.NewGuid(), "validation", 1, GitTransportKind.Ssh, GitAuthenticationKind.SshKey,
			new(remote, "network", "trust"), "credential", SourceControlCapability.Read);
		RepositoryConnectionProfilePolicy.Validate(profile);
		if (remote.AbsolutePath.Split('/').Any(part => part is "." or "..")
			|| remote.AbsolutePath.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('/' or '-' or '_' or '.')))
		{ Reject(); }
	}

	private static void Identity(string id, string tenant, long revision, Uri scope)
	{
		if (string.IsNullOrWhiteSpace(id) || id.Length > 240 || id != id.Trim() || id.Any(char.IsControl)
			|| string.IsNullOrWhiteSpace(tenant) || tenant.Length > 64 || tenant != tenant.Trim() || tenant == "$global" || tenant.Any(char.IsControl) || revision <= 0)
		{ Reject(); }
		ValidateScope(scope);
	}
	private static System.Collections.ObjectModel.ReadOnlyCollection<string> Ranges(IReadOnlyList<string> ranges)
	{
		if (ranges is null || ranges.Count > 64 || ranges.Any(range => range != range.Trim() || !IPNetwork.TryParse(range, out _)))
		{ Reject(); }
		return Array.AsReadOnly(ranges.ToArray());
	}
	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
