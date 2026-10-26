using VertexBPMN.Domain.Entities;
using Xunit;

#pragma warning disable RCS1266 // JSON test fixtures require escaped quotes.

namespace VertexBPMN.Tests.Unit.Lc10.Domain;

public sealed class ReleaseReferenceGraphValidatorTests
{
    private const string Tenant = "t1";

    private static string Content(string referencesJson, string pagesJson = "[]") =>
        $"{{\"references\":{referencesJson},\"pages\":{pagesJson}}}";

    [Fact]
    public void MissingProcessArtifact_BlocksWithDiagnostic()
    {
        var content = Content("[{\"type\":\"process\",\"key\":\"Missing\",\"revision\":1}]");
        var result = ReleaseReferenceGraphValidator.Validate(content, _ => false, Tenant);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Diagnostics, d => d.Contains("Missing artifact 'process'", StringComparison.Ordinal));
    }

    [Fact]
    public void ForeignProcessArtifact_BlocksWithDiagnostic()
    {
        var content = Content("[{\"type\":\"process\",\"key\":\"p\",\"revision\":1,\"tenantId\":\"other\"}]");
        var result = ReleaseReferenceGraphValidator.Validate(content, _ => true, Tenant);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Diagnostics, d => d.Contains("Foreign artifact", StringComparison.Ordinal));
    }

    [Fact]
    public void MissingFormRevision_Blocks()
    {
        var content = Content("[{\"type\":\"form\",\"key\":\"MissingForm\",\"revision\":3}]");
        var result = ReleaseReferenceGraphValidator.Validate(content, _ => false, Tenant);

        Assert.False(result.IsCompatible);
        Assert.Contains(result.Diagnostics, d => d.Contains("'form'", StringComparison.Ordinal));
    }

    [Fact]
    public void AllPresentAndSameTenant_IsCompatible()
    {
        var content = Content(
            "[{\"type\":\"process\",\"key\":\"p\",\"revision\":1},{\"type\":\"form\",\"key\":\"f\",\"revision\":1,\"tenantId\":\"t1\"}]",
            "[{\"components\":[{\"id\":\"c1\",\"version\":\"1.2\"}]}]");

        var result = ReleaseReferenceGraphValidator.Validate(content, _ => true, Tenant);

        Assert.True(result.IsCompatible);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void ExtractReferenceProbes_IncludesUiComponents()
    {
        var content = Content("[]", "[{\"components\":[{\"id\":\"c1\",\"version\":\"2\"},{\"id\":\"c2\",\"version\":\"abc\"}]}]");
        var probes = ReleaseReferenceGraphValidator.ExtractReferenceProbes(content);

        Assert.Contains(probes, p => p.Kind == "ui-component" && p.Revision == 2);
        // Non-numeric UI version degrades to revision 1.
        Assert.Contains(probes, p => p.Kind == "ui-component" && p.Revision == 1);
    }
}
