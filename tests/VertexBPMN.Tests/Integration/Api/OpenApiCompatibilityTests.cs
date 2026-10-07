using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Api;

public sealed class OpenApiCompatibilityTests
{
    [Fact]
    public async Task Swagger_generates_repository_contract_with_security_and_xml_comments()
    {
        await using var root = new CustomWebApplicationFactory();
        await using var host = root.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Modules:Swagger"] = "true"
                })));
        using var client = host.CreateClient();

        using var response = await client.GetAsync("/api/swagger/v1/swagger.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var json = document.RootElement;
        Assert.StartsWith("3.", json.GetProperty("openapi").GetString());
        var deployment = json.GetProperty("paths").GetProperty("/api/repository").GetProperty("post");
        Assert.Equal("Deploys a new BPMN process definition.", deployment.GetProperty("summary").GetString());
        Assert.True(deployment.GetProperty("requestBody").GetProperty("content")
            .GetProperty("application/json").TryGetProperty("schema", out _));
        Assert.True(deployment.GetProperty("responses").TryGetProperty("201", out _));
        var bearer = json.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        Assert.Equal("http", bearer.GetProperty("type").GetString());
        Assert.Equal("bearer", bearer.GetProperty("scheme").GetString());
        Assert.Contains(json.GetProperty("security").EnumerateArray(), requirement => requirement.TryGetProperty("Bearer", out _));
        Assert.Contains(json.GetProperty("tags").EnumerateArray(), tag => tag.GetProperty("name").GetString() == "Simulation");
    }
}
