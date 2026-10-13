using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using VertexBPMN.Application;
using VertexBPMN.Application.SourceControl;
using VertexBPMN.Infrastructure.SourceControl;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class GitHttpsPolicyRegistryTests
{
	private static readonly SourceControlContext Actor = new("tenant-a", "actor-a");
	private static readonly Uri Scope = new("https://git.example.com:8443/team");
	private static readonly GitHttpsNetworkPolicy Network = new("private-git", Actor.TenantId, Scope, ["10.20.0.0/16"], []);
	private static readonly RepositoryConnectionProfile Profile = new(Guid.NewGuid(), Actor.TenantId, 1,
		GitTransportKind.Https, GitAuthenticationKind.Anonymous, new(Scope, Network.Id), null, SourceControlCapability.Read);
	private static readonly RepositoryBinding Binding = new(Guid.NewGuid(), Actor.TenantId,
		new Uri("https://git.example.com:8443/team/models.git"), null, "master", "release", ["models"])
	{
		ConnectionProfileId = Profile.Id
	};
	private static readonly Lazy<string> CertificateAuthority = new(() => CreateCertificatePem(
		new X509BasicConstraintsExtension(true, false, 0, true), X509KeyUsageFlags.KeyCertSign));

	[Fact]
	public void Deployment_configuration_binds_typed_network_and_public_ca_profiles()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			["SourceControl:Enabled"] = "true",
			["SourceControl:GitExecutablePath"] = Path.Combine(Path.GetTempPath(), "git"),
			["SourceControl:WorkspaceRoot"] = Path.Combine(Path.GetTempPath(), "vertex-config-test"),
			["SourceControl:AllowedHosts:0"] = "github.com",
			["SourceControl:HttpsNetworkPolicies:0:Id"] = Network.Id,
			["SourceControl:HttpsNetworkPolicies:0:TenantId"] = Actor.TenantId,
			["SourceControl:HttpsNetworkPolicies:0:Revision"] = "3",
			["SourceControl:HttpsNetworkPolicies:0:RepositoryScope"] = Scope.AbsoluteUri,
			["SourceControl:HttpsNetworkPolicies:0:AllowedAddressRanges:0"] = "10.20.0.0/16",
			["SourceControl:HttpsNetworkPolicies:0:DeniedAddressRanges:0"] = "10.20.1.0/24",
			["SourceControl:HttpsTrustProfiles:0:Id"] = "corporate-ca",
			["SourceControl:HttpsTrustProfiles:0:TenantId"] = Actor.TenantId,
			["SourceControl:HttpsTrustProfiles:0:RepositoryScope"] = Scope.AbsoluteUri,
			["SourceControl:HttpsTrustProfiles:0:CertificateAuthorityPem"] = CertificateAuthority.Value
		}).Build();
		var services = new ServiceCollection();
		services.AddApplicationServices(configuration);
		using var provider = services.BuildServiceProvider();
		var monitor = provider.GetRequiredService<IOptionsMonitor<SourceControlOptions>>();
		var registry = new GitHttpsPolicyRegistry(monitor);
		var resolved = registry.Resolve(Actor, Binding, Profile with { GitEndpoint = new(Scope, Network.Id, "corporate-ca") });
		Assert.Equal(3, resolved.Network.Revision);
		Assert.Equal(["10.20.1.0/24"], resolved.Network.DeniedAddressRanges);
		Assert.Equal(CertificateAuthority.Value, resolved.Trust!.CertificateAuthorityPem);
	}

	[Fact]
	public void Absent_cidr_lists_bind_to_the_public_default_without_private_freedom()
	{
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>(StringComparer.Ordinal)
		{
			["Network:Id"] = Network.Id,
			["Network:TenantId"] = Actor.TenantId,
			["Network:RepositoryScope"] = Scope.AbsoluteUri
		}).Build();
		var bound = configuration.GetSection("Network").Get<GitHttpsNetworkPolicy>();
		Assert.NotNull(bound);
		var snapshot = GitHttpsConfigurationPolicy.Snapshot(bound);
		Assert.Empty(snapshot.AllowedAddressRanges);
		Assert.Empty(snapshot.DeniedAddressRanges);
		Assert.Equal(1, snapshot.Revision);
	}

	[Theory]
	[InlineData("id")]
	[InlineData("tenant")]
	[InlineData("revision")]
	[InlineData("loopback")]
	[InlineData("root")]
	[InlineData("traversal")]
	[InlineData("cidr")]
	[InlineData("null-ranges")]
	[InlineData("many-ranges")]
	[InlineData("duplicate")]
	public void Invalid_deployment_network_configuration_is_rejected(string invalid)
	{
		var network = invalid switch
		{
			"id" => Network with { Id = " " },
			"tenant" => Network with { TenantId = "$global" },
			"revision" => Network with { Revision = 0 },
			"loopback" => Network with { RepositoryScope = new Uri("https://127.0.0.1/team") },
			"root" => Network with { RepositoryScope = new Uri("https://git.example.com/") },
			"traversal" => Network with { RepositoryScope = new Uri("https://git.example.com/team/../other") },
			"cidr" => Network with { AllowedAddressRanges = ["10.20.0.0/99"] },
			"null-ranges" => Network with { AllowedAddressRanges = null! },
			"many-ranges" => Network with { AllowedAddressRanges = Enumerable.Repeat("10.0.0.0/8", 65).ToArray() },
			_ => Network
		};
		var options = Configuration(invalid == "duplicate" ? [network, network] : [network]);
		Assert.True(new SourceControlOptionsValidator().Validate(null, options).Failed);
	}

	[Theory]
	[InlineData("empty")]
	[InlineData("malformed")]
	[InlineData("private-key")]
	[InlineData("prefix")]
	[InlineData("suffix")]
	[InlineData("too-large")]
	[InlineData("too-many")]
	[InlineData("leaf")]
	[InlineData("no-signing")]
	public void Trust_bundles_accept_only_bounded_public_ca_certificates(string invalid)
	{
		var pem = invalid switch
		{
			"empty" => "",
			"malformed" => "-----BEGIN CERTIFICATE-----\nYWJj\n-----END CERTIFICATE-----",
			"private-key" => "-----BEGIN PRIVATE KEY-----\nYWJj\n-----END PRIVATE KEY-----",
			"prefix" => "untrusted-prefix\n" + CertificateAuthority.Value,
			"suffix" => CertificateAuthority.Value + "\nuntrusted-suffix",
			"too-large" => new string(' ', (64 * 1024) + 1),
			"too-many" => string.Join('\n', Enumerable.Repeat(CertificateAuthority.Value, 17)),
			"leaf" => CreateCertificatePem(new(false, false, 0, true), X509KeyUsageFlags.DigitalSignature),
			_ => CreateCertificatePem(new(true, false, 0, true), X509KeyUsageFlags.DigitalSignature)
		};
		Assert.True(new SourceControlOptionsValidator().Validate(null,
			Configuration([Network], [new("corporate-ca", Actor.TenantId, Scope, pem)])).Failed);
	}

	[Theory]
	[InlineData("missing")]
	[InlineData("foreign")]
	[InlineData("revoked")]
	[InlineData("wrong-port")]
	[InlineData("wrong-path")]
	public void Profile_references_cannot_resolve_foreign_revoked_or_unscoped_policies(string unavailable)
	{
		var network = unavailable switch
		{
			"foreign" => Network with { TenantId = "tenant-b" },
			"revoked" => Network with { IsRevoked = true },
			"wrong-port" => Network with { RepositoryScope = new Uri("https://git.example.com/team") },
			"wrong-path" => Network with { RepositoryScope = new Uri("https://git.example.com:8443/team/other") },
			_ => Network with { Id = "other-policy" }
		};
		Assert.Throws<SourceControlSecurityException>(() => Registry(() => Configuration([network])).Resolve(Actor, Binding, Profile));
	}

	[Fact]
	public void Approval_snapshots_cidr_lists_and_detects_changes_even_without_a_revision_increment()
	{
		var ranges = new[] { "10.20.0.0/16" };
		var current = Configuration([Network with { AllowedAddressRanges = ranges }]);
		var registry = Registry(() => current);
		var accepted = registry.Resolve(Actor, Binding, Profile);
		var approved = new ApprovedGitHttpsTarget(Binding.Remote, Profile, [IPAddress.Parse("10.20.1.1")]) { AdministrativePolicy = accepted };
		ranges[0] = "10.21.0.0/16";
		Assert.Equal("10.20.0.0/16", Assert.Single(accepted.Network.AllowedAddressRanges));
		Assert.Equal(SourceControlErrorCode.RevisionConflict,
			Assert.Throws<SourceControlSecurityException>(() => registry.DemandCurrent(Actor, Binding, approved)).Code);
		current = Configuration([Network with { Revision = 2 }]);
		Assert.Equal(SourceControlErrorCode.RevisionConflict,
			Assert.Throws<SourceControlSecurityException>(() => registry.DemandCurrent(Actor, Binding, approved)).Code);
		current = Configuration([Network with { IsRevoked = true }]);
		Assert.Equal(SourceControlErrorCode.ProviderUnavailable,
			Assert.Throws<SourceControlSecurityException>(() => registry.DemandCurrent(Actor, Binding, approved)).Code);
	}

	[Fact]
	public void Trust_reference_scope_revision_and_contents_remain_bound_to_the_approval()
	{
		var trust = new GitHttpsTrustProfile("corporate-ca", Actor.TenantId, Scope, CertificateAuthority.Value);
		var profile = Profile with { GitEndpoint = new(Scope, Network.Id, trust.Id) };
		var current = Configuration([Network], [trust]);
		var registry = Registry(() => current);
		var accepted = registry.Resolve(Actor, Binding, profile);
		var approved = new ApprovedGitHttpsTarget(Binding.Remote, profile, [IPAddress.Parse("10.20.1.1")]) { AdministrativePolicy = accepted };
		registry.DemandCurrent(Actor, Binding, approved);
		current = Configuration([Network], [trust with { CertificateAuthorityPem = trust.CertificateAuthorityPem + "\n" }]);
		Assert.Equal(SourceControlErrorCode.RevisionConflict,
			Assert.Throws<SourceControlSecurityException>(() => registry.DemandCurrent(Actor, Binding, approved)).Code);
		current = Configuration([Network], [trust with { RepositoryScope = new Uri("https://other.example.com/team") }]);
		Assert.Throws<SourceControlSecurityException>(() => registry.Resolve(Actor, Binding, profile));
		current = Configuration([Network], [trust with { TenantId = "tenant-b" }]);
		Assert.Throws<SourceControlSecurityException>(() => registry.Resolve(Actor, Binding, profile));
		current = Configuration([Network], [trust with { IsRevoked = true }]);
		Assert.Throws<SourceControlSecurityException>(() => registry.Resolve(Actor, Binding, profile));
		current = Configuration([Network]);
		Assert.Throws<SourceControlSecurityException>(() => registry.Resolve(Actor, Binding, profile));
	}

	[Fact]
	public void Disabled_mode_does_not_require_valid_ca_or_network_configuration()
	{
		var disabled = new SourceControlOptions { HttpsNetworkPolicies = null!, HttpsTrustProfiles = null! };
		Assert.True(new SourceControlOptionsValidator().Validate(null, disabled).Succeeded);
		Assert.Equal(SourceControlErrorCode.Disabled,
			Assert.Throws<SourceControlSecurityException>(() => Registry(() => disabled).Resolve(Actor, Binding, Profile)).Code);
	}

	private static GitHttpsPolicyRegistry Registry(Func<SourceControlOptions> configuration)
	{
		var monitor = new Mock<IOptionsMonitor<SourceControlOptions>>();
		monitor.SetupGet(value => value.CurrentValue).Returns(configuration);
		return new(monitor.Object);
	}

	private static SourceControlOptions Configuration(GitHttpsNetworkPolicy[] networks, GitHttpsTrustProfile[]? trusts = null) => new()
	{
		Enabled = true,
		GitExecutablePath = Path.Combine(Path.GetTempPath(), "git"),
		WorkspaceRoot = Path.Combine(Path.GetTempPath(), "vertex-policy-test"),
		AllowedHosts = ["github.com"],
		HttpsNetworkPolicies = networks,
		HttpsTrustProfiles = trusts ?? []
	};

	private static string CreateCertificatePem(X509BasicConstraintsExtension constraints, X509KeyUsageFlags usage)
	{
		using var key = RSA.Create(2048);
		var request = new CertificateRequest("CN=Vertex policy test certificate", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
		request.CertificateExtensions.Add(constraints);
		request.CertificateExtensions.Add(new X509KeyUsageExtension(usage, true));
		using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
		return certificate.ExportCertificatePem();
	}
}
