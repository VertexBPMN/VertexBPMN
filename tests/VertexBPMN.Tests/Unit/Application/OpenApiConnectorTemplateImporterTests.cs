using System.Net;
using VertexBPMN.Application.Connectors;
using VertexBPMN.Application.Import;

namespace VertexBPMN.Tests.Unit.Application;

public sealed class OpenApiConnectorTemplateImporterTests
{
    private const string Spec = """
    {
      "openapi": "3.0.0",
      "info": { "title": "Demo", "version": "1.0" },
      "servers": [ { "url": "https://api.example.com/v1" } ],
      "paths": {
        "/users/{id}": {
          "get": {
            "operationId": "getUserById",
            "parameters": [ { "name": "id", "in": "path", "required": true, "schema": { "type": "integer" } } ],
            "security": [ { "bearerAuth": [] } ]
          }
        },
        "/orders": {
          "post": {
            "operationId": "createOrder",
            "security": [],
            "requestBody": { "content": { "application/json": { "schema": { "type": "object", "properties": { "qty": { "type": "integer" } } } } } }
          }
        },
        "/legacy": {
          "delete": {
            "parameters": [ { "name": "x", "in": "query", "schema": { "type": "string" } } ]
          }
        },
        "/oauth": {
          "get": { "operationId": "getOauth", "security": [ { "oauth": [] } ] }
        }
      },
      "components": {
        "securitySchemes": {
          "bearerAuth": { "type": "http", "scheme": "bearer" },
          "oauth": { "type": "oauth2", "flows": { "clientCredentials": { "tokenUrl": "/token" } } }
        }
      }
    }
    """;

    private readonly OpenApiConnectorTemplateImporter _importer = new();

    [Fact]
    public void Import_MapsPathsToHttpTemplates_WithMethodEndpointAndAuth()
    {
        var result = _importer.Import(Spec, "tenant-a");

        Assert.Equal(3, result.Templates.Count);
        Assert.Equal(4, result.Report.Count);

        var user = result.Templates.Single(t => t.Name == "getUserById");
        Assert.Equal("http", user.Runtime);
        Assert.Contains(user.AppliesTo, x => x == "serviceTask");
        Assert.Equal("get", user.Properties.Single(p => p.Key == "vertex:connector.method").DefaultValue);
        Assert.Equal("https://api.example.com/v1/users/{id}", user.Properties.Single(p => p.Key == "vertex:connector.endpoint").DefaultValue);
        Assert.Equal("Bearer", user.Properties.Single(p => p.Key == "vertex:connector.authScheme").DefaultValue);
        var idParam = user.Properties.Single(p => p.Key == "id");
        Assert.True(idParam.Required);
        Assert.Equal("number", idParam.Type);
    }

    [Fact]
    public void Import_FlagsComplexBodyAndOAuthAsNeedsReview()
    {
        var result = _importer.Import(Spec, "tenant-a");

        var order = result.Report.Single(r => r.OperationId == "createOrder");
        Assert.Equal(N8nImportDisposition.NeedsReview, order.Disposition);

        var oauth = result.Report.Single(r => r.OperationId == "getOauth");
        Assert.Equal(N8nImportDisposition.NeedsReview, oauth.Disposition);
        Assert.Contains("OAuth2", oauth.Message);
    }

    [Fact]
    public void Import_ReportsUnsupportedWhenOperationIdMissing()
    {
        var result = _importer.Import(Spec, "tenant-a");

        var legacy = result.Report.Single(r => r.OperationId == "DELETE /legacy");
        Assert.Equal(N8nImportDisposition.Unsupported, legacy.Disposition);
        Assert.DoesNotContain(result.Templates, t => t.Name == "DELETE /legacy");
    }

    [Fact]
    public void Import_RejectsYamlWithJsonOnlyMessage()
    {
        var exception = Assert.Throws<ArgumentException>(() => _importer.Import("openapi: 3.0.0\npaths: {}", "tenant-a"));
        Assert.Contains("JSON", exception.Message);
    }

    [Fact]
    public async Task ImportedTemplate_ExecutesAgainstHttpEndpoint_WithResolvedPath()
    {
        // Phase 1 §1.7: an imported connector template must be runnable end-to-end.
        // SSRF-Guard blocks real loopback, so we drive HttpConnectorExecutor through a
        // recording HttpMessageHandler and assert the rendered URL + method from the import.
        var user = _importer.Import(Spec, "tenant-a").Templates.Single(t => t.Name == "getUserById");
        var method = user.Properties.Single(p => p.Key == "vertex:connector.method").DefaultValue!;
        var endpoint = user.Properties.Single(p => p.Key == "vertex:connector.endpoint").DefaultValue!;

        Uri? captured = null;
        using var httpClient = new HttpClient(new RecordingHandler(r => captured = r.RequestUri));
        var executor = new HttpConnectorExecutor(httpClient);
        var context = new ConnectorExecutionContext(
            "tenant-a", "http", user.Name, new Uri(endpoint),
            new Dictionary<string, string> { ["vertex:connector.method"] = method },
            new Dictionary<string, object> { ["id"] = 42 },
            new ConnectorRetryPolicy(0, TimeSpan.Zero, TimeSpan.Zero));

        var result = await executor.ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.True(result.Success);
        Assert.Equal("https://api.example.com/v1/users/42", captured!.ToString());
    }

    private sealed class RecordingHandler(Action<HttpRequestMessage> onRequest) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            onRequest(request);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
