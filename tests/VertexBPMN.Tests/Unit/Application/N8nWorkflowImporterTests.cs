using VertexBPMN.Application.Import;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class N8nWorkflowImporterTests
{
    [Fact]
    public void Import_MapsWebhookHttpIfAndReportsUnsupportedNodes()
    {
        const string workflow = """
        { "name": "Webhook flow", "nodes": [
          { "name": "Webhook", "type": "n8n-nodes-base.webhook" },
          { "name": "Request", "type": "n8n-nodes-base.httpRequest", "credentials": { "httpBasicAuth": { "id": "legacy" } } },
          { "name": "Check", "type": "n8n-nodes-base.if" },
          { "name": "Unknown", "type": "n8n-nodes-base.code" }
        ], "connections": {
          "Webhook": { "main": [[{ "node": "Request" }]] },
          "Request": { "main": [[{ "node": "Check" }]] },
          "Check": { "main": [[{ "node": "Unknown" }]] }
        } }
        """;

        var result = new N8nWorkflowImporter().Import(workflow);

        Assert.Contains("message start", result.Report[0].Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("vertex:connector=\"http\"", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains("exclusiveGateway", result.BpmnXml, StringComparison.Ordinal);
        Assert.Contains(result.Report, item => item.Disposition == N8nImportDisposition.NeedsReview);
        Assert.Contains(result.Report, item => item.Disposition == N8nImportDisposition.Unsupported);
        Assert.Contains("sequenceFlow", result.BpmnXml, StringComparison.Ordinal);
    }
}
