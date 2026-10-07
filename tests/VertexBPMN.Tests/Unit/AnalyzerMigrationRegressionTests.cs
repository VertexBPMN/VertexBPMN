using System.Text.Json;
using System.Xml.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Performance;

namespace VertexBPMN.Tests.Unit;

public sealed class AnalyzerMigrationRegressionTests
{
    [Fact]
    public void ValidationException_ErrorConstructor_HasSafeDiagnostics()
    {
        var exception = new BpmnValidationException("invalid model", new List<string> { "missing start" });
        Assert.Empty(exception.Diagnostics);
        Assert.Single(exception.Errors);
        Assert.Contains("No validation diagnostics available.", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void ValidationException_RejectsNullErrors()
    {
        Assert.Throws<ArgumentNullException>(() => new BpmnValidationException("invalid", (List<string>)null!));
    }

    [Fact]
    public async Task LazyElement_ConcurrentFirstAccess_PublishesOneIndependentClone()
    {
        var original = XElement.Parse("<task id='one'><extension /></task>");
        var lazy = new LazyXElement(original);
        Assert.False(lazy.IsCloned);
        var readers = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => lazy.Element, TestContext.Current.CancellationToken)).ToArray();
        var elements = await Task.WhenAll(readers);
        Assert.True(lazy.IsCloned);
        Assert.NotSame(original, elements[0]);
        Assert.All(elements, element => Assert.Same(elements[0], element));
        elements[0].SetAttributeValue("id", "changed");
        Assert.Equal("one", (string?)original.Attribute("id"));
    }

    [Fact]
    public void UriOverloads_PreserveJsonWireContracts()
    {
        var configuration = new OAuth2AuthorizationConfig(new Uri("https://example.org/auth"),
            new Uri("https://example.org/token"), "studio", new Uri("https://example.org/callback"), "openid");
        Assert.Equal(configuration, JsonSerializer.Deserialize<OAuth2AuthorizationConfig>(JsonSerializer.Serialize(configuration)));
        var start = new OAuth2AuthorizationStart(new Uri("https://example.org/auth?state=one"), "one");
        Assert.Equal(start, JsonSerializer.Deserialize<OAuth2AuthorizationStart>(JsonSerializer.Serialize(start)));
        var prefix = new NamespacePrefix("bpmn", new Uri("http://www.omg.org/spec/BPMN/20100524/MODEL"));
        Assert.Equal(prefix, JsonSerializer.Deserialize<NamespacePrefix>(JsonSerializer.Serialize(prefix)));
    }

    [Fact]
    public void CacheKeys_PreserveExistingFormats()
    {
        var id = Guid.Parse("0f5b0ad2-a30c-483c-bde4-7bfbff9e8999");
        Assert.Equal($"process_def_{id}", CacheKeys.ProcessDefinitionById(id));
        Assert.Equal($"process_inst_{id}", CacheKeys.ProcessInstanceById(id));
        Assert.Equal("user_user-1", CacheKeys.UserById("user-1"));
        Assert.Equal("tenant_tenant-1", CacheKeys.TenantById("tenant-1"));
    }
}
