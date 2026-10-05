using System.Net;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;

namespace VertexBPMN.Tests.Unit.Infrastructure;

public sealed class SourceControlNetworkTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("10.1.1.1")]
    [InlineData("172.31.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::1")]
    public void Nonpublic_and_transition_addresses_are_rejected(string address) =>
        Assert.False(SourceControlHttps.IsPublicAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_accepted(string address) => Assert.True(SourceControlHttps.IsPublicAddress(IPAddress.Parse(address)));

    [Theory]
    [InlineData("http://github.com/example/models.git")]
    [InlineData("https://github.com:444/example/models.git")]
    [InlineData("https://evil.example/models.git")]
    [InlineData("https://token@github.com/example/models.git")]
    [InlineData("https://github.com/example/models.git?token=value")]
    [InlineData("https://github.com/example/models.git#fragment")]
    [InlineData("file:///tmp/repo")]
    public void Unapproved_transport_targets_are_rejected(string uri) =>
        Assert.Throws<SourceControlSecurityException>(() => SourceControlHttps.ValidateTarget(new Uri(uri), ["github.com"]));
}
