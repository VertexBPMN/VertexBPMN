using System.Net;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

/// <summary>Operation-local DNS approval; consumers never resolve the host again.</summary>
internal sealed class ApprovedGitHttpsTarget(Uri remote, RepositoryConnectionProfile profile, IReadOnlyList<IPAddress> addresses)
{
	private readonly string[] addresses = addresses.Select(address => address.ToString()).ToArray();
	internal Uri Remote { get; } = remote;
	internal RepositoryConnectionProfile Profile { get; } = profile;
	internal ResolvedGitHttpsPolicy? AdministrativePolicy { get; init; }
	internal SourceControlCapability RequiredCapabilities { get; init; } = SourceControlCapability.Read;
	internal IPAddress[] CopyAddresses() => addresses.Select(IPAddress.Parse).ToArray();
}
