using System.Net;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Infrastructure.SourceControl;

internal sealed class ApprovedGitSshTarget(SourceControlExecutionTarget target, GitSshNetworkPolicy network, GitSshTrustProfile trust,
	IReadOnlyList<IPAddress> addresses)
{
	private readonly string[] addresses = addresses.Select(address => address.ToString()).ToArray();
	internal SourceControlExecutionTarget Target { get; } = target;
	internal GitSshNetworkPolicy Network { get; } = network;
	internal GitSshTrustProfile Trust { get; } = trust;
	internal IPAddress[] CopyAddresses() => addresses.Select(IPAddress.Parse).ToArray();
}
