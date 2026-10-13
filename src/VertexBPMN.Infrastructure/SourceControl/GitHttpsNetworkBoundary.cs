using System.Net;
using System.Net.Sockets;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Scope first, then reject every unsafe DNS answer before starting native Git.</summary>
internal static class GitHttpsNetworkBoundary
{
	internal static Task<ApprovedGitHttpsTarget> ResolveAsync(SourceControlContext context, RepositoryBinding binding,
		RepositoryConnectionProfile profile, GitHttpsNetworkPolicy policy, CancellationToken cancellationToken)
		=> ResolveAsync(context, binding, profile, policy, Dns.GetHostAddressesAsync, cancellationToken);

	internal static async Task<ApprovedGitHttpsTarget> ResolveAsync(SourceControlContext context, RepositoryBinding binding,
		RepositoryConnectionProfile profile, GitHttpsNetworkPolicy policy,
		Func<string, CancellationToken, Task<IPAddress[]>> resolve, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(policy);
		ArgumentNullException.ThrowIfNull(resolve);
		policy = GitHttpsConfigurationPolicy.Snapshot(policy);
		if (policy.IsRevoked)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.ProviderUnavailable);
		}
		RepositoryConnectionProfilePolicy.DemandRepository(context, binding, profile);
		if (policy.TenantId != context.TenantId)
		{
			throw new SourceControlSecurityException(SourceControlErrorCode.NotFound);
		}

		if (profile.Transport != GitTransportKind.Https || policy.Id != profile.GitEndpoint.NetworkPolicyReference)
		{
			Reject();
		}

		RepositoryConnectionProfilePolicy.DemandRepository(context, binding,
			profile with { GitEndpoint = new(policy.RepositoryScope, policy.Id) });
		var allowed = ParseRanges(policy.AllowedAddressRanges);
		var denied = ParseRanges(policy.DeniedAddressRanges);
		cancellationToken.ThrowIfCancellationRequested();
		var answers = await resolve(binding.Remote.IdnHost, cancellationToken);
		cancellationToken.ThrowIfCancellationRequested();
		if (answers.Length is 0 or > 32 || answers.Any(address => !Allowed(address, allowed, denied)))
		{
			Reject();
		}

		return new(binding.Remote, profile, answers.Distinct().ToArray());
	}

	private static IPNetwork[] ParseRanges(IReadOnlyList<string> ranges)
	{
		if (ranges is null || ranges.Count > 64)
		{
			Reject();
		}

		var networks = new IPNetwork[ranges.Count];
		for (var index = 0; index < ranges.Count; index++)
		{
			var value = ranges[index];
			if (string.IsNullOrWhiteSpace(value) || value != value.Trim()
				|| !IPNetwork.TryParse(value, out networks[index]))
			{
				Reject();
			}
		}

		return networks;
	}

	internal static void DemandAddresses(IPAddress[] answers, IReadOnlyList<string> allowedRanges, IReadOnlyList<string> deniedRanges)
	{
		var allowed = ParseRanges(allowedRanges);
		var denied = ParseRanges(deniedRanges);
		if (answers.Length is 0 or > 32 || answers.Any(address => !Allowed(address, allowed, denied))) { Reject(); }
	}

	private static bool Allowed(IPAddress address, IPNetwork[] allowed, IPNetwork[] denied)
	{
		if (address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId != 0)
		{
			return false;
		}

		if (address.IsIPv4MappedToIPv6)
		{
			address = address.MapToIPv4();
		}

		if (!EligibleAddress(address) || denied.Any(network => network.Contains(address)))
		{
			return false;
		}

		return allowed.Length == 0 ? SourceControlHttps.IsPublicAddress(address)
			: allowed.Any(network => network.Contains(address));
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "S1313", Justification = "Fixed cloud infrastructure endpoints are a security denylist, never deployment connection configuration.")]
	private static bool EligibleAddress(IPAddress address)
	{
		if (SourceControlHttps.IsPublicAddress(address))
		{
			return !address.Equals(IPAddress.Parse("168.63.129.16")); // Azure platform endpoint.
		}

		var bytes = address.GetAddressBytes();
		if (address.AddressFamily == AddressFamily.InterNetwork)
		{
			return bytes[0] == 10 || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
				|| (bytes[0] == 192 && bytes[1] == 168);
		}

		return address.AddressFamily == AddressFamily.InterNetworkV6 && address.ScopeId == 0
			&& (bytes[0] & 0xfe) == 0xfc
			&& !address.Equals(IPAddress.Parse("fd00:ec2::254"))
			&& !address.Equals(IPAddress.Parse("fd20:ce::254"));
	}

	[System.Diagnostics.CodeAnalysis.DoesNotReturn]
	private static void Reject() => throw new SourceControlSecurityException(SourceControlErrorCode.InvalidInput);
}
