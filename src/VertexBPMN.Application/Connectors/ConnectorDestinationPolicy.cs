using System.Collections.Concurrent;
using System.Data.Common;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Application.Connectors;

/// <summary>
/// Enforces destination allowlists independently from credential selection and
/// rejects loopback, private, link-local and unspecified network destinations.
/// </summary>
public sealed class ConnectorDestinationPolicy(IConfiguration configuration)
{
	public async Task ValidateAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		switch (context.Type.ToLowerInvariant())
		{
			case "http":
			case "webhook":
			case "slack":
			case "ai":
				if (context.Endpoint is null)
				{
					throw new ServiceTaskExecutionException("The connector requires an absolute destination endpoint.");
				}

				await ValidateNetworkHostAsync(context.Endpoint.Host, "AllowedHttpHosts", cancellationToken);
				break;

			case "email":
			case "smtp":
				await ValidateNetworkHostAsync(RequiredAttribute(context, "vertex:connector.smtpHost"),
					"AllowedSmtpHosts", cancellationToken);
				break;

			case "database":
			case "db":
			case "postgresql":
			case "sqlserver":
			case "sqlite":
				await ValidateDatabaseAsync(context, cancellationToken);
				break;
		}
	}

	private async Task ValidateDatabaseAsync(ConnectorExecutionContext context, CancellationToken cancellationToken)
	{
		var provider = RequiredAttribute(context, "vertex:connector.provider");
		EnsureAllowed(provider, "AllowedDatabaseProviders", "database provider");
		if (string.IsNullOrWhiteSpace(context.CredentialSecret))
		{
			throw new ServiceTaskExecutionException("Database connector requires a connection-string credential.");
		}

		var values = new DbConnectionStringBuilder { ConnectionString = context.CredentialSecret };
		var host = Value(values, "Host") ?? Value(values, "Server") ?? Value(values, "Data Source");
		if (provider.Contains("sqlite", StringComparison.OrdinalIgnoreCase))
		{
			if (string.IsNullOrWhiteSpace(host) || string.Equals(host, ":memory:", StringComparison.OrdinalIgnoreCase))
			{
				throw new ServiceTaskExecutionException("SQLite connector requires a durable allowlisted file path.");
			}

			var fullPath = Path.GetFullPath(host);
			var roots = configuration.GetSection("ConnectorRuntime:AllowedDatabaseFileRoots").Get<string[]>() ?? [];
			if (!roots.Select(Path.GetFullPath).Any(root => fullPath.StartsWith(
					root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
					StringComparison.Ordinal)))
			{
				throw new ServiceTaskExecutionException("SQLite destination is outside the configured database file roots.");
			}

			return;
		}

		if (string.IsNullOrWhiteSpace(host))
		{
			throw new ServiceTaskExecutionException("The database destination host could not be determined.");
		}

		host = NormalizeDatabaseHost(host);
		await ValidateNetworkHostAsync(host, "AllowedDatabaseHosts", cancellationToken);
	}

	private static string NormalizeDatabaseHost(string value)
	{
		var host = value.Trim();
		if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
		{
			host = host[4..].Trim();
		}

		if (host.StartsWith("[", StringComparison.Ordinal))
		{
			var closingBracket = host.IndexOf(']');
			if (closingBracket > 1)
			{
				return host[1..closingBracket];
			}
		}

		var separator = host.IndexOfAny([',', '\\']);
		if (separator > 0)
		{
			host = host[..separator];
		}

		var colon = host.LastIndexOf(':');
		if (colon > 0 && host.Count(character => character == ':') == 1
					  && int.TryParse(host[(colon + 1)..], out _))
		{
			host = host[..colon];
		}

		return host.Trim();
	}

	private async Task ValidateNetworkHostAsync(string host, string section, CancellationToken cancellationToken)
	{
		EnsureAllowed(host, section, "destination host");
		await ThrowIfForbiddenAsync(host, cancellationToken);
	}

	/// <summary>
	/// Rejects loopback, private, link-local, multicast, unspecified and unresolved
	/// destinations after DNS resolution, without requiring a pre-registered
	/// allowlist. Used for operator-supplied public endpoints such as OAuth2
	/// token URLs (M2) where no static host allowlist is configured.
	/// </summary>
	public static async Task ThrowIfForbiddenAsync(string host, CancellationToken cancellationToken, bool rejectUnresolvable = true)
	{
		IPAddress[] addresses;
		try
		{
			addresses = IPAddress.TryParse(host, out var address)
				? [address]
				: await Dns.GetHostAddressesAsync(host, cancellationToken);
		}
		catch (SocketException exception)
		{
			if (!rejectUnresolvable)
			{
				return; // no reachable internal target; the POST fails anyway
			}

			throw new ServiceTaskExecutionException($"Destination host '{host}' could not be resolved.", exception);
		}

		if (addresses.Any(IsForbiddenAddress))
		{
			throw new ServiceTaskExecutionException(
				$"Destination host '{host}' resolves to a private, loopback, link-local or unspecified address.");
		}

		if (addresses.Length == 0 && rejectUnresolvable)
		{
			throw new ServiceTaskExecutionException($"Destination host '{host}' could not be resolved.");
		}
	}

	private void EnsureAllowed(string value, string section, string kind)
	{
		var allowlist = configuration.GetSection($"ConnectorRuntime:{section}").Get<string[]>() ?? [];
		var allowed = allowlist.Any(entry => string.Equals(entry, value, StringComparison.OrdinalIgnoreCase)
			|| (entry.StartsWith("*.", StringComparison.Ordinal)
			   && value.EndsWith(entry[1..], StringComparison.OrdinalIgnoreCase)
			   && value.Length > entry.Length - 1));
		if (!allowed)
		{
			throw new ServiceTaskExecutionException($"The {kind} '{value}' is not allowlisted by ConnectorRuntime:{section}.");
		}
	}

	private static bool IsForbiddenAddress(IPAddress address)
	{
		if (address.IsIPv4MappedToIPv6)
		{
			return IsForbiddenAddress(address.MapToIPv4());
		}

		if (address.AddressFamily == AddressFamily.InterNetworkV6
			&& (address.GetAddressBytes()[0] & 0xfe) == 0xfc)
		{
			return true;
		}

		if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)
			|| address.Equals(IPAddress.None) || address.Equals(IPAddress.IPv6None)
			|| address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
		{
			return true;
		}

		if (address.AddressFamily != AddressFamily.InterNetwork)
		{
			return false;
		}

		var bytes = address.GetAddressBytes();
		return bytes[0] == 10
			   || bytes[0] == 127
			   || bytes[0] == 0
			   || (bytes[0] == 169 && bytes[1] == 254)
			   || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
			   || (bytes[0] == 192 && bytes[1] == 168)
			   || bytes[0] >= 224;
	}

	private static string RequiredAttribute(ConnectorExecutionContext context, string key) =>
		context.Attributes.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
			? value.Trim()
			: throw new ServiceTaskExecutionException($"Connector requires '{key}'.");

	private static string? Value(DbConnectionStringBuilder values, string key) =>
		values.TryGetValue(key, out var value) ? Convert.ToString(value) : null;

	/// <summary>
	/// Resolves a host and returns its validated non-forbidden addresses after rejecting
	/// loopback/private/link-local/multicast/unspecified ones. Connecting to one of the
	/// returned addresses (instead of re-resolving by hostname) closes the DNS-rebinding /
	/// TOCTOU window between SSRF validation and the actual connection (M5).
	/// </summary>
	public static async Task<IPAddress[]> ResolveValidatedAddressesAsync(string host, CancellationToken cancellationToken)
	{
		IPAddress[] addresses;
		try
		{
			addresses = IPAddress.TryParse(host, out var address)
				? [address]
				: await Dns.GetHostAddressesAsync(host, cancellationToken);
		}
		catch (SocketException exception)
		{
			throw new ServiceTaskExecutionException($"Destination host '{host}' could not be resolved.", exception);
		}

		ValidateResolvedAddresses(host, addresses);
		return addresses;
	}

	public static void ValidateResolvedAddresses(string host, IPAddress[] addresses)
	{
		if (addresses.Length == 0 || addresses.Any(IsForbiddenAddress))
		{
			throw new ServiceTaskExecutionException(
				$"Destination host '{host}' resolves to a private, loopback, link-local or unspecified address.");
		}
	}
}
