using VertexBPMN.Application.Import;
using VertexBPMN.Domain.Interfaces;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class N8nWorkflowImporterTests
{
    [Fact]
    public void Import_DoesNotCreateCredentialReference_WhenTenantHasNoUniqueMatch()
    {
        const string workflow = """
        { "nodes": [
          { "name": "Request", "type": "n8n-nodes-base.httpRequest", "credentials": { "httpBasicAuth": { "id": "legacy-id" } } }
        ] }
        """;

        var result = new N8nWorkflowImporter().Import(workflow, []);

        Assert.DoesNotContain("credentialRef", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains(result.Report, item => item.Disposition == N8nImportDisposition.NeedsReview && item.Message.Contains("No credential reference", StringComparison.Ordinal));
    }

    [Fact]
    public void Import_MapsWebhookHttpIfAndReportsUnsupportedNodes()
    {
        const string workflow = """
        { "name": "Webhook flow", "nodes": [
          { "name": "Webhook", "type": "n8n-nodes-base.webhook" },
          { "name": "Request", "type": "n8n-nodes-base.httpRequest", "credentials": { "httpBasicAuth": { "id": "vertex-http", "name": "HTTP Basic" } } },
          { "name": "Check", "type": "n8n-nodes-base.if", "parameters": { "conditions": { "string": [{ "value1": "={{ $json.status }}", "operation": "equals", "value2": "approved" }] } } },
          { "name": "Unknown", "type": "n8n-nodes-base.code" }
        ], "connections": {
          "Webhook": { "main": [[{ "node": "Request" }]] },
          "Request": { "main": [[{ "node": "Check" }]] },
          "Check": { "main": [[{ "node": "Unknown" }], [{ "node": "Request" }]] }
        } }
        """;

        var credentials = new[] { new CredentialMetadata("vertex-http", "default", "HTTP Basic", "httpBasicAuth", null, [], DateTime.UtcNow, DateTime.UtcNow, null) };
        var result = new N8nWorkflowImporter().Import(workflow, credentials);

        Assert.Contains("message start", result.Report[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vertex:connector=\"http\"", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains("vertex:credentialRef=\"vertex-http\"", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains("exclusiveGateway", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains("status == \"approved\"", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains("default=\"flow_", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains(result.Report, item => item.Disposition == N8nImportDisposition.Migrated && item.NodeName == "Check");
        Assert.Contains(result.Report, item => item.Disposition == N8nImportDisposition.Unsupported);
        Assert.Contains("sequenceFlow", result.BpmnXml, StringComparison.Ordinal);
    }
}
