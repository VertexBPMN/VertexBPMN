using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using Xunit;

namespace VertexBPMN.Tests.Unit.Lc10.Domain;

public sealed class ReleaseManifestTests
{
    private static readonly string[] Dependencies = ["connector-a"];
    private static readonly string[] OtherDependencies = ["connector-b"];

    [Fact]
    public void Parse_RoundTripsAllFields()
    {
        var json = ReleaseManifest.Serialize(new ReleaseManifest
        {
            SchemaVersion = "2",
            ArtifactKind = "application",
            Key = "k1",
            Revision = 7,
            Hash = "deadbeef",
            Dependencies = Dependencies,
            TargetProfile = "prod",
            CredentialAlias = "cred/x",
        });

        var manifest = ReleaseManifest.Parse(json);

        Assert.Equal("2", manifest.SchemaVersion, StringComparer.Ordinal);
        Assert.Equal("application", manifest.ArtifactKind, StringComparer.Ordinal);
        Assert.Equal("k1", manifest.Key, StringComparer.Ordinal);
        Assert.Equal(7, manifest.Revision);
        Assert.Equal("deadbeef", manifest.Hash, StringComparer.Ordinal);
        Assert.Equal(Dependencies, manifest.Dependencies, StringComparer.Ordinal);
        Assert.Equal("prod", manifest.TargetProfile, StringComparer.Ordinal);
        Assert.Equal("cred/x", manifest.CredentialAlias, StringComparer.Ordinal);
    }

    [Fact]
    public void Parse_RejectsUnsupportedSchemaVersion()
    {
        Assert.Throws<ApplicationValidationException>(() => ReleaseManifest.Parse("{\"schemaVersion\":\"99\"}"));
    }

    [Fact]
    public void ValidateForPrepare_RejectsWrongKey()
    {
        var manifest = ReleaseManifest.Parse("{\"schemaVersion\":\"1\",\"artifactKind\":\"application\",\"key\":\"wrong\",\"revision\":1,\"hash\":\"h\"}");
        Assert.Throws<ApplicationForeignTenantException>(() => manifest.ValidateForPrepare("expected", 1));
    }

    [Fact]
    public void ValidateForPrepare_RejectsMissingHash()
    {
        var manifest = ReleaseManifest.Parse("{\"schemaVersion\":\"1\",\"artifactKind\":\"application\",\"key\":\"k\",\"revision\":1}");
        Assert.Throws<ApplicationValidationException>(() => manifest.ValidateForPrepare("k", 1));
    }

    [Fact]
    public void ValidateForPrepare_RejectsRevisionMismatch()
    {
        var manifest = ReleaseManifest.Parse("{\"schemaVersion\":\"1\",\"artifactKind\":\"application\",\"key\":\"k\",\"revision\":2,\"hash\":\"h\"}");
        Assert.Throws<ApplicationValidationException>(() => manifest.ValidateForPrepare("k", 1));
    }

    [Fact]
    public void ComputeHash_IsStableAndDistinguishesInputs()
    {
        var h1 = ReleaseManifest.ComputeHash("hello");
        var h2 = ReleaseManifest.ComputeHash("hallo");
        Assert.Equal(h1, ReleaseManifest.ComputeHash("hello"), StringComparer.Ordinal);
        Assert.NotEqual(h1, h2, StringComparer.Ordinal);
    }

    [Fact]
    public void DifferentDependencies_AreDistinguished()
    {
        Assert.NotEqual(Dependencies, OtherDependencies, StringComparer.Ordinal);
    }
}
