using System.Text.Json.Nodes;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Applications;

public sealed class AppDesignerEndpointTests
{
    [Fact]
    public void EditingPages_PreservesEndpointAndRootMetadataThroughHistoryAndReload()
    {
        const string content = """
            {"localizations":{"de":{"title":"Hallo"}},"references":[{"type":"process","key":"orders","id":"definition-1","tenantId":"tenant-a"}],"release":{"id":"release-1"},"endpoints":[{"key":"inbox","method":"GET","route":"/endpoints/inbox","pathParameters":[],"inputSchema":{"type":"object"},"outputSchema":{"type":"object"},"permission":"AppUser","limits":{"timeoutSeconds":10,"maxRequestBytes":1024,"maxResponseBytes":4096},"action":{"pageId":"home","type":"data-source-read","version":"1.0.0","target":"personal-task-inbox"}}],"pages":[]}
            """;
        var expected = JsonNode.Parse(content)!;
        var document = AppDesignerDocument.Parse(content);
        document.AddPage("Home");
        AssertPreserved(expected, document);
        Assert.True(document.Undo());
        AssertPreserved(expected, document);
        Assert.True(document.Redo());
        AssertPreserved(expected, AppDesignerDocument.Parse(document.ToJson()));
    }

    [Fact]
    public void EditingComponent_PreservesPageActionsSourceQueryAndComponentMetadata()
    {
        const string content = """
            {"localizations":{},"dataSources":[{"key":"inbox","bindingVersion":2,"query":"personal-task-inbox","permission":"ReadOnly"}],"pages":[{"id":"home","route":"/home","title":"Inbox","actions":[{"type":"data-source-read","version":"1.0.0","dataSource":"inbox"}],"components":[{"id":"read-button","type":"button","version":"1.0.0","label":"Read","permission":"ReadOnly","action":{"type":"data-source-read","version":"1.0.0","dataSource":"inbox","permission":"ReadOnly"}}]}]}
            """;
        var document = AppDesignerDocument.Parse(content);
        var component = document.Pages[0].Components[0];
        document.SetComponentProperty("home", component.Id, "label", "Changed");
        var actual = JsonNode.Parse(document.ToJson())!;
        Assert.Equal("personal-task-inbox", actual["dataSources"]![0]!["query"]?.GetValue<string>());
        Assert.Equal("Inbox", actual["pages"]![0]!["title"]!.GetValue<string>());
        Assert.NotNull(actual["pages"]![0]!["actions"]);
        Assert.Equal("read-button", actual["pages"]![0]!["components"]![0]!["id"]!.GetValue<string>());
        Assert.Equal("ReadOnly", actual["pages"]![0]!["components"]![0]!["permission"]!.GetValue<string>());
        Assert.Equal("ReadOnly", actual["pages"]![0]!["components"]![0]!["action"]!["permission"]!.GetValue<string>());
    }

    [Fact]
    public void UpsertEndpoint_BindsExistingReadActionAndSupportsHistory()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("inbox");
        var page = document.AddPage("Home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "data-source-read");
        document.SetActionTarget(page.Id, button.Id, "inbox");
        var binding = new VertexBPMN.Domain.Entities.ApplicationEndpointActionBinding(page.Id, "data-source-read", "1.0.0", "inbox");
        var endpoint = new VertexBPMN.Domain.Entities.ApplicationEndpointDefinition("search", "GET", "/endpoints/search/{search}", ["search"],
            System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }),
            System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" }), "ReadOnly", new(5, 1024, 4096), binding);
        document.UpsertEndpoint(endpoint);
        Assert.Single(VertexBPMN.Domain.Entities.ApplicationEndpointMetadata.Read(document.ToJson()));
        Assert.True(document.Undo());
        Assert.Empty(VertexBPMN.Domain.Entities.ApplicationEndpointMetadata.Read(document.ToJson()));
        Assert.True(document.Redo());
        var updated = endpoint with { Permission = "ProcessManager" };
        document.UpsertEndpoint(updated);
        Assert.Equal("ProcessManager", Assert.Single(VertexBPMN.Domain.Entities.ApplicationEndpointMetadata.Read(document.ToJson())).Permission);
        Assert.True(document.Undo());
        Assert.Equal("ReadOnly", Assert.Single(VertexBPMN.Domain.Entities.ApplicationEndpointMetadata.Read(document.ToJson())).Permission);
    }

    [Theory]
    [InlineData("PUT", "/endpoints/search/{search}", "inbox", "ReadOnly", 5)]
    [InlineData("GET", "https://example.org", "inbox", "ReadOnly", 5)]
    [InlineData("GET", "/endpoints/search/{search}", "foreign", "ReadOnly", 5)]
    [InlineData("GET", "/endpoints/search/{search}", "inbox", "Invented", 5)]
    [InlineData("GET", "/endpoints/search/{search}", "inbox", "ReadOnly", 0)]
    public void UpsertEndpoint_RejectsInvalidConfigurationWithoutChangingDraft(string method, string route, string target, string permission, int timeout)
    {
        var document = AppDesignerDocument.Parse("""
            {"dataSources":[{"key":"inbox","bindingVersion":2}],"pages":[{"id":"home","route":"/home","actions":[{"type":"data-source-read","version":"1.0.0","dataSource":"inbox"}]}]}
            """);
        var schema = System.Text.Json.JsonSerializer.SerializeToElement(new { type = "object" });
        var endpoint = new VertexBPMN.Domain.Entities.ApplicationEndpointDefinition("search", method, route, ["search"], schema, schema, permission,
            new(timeout, 1024, 4096), new("home", "data-source-read", "1.0.0", target));
        var before = document.ToJson();
        Assert.Throws<VertexBPMN.Domain.Exceptions.ApplicationValidationException>(() => document.UpsertEndpoint(endpoint));
        Assert.Equal(before, document.ToJson());
        Assert.False(document.CanUndo);
    }

    private static void AssertPreserved(JsonNode expected, AppDesignerDocument document)
    {
        var actual = JsonNode.Parse(document.ToJson())!;
        Assert.True(JsonNode.DeepEquals(expected["endpoints"], actual["endpoints"]), "Endpoint metadata lost during document edit/roundtrip.");
        Assert.True(JsonNode.DeepEquals(expected["localizations"], actual["localizations"]));
        Assert.True(JsonNode.DeepEquals(expected["references"], actual["references"]));
        Assert.True(JsonNode.DeepEquals(expected["release"], actual["release"]));
    }
}
