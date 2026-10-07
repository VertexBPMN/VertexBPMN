using System.Security.Cryptography;
using System.Text;
using VertexBPMN.SourceControl.Abstractions;

namespace VertexBPMN.SourceControl.Tests;

/// <summary>Pure contract checks. These are not Git transport, persistence or browser acceptance.</summary>
public class SourceControlContractTests
{
    [Theory]
    [InlineData(40)]
    [InlineData(64)]
    public void CommitId_AcceptsFullObjectIdsAndCanonicalizesCase(int length)
    {
        var id = new GitCommitId(new string('A', length));
        Assert.Equal(new string('a', length), id.Value);
        Assert.Equal(new GitCommitId(new string('a', length)), id);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(39)]
    [InlineData(41)]
    [InlineData(63)]
    [InlineData(65)]
    public void CommitId_RejectsAbbreviatedOrInvalidLengthIds(int length)
        => Assert.ThrowsAny<ArgumentException>(() => new GitCommitId(new string('a', length)));

    [Theory]
    [InlineData("main")]
    [InlineData("HEAD~1")]
    [InlineData("--all")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaag")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa ")]
    public void CommitId_RejectsRefsExpressionsOptionsAndNonHex(string value)
        => Assert.ThrowsAny<ArgumentException>(() => new GitCommitId(value));

    [Fact]
    public void CommitId_RejectsNull()
        => Assert.ThrowsAny<ArgumentException>(() => new GitCommitId(null!));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("$global")]
    [InlineData(" tenant-a")]
    [InlineData("tenant-a ")]
    [InlineData("tenant\na")]
    public void Context_RejectsMissingGlobalOrNonCanonicalTenant(string tenant)
        => Assert.ThrowsAny<ArgumentException>(() => new SourceControlContext(tenant, "issuer:subject"));

    [Fact]
    public void Context_RejectsOverlongTenant()
        => Assert.ThrowsAny<ArgumentException>(() => new SourceControlContext(new string('a', 65), "issuer:subject"));

    [Fact]
    public void Context_RejectsNullIdentityAndOverlongActor()
    {
        Assert.ThrowsAny<ArgumentException>(() => new SourceControlContext(null!, "actor"));
        Assert.ThrowsAny<ArgumentException>(() => new SourceControlContext("tenant-a", null!));
        Assert.ThrowsAny<ArgumentException>(() => new SourceControlContext("tenant-a", new string('a', 513)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" actor")]
    [InlineData("actor ")]
    [InlineData("actor\nforged")]
    public void Context_RequiresCanonicalAuthenticatedActor(string actor)
        => Assert.ThrowsAny<ArgumentException>(() => new SourceControlContext("tenant-a", actor));

    [Fact]
    public void Context_KeepsTenantAndIssuerQualifiedActor()
    {
        var context = new SourceControlContext("tenant-a", "issuer-id:subject-id");
        Assert.Equal("tenant-a", context.TenantId);
        Assert.Equal("issuer-id:subject-id", context.ActorId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("with space")]
    [InlineData("line\nbreak")]
    [InlineData("a/b")]
    [InlineData("ä")]
    public void IdempotencyKey_RejectsNonCanonicalKeys(string value)
        => Assert.ThrowsAny<ArgumentException>(() => new SourceControlIdempotencyKey(value));

    [Fact]
    public void IdempotencyKey_BoundaryAndCaseAreExplicit()
    {
        Assert.Equal(new string('a', 128), new SourceControlIdempotencyKey(new string('a', 128)).Value);
        Assert.Throws<ArgumentException>(() => new SourceControlIdempotencyKey(new string('a', 129)));
        Assert.NotEqual(new SourceControlIdempotencyKey("Request_1"), new SourceControlIdempotencyKey("request_1"));
    }

    [Fact]
    public void ExpectedRemoteRef_AbsenceIsNotAnOptionalCheck()
    {
        Assert.True(ExpectedRemoteRef.Absent.MustBeAbsent);
        var id = new GitCommitId(new string('a', 40));
        var expectation = ExpectedRemoteRef.At(id);
        Assert.False(expectation.MustBeAbsent);
        Assert.Equal(id, expectation.Commit);
        Assert.Throws<ArgumentNullException>(() => ExpectedRemoteRef.At(null!));
    }

    [Fact]
    public void Snapshot_CopiesInputAndOutputAndPreservesExactBytes()
    {
        var content = Encoding.UTF8.GetBytes("\uFEFF<definitions>\r\n  original\r\n</definitions>");
        var expected = (byte[])content.Clone();
        var generation = Guid.NewGuid();
        var snapshot = new ModelSnapshot("models/test.bpmn", SourceModelKind.Bpmn, generation, 17, content);
        Array.Fill(content, (byte)0);
        var exported = snapshot.CopyContent();
        exported[0] = 0;
        Assert.Equal(expected, snapshot.CopyContent());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(expected)), snapshot.ContentSha256);
        Assert.Equal(expected.Length, snapshot.ContentLength);
        Assert.Equal(generation, snapshot.DocumentGeneration);
        Assert.Equal(17, snapshot.LocalRevision);
    }

    [Fact]
    public void Snapshot_DoesNotPutContentInDiagnosticString()
    {
        var snapshot = new ModelSnapshot("model.bpmn", SourceModelKind.Bpmn, Guid.NewGuid(), 0,
            Encoding.UTF8.GetBytes("sensitive-payload"));
        Assert.DoesNotContain("sensitive-payload", snapshot.ToString());
    }

    [Fact]
    public void Snapshot_RequiresRevisionGenerationContentAndKnownKind()
    {
        var bytes = new byte[] { 1 };
        Assert.ThrowsAny<ArgumentException>(() => new ModelSnapshot("", SourceModelKind.Bpmn, Guid.NewGuid(), 0, bytes));
        Assert.ThrowsAny<ArgumentException>(() => new ModelSnapshot("a", SourceModelKind.Bpmn, Guid.Empty, 0, bytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModelSnapshot("a", SourceModelKind.Bpmn, Guid.NewGuid(), -1, bytes));
        Assert.ThrowsAny<ArgumentException>(() => new ModelSnapshot("a", SourceModelKind.Bpmn, Guid.NewGuid(), 0, []));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ModelSnapshot("a", (SourceModelKind)999, Guid.NewGuid(), 0, bytes));
    }

    [Fact]
    public void ProviderMethods_RequireCancellationAndWriteContext()
    {
        foreach (var type in new[] { typeof(IModelSourceControlProvider), typeof(IRepositoryHostingProvider) })
        {
            foreach (var method in type.GetMethods())
            {
                Assert.Equal(typeof(CancellationToken), method.GetParameters()[^1].ParameterType);
                Assert.False(method.GetParameters()[^1].HasDefaultValue);
                if (method.Name == "GetAvailabilityAsync") continue;
                Assert.Equal(typeof(SourceControlContext), method.GetParameters()[0].ParameterType);
                Assert.Equal(typeof(RepositoryBinding), method.GetParameters()[1].ParameterType);
            }
        }
    }

    [Fact]
    public void ProviderContracts_KeepGitAndHostingResponsibilitiesSeparate()
    {
        Assert.DoesNotContain(typeof(IModelSourceControlProvider).GetMethods(), m => m.Name.Contains("PullRequest"));
        Assert.DoesNotContain(typeof(IRepositoryHostingProvider).GetMethods(), m => m.Name is "CommitAsync" or "PushAsync");
        Assert.DoesNotContain(typeof(IModelSourceControlProvider).GetMethods(), m => m.Name.Contains("Execute"));
    }

    [Fact]
    public void Abstractions_DoNotReferenceApiEfAspireOrGitProcessLibraries()
    {
        var names = typeof(IModelSourceControlProvider).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.DoesNotContain(names, name => name.StartsWith("VertexBPMN.Api", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name.StartsWith("Aspire", StringComparison.Ordinal));
        Assert.DoesNotContain(names, name => name is "System.Diagnostics.Process" or "LibGit2Sharp");
    }

    [Fact]
    public void ReadAndDeployPermissions_AreIndependentFlags()
    {
        Assert.False(RepositoryPermission.Read.HasFlag(RepositoryPermission.Deploy));
        Assert.False(RepositoryPermission.Commit.HasFlag(RepositoryPermission.Push));
        Assert.False(RepositoryPermission.Manage.HasFlag(RepositoryPermission.Deploy));
    }

    [Fact]
    public void Options_DoNotEnableGitOrChooseHostPathsByDefault()
    {
        var options = new SourceControlOptions();
        Assert.False(options.Enabled);
        Assert.Null(options.GitExecutablePath);
        Assert.Null(options.WorkspaceRoot);
        Assert.Empty(options.AllowedHosts);
        Assert.Equal("vertex/", options.WorkBranchPrefix);
    }

    [Fact]
    public void Limits_AreExplicitAndBoundedBeforeRuntimeImplementation()
    {
        var limits = new SourceControlLimits();
        Assert.Equal(2 * 1024 * 1024, limits.MaxModelBytes);
        Assert.Equal(256L * 1024 * 1024, limits.MaxRepositoryBytes);
        Assert.Equal(100, limits.MaxPageSize);
        Assert.Equal(2, limits.MaxConcurrentJobsPerTenant);
        Assert.Equal(8, limits.MaxConcurrentJobsTotal);
        Assert.Equal(TimeSpan.FromSeconds(15), limits.ReadTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), limits.WriteTimeout);
    }
}
