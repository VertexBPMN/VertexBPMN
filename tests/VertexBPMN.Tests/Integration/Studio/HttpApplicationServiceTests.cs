using Microsoft.AspNetCore.Mvc.Testing;
using Moq;
using VertexBPMN.Studio.Services;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Integration.Studio;

/// <summary>
/// LC07 S1: the Studio HttpApplicationService runs against the REAL ApplicationController
/// + persistence (via the CustomWebApplicationFactory). No HTTP mocks - this exercises
/// list/create/get/save through the actual API + SQLite persistence.
/// </summary>
public sealed class HttpApplicationServiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string TenantA = "tenant-a";

    private readonly HttpApplicationService _service;

    public HttpApplicationServiceTests(CustomWebApplicationFactory factory)
    {
        var realClient = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("http://localhost") });
        realClient.Timeout = TimeSpan.FromSeconds(30);
        var realFactory = new Mock<IHttpClientFactory>();
        realFactory.Setup(value => value.CreateClient("VertexBPMN.Api")).Returns(realClient);
        _service = new HttpApplicationService(realFactory.Object);
    }

    [Fact]
    public async Task SaveDraft_WithDeclaredDataSourceBoundTableAndMappedField_RoundtripsThroughRealApi()
    {
        // LC07 S2: build a draft with a declared data source, a bound table and a schema-validated
        // field mapping via the designer document model, persist it through the real API + SQLite
        // persistence, reload and assert the full declare -> bind -> map -> save -> reload round-trip.
        var document = VertexBPMN.Studio.Services.AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount", "customer"]);
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");
        document.BindComponent(page.Id, table.Id, "invoiceList");
        document.MapField(page.Id, table.Id, "number", "Number");
        var content = document.ToJson();

        var key = $"lc07s2_{Guid.NewGuid():N}";
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreateAsync(TenantA, key, "LC07 data binding draft", content, ct);
        await _service.SaveDraftAsync(created.Id, TenantA, key, "LC07 data binding draft", expectedVersion: 1, content, ct);

        var reloaded = await _service.GetDraftAsync(created.Id, TenantA, ct);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Version);
        Assert.Equal(content, reloaded.Content);

        // The stored content still carries the declared source, the bound table and the mapping.
        Assert.Contains("\"dataSources\":[{\"key\":\"invoiceList\"", reloaded.Content, StringComparison.Ordinal);
        Assert.Contains("\"dataSource\":\"invoiceList\"", reloaded.Content, StringComparison.Ordinal);
        Assert.Contains("\"sourcePath\":\"number\",\"targetKey\":\"Number\"", reloaded.Content, StringComparison.Ordinal);
        Assert.Equal("[]", VertexBPMN.Domain.Entities.ApplicationDomainDiagnostics.Validate(reloaded.Content));
    }

    [Fact]
    public async Task SaveDraft_WithConfiguredAction_RoundtripsThroughRealApi()
    {
        // LC07 S3: add a button with a configured data-source-read action (reference target
        // -> declared source + explicit input), persist through the real API, reload and assert
        // the whole add-action -> set-reference -> set-input -> save -> reload round-trip.
        var document = VertexBPMN.Studio.Services.AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount", "customer"]);
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "data-source-read");
        document.SetActionTarget(page.Id, button.Id, "invoiceList");
        document.AddActionInput(page.Id, button.Id, "maxRecords", "50");
        var content = document.ToJson();

        var key = $"lc07s3_{Guid.NewGuid():N}";
        var ct = TestContext.Current.CancellationToken;
        var created = await _service.CreateAsync(TenantA, key, "LC07 action draft", content, ct);
        await _service.SaveDraftAsync(created.Id, TenantA, key, "LC07 action draft", expectedVersion: 1, content, ct);

        var reloaded = await _service.GetDraftAsync(created.Id, TenantA, ct);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Version);
        Assert.Equal(content, reloaded.Content);

        // The stored content still carries the configured action with its reference target + input.
        Assert.Contains("\"action\":{\"type\":\"data-source-read\",\"version\":\"1.0.0\",\"dataSource\":\"invoiceList\",\"inputs\":{\"maxRecords\":\"50\"}}", reloaded.Content, StringComparison.Ordinal);
        Assert.Equal("[]", VertexBPMN.Domain.Entities.ApplicationDomainDiagnostics.Validate(reloaded.Content));
    }

    [Fact]
    public async Task List_Create_GetDraft_Save_Roundtrip_ThroughRealApi()
    {
        var key = $"lc07svc_{Guid.NewGuid():N}";
        var ct = TestContext.Current.CancellationToken;

        // Fresh tenant: nothing listed yet.
        var before = await _service.ListAsync(TenantA, ct);
        Assert.DoesNotContain(before, summary => summary.Key == key);

        // CREATE an application.
        var created = await _service.CreateAsync(TenantA, key, "LC07 designer draft", "{\"localizations\":{},\"pages\":[]}", ct);
        Assert.Equal(key, created.Key);
        Assert.Equal(1, created.Version);

        // LIST includes it.
        var listed = await _service.ListAsync(TenantA, ct);
        Assert.Contains(listed, summary => summary.Id == created.Id && summary.Key == key && summary.Version == 1);

        // GET the draft content.
        var draft = await _service.GetDraftAsync(created.Id, TenantA, ct);
        Assert.NotNull(draft);
        Assert.Equal("{\"localizations\":{},\"pages\":[]}", draft.Content);

        // SAVE a new content revision.
        const string edited = "{\"localizations\":{},\"pages\":[{\"id\":\"home\",\"route\":\"/home\",\"components\":[{\"type\":\"text\",\"version\":\"1.0.0\"}]}]}";
        await _service.SaveDraftAsync(created.Id, TenantA, key, "LC07 designer draft", expectedVersion: 1, edited, ct);

        // Reload shows the saved revision.
        var reloaded = await _service.GetDraftAsync(created.Id, TenantA, ct);
        Assert.NotNull(reloaded);
        Assert.Equal(2, reloaded.Version);
        Assert.Equal(edited, reloaded.Content);
    }
}
