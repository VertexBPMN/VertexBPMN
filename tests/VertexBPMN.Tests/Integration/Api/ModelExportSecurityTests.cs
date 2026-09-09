using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using VertexBPMN.Application;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

[Collection("IntegratedApi")]
public sealed class ModelExportSecurityTests(CustomWebApplicationFactory factory, SharedSqliteDbFixture dbFixture, ITestOutputHelper output)
{
    [Fact]
    public async Task ApiExport_RedactsInlineSecrets_PreservesStoredModel_AndRejectsRedactedRedeploy()
    {
        var host = factory.WithSharedFixture(dbFixture);
        using var client = host.CreateClient(output);
        var key = "export-" + Guid.NewGuid().ToString("N");
        var xml = $"""
            <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
              <process id="{key}" isExecutable="true">
                <extensionElements><vertex:config password="never-export-this" credentialRef="keep-this-reference" /></extensionElements>
                <startEvent id="start"/><sequenceFlow id="flow" sourceRef="start" targetRef="end"/><endEvent id="end"/>
              </process>
            </definitions>
            """;
        var ct = TestContext.Current.CancellationToken;
        using var deployed = await client.PostAsJsonAsync("/api/repository", new { bpmnXml = xml, name = key }, ct);
        Assert.Equal(HttpStatusCode.Created, deployed.StatusCode);
        var deployedBody = await deployed.Content.ReadAsStringAsync(ct);
        Assert.DoesNotContain("never-export-this", deployedBody);
        using var json = JsonDocument.Parse(deployedBody);
        var id = json.RootElement.GetProperty("id").GetGuid();
        foreach (var path in new[] { $"/api/repository/{id}", $"/api/vertex/process-definition/{id}/xml", "/api/repository?key=" + key })
        {
            var body = await client.GetStringAsync(path, ct);
            Assert.DoesNotContain("never-export-this", body);
            Assert.Contains("keep-this-reference", body);
            Assert.Contains(ModelExportRedaction.Marker, body);
        }
        using var scope = host.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<IRepositoryService>().GetByIdAsync(id, ct);
        Assert.Equal(xml, stored!.BpmnXml);
        using var redeploy = await client.PostAsJsonAsync("/api/repository",
            new { bpmnXml = json.RootElement.GetProperty("bpmnXml").GetString(), name = key }, ct);
        Assert.Equal(HttpStatusCode.BadRequest, redeploy.StatusCode);
    }

    [Theory]
    [InlineData("<model><input name='password'>sensitive-value</input></model>")]
    [InlineData("<model><property name='client_secret' value='sensitive-value'/></model>")]
    [InlineData("<model url='https://user:sensitive-value@example.org/'/>")]
    [InlineData("<model url='https://example.org/?api_key=sensitive-value'/>")]
    public void StructuredInlineSecretsAreRemoved(string xml)
    {
        var result = ModelExportRedaction.Redact(xml);
        Assert.DoesNotContain("sensitive-value", result);
        Assert.Contains(ModelExportRedaction.Marker, result);
    }

    [Fact]
    public void UnchangedModelsKeepExactXml()
    {
        const string xml = "<model secretRef='credential-1' credentialRef='credential-2' />";
        Assert.Equal(xml, ModelExportRedaction.Redact(xml));
    }
}
