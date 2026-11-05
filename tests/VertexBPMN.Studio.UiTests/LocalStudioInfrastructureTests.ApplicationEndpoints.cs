using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

#pragma warning disable MA0051
public sealed partial class LocalStudioInfrastructureTests
{
    [Fact]
    [Trait("Category", "LocalStudioE2E")]
    public async Task ApplicationDesigner_Endpoint_CreateEditSaveReloadPublishAndReadRealTasks()
    {
        Assert.SkipUnless(LocalStudioE2ETestHost.IsEnabled, "Requires isolated PostgreSQL/RabbitMQ.");
        var ct = TestContext.Current.CancellationToken;
        using var api = host.CreateApiClient();
        var key = "lc09b-" + host.RunId;
        const string initial = """
            {"localizations":{},"dataSources":[{"key":"inbox","bindingVersion":2,"query":"personal-task-inbox","fields":["id","name"]}],"pages":[{"id":"home","title":"Home","route":"/home","components":[{"type":"button","version":"1.0.0","label":"Read","action":{"type":"data-source-read","version":"1.0.0","dataSource":"inbox"}}]}]}
            """;
        using var created = await api.PostAsJsonAsync("api/applications", new { tenantId = host.TenantId, key, name = key, expectedVersion = 0, content = initial }, ct);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(ct)).GetProperty("id").GetString()!;
        host.RegisterApiCleanup(HttpMethod.Delete, $"api/applications/{id}?tenantId={host.TenantId}");
        var page = await host.CreatePageAsync();
        try
        {
            await page.GotoAsync($"{host.StudioBaseAddress}applications");
            await SelectTenantAsync(page, host.AgentTenantName, host.TenantId);
            await page.EvaluateAsync("url => Blazor.navigateTo(url)", $"/applications/{id}/design");
            await page.GetByTestId("new-page-title").WaitForAsync();
            Assert.Equal(1, await page.GetByTestId("endpoint-key").CountAsync());
            await FillBoundInputAsync(page.GetByTestId("endpoint-key"), "search");
            await FillBoundInputAsync(page.GetByTestId("endpoint-route"), "/endpoints/search/{search}");
            await FillBoundInputAsync(page.GetByTestId("endpoint-parameters"), "search");
            await page.Locator("[data-testid=endpoint-action]:visible").ClickAsync();
            await page.GetByRole(AriaRole.Option, new() { Name = "home · data-source-read · inbox", Exact = true }).ClickAsync();
            await page.GetByTestId("endpoint-apply").ClickAsync();
            await page.GetByTestId("endpoint-node").WaitForAsync();
            await page.GetByTestId("endpoint-edit").ClickAsync();
            await FillBoundInputAsync(page.GetByTestId("endpoint-timeout"), "8");
            await page.GetByTestId("endpoint-apply").ClickAsync();
            await page.GetByTestId("save-draft").ClickAsync();
            await page.GetByText("Draft saved as revision 2.", new() { Exact = true }).WaitForAsync();
            using var draft = await api.GetFromJsonAsync<JsonDocument>($"api/applications/{id}?tenantId={host.TenantId}", ct);
            var content = draft!.RootElement.GetProperty("content").GetString()!;
            await AssertDesignerDatabaseAsync(id, host.TenantId, content);
            using var parsed = JsonDocument.Parse(content);
            Assert.Equal(8, parsed.RootElement.GetProperty("endpoints")[0].GetProperty("limits").GetProperty("timeoutSeconds").GetInt32());
            await page.GotoAsync($"{host.StudioBaseAddress}applications");
            await SelectTenantAsync(page, host.AgentTenantName, host.TenantId);
            await page.EvaluateAsync("url => Blazor.navigateTo(url)", $"/applications/{id}/design");
            await page.GetByTestId("endpoint-node").WaitForAsync();
            await page.GetByTestId("endpoint-edit").ClickAsync();
            Assert.Equal("8", await page.GetByTestId("endpoint-timeout").InputValueAsync());
            await page.GetByTestId("publish").ClickAsync();
            await page.GetByText("Revision 2 veröffentlicht.", new() { Exact = true }).WaitForAsync();
            using var runtime = await host.CreateEndpointRuntimeClientAsync(host.TenantId);
            using var response = await runtime.GetAsync($"api/apps/{key}/endpoints/search/Invoice", ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = await response.Content.ReadFromJsonAsync<JsonDocument>(ct);
            Assert.Single(body!.RootElement.GetProperty("items").EnumerateArray());
        }
        finally { await host.ClosePageAsync(page); }
    }
}
