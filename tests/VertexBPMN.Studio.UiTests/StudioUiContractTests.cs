using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed class StudioUiContractTests(StudioUiTestHost host) : IClassFixture<StudioUiTestHost>
{
    [Fact]
    public async Task Dashboard_Renders_And_Loads_Runtime_Contracts()
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync(host.BaseAddress.ToString());

        await page.GetByRole(AriaRole.Heading, new() { Name = "Dashboard", Exact = true }).WaitForAsync();
        await page.GetByText("Approve invoice", new() { Exact = true }).WaitForAsync();

        Assert.Contains(host.ApiRequests, request => request.StartsWith("GET /api/runtime", StringComparison.Ordinal));
        Assert.Contains(host.ApiRequests, request => request.StartsWith("GET /api/task", StringComparison.Ordinal));
        Assert.Contains(host.ApiRequests, request => request.StartsWith("GET /api/repository", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessDefinitions_Renders_RepositoryContractData()
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync($"{host.BaseAddress}process-definitions");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Process Definitions", Exact = true }).First.WaitForAsync();
        await page.GetByText("Key: InvoiceProcess", new() { Exact = true }).WaitForAsync();
        await page.GetByText("1-1 of 1", new() { Exact = true }).WaitForAsync();
    }

    [Fact]
    public async Task Tasks_Renders_TaskContractData()
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync($"{host.BaseAddress}tasks");

        await page.GetByRole(AriaRole.Heading, new() { Name = "Tasks", Exact = true }).WaitForAsync();
        await page.GetByText("Approve invoice", new() { Exact = true }).WaitForAsync();
    }

    [Theory]
    [InlineData("bpmn-modeler", "BPMN Modeler", "Deploy BPMN", "Export XML", "bpmn-modeler-shell", "Viewer", "bpmn-viewer-shell")]
    [InlineData("dmn-modeler", "DMN Modeler", "Deploy DMN", "Export DMN", "dmn-modeler-shell", "Viewer", "dmn-viewer-shell")]
    [InlineData("form-builder", "Form Builder", "Save JSON", "Export JSON", "form-builder-shell", "Runtime Viewer", "form-viewer-shell")]
    [InlineData("cmmn-modeler", "CMMN Modeler", "Register case model", "Export CMMN", "cmmn-modeler-shell", "Viewer", "cmmn-viewer-shell")]
    public async Task BpmnIoModelerShells_Render_EditorViewer_And_ActionButtons(
        string route,
        string heading,
        string primaryAction,
        string exportAction,
        string modelerTestId,
        string viewerTab,
        string viewerTestId)
    {
        var page = await host.Browser.NewPageAsync();
        await page.GotoAsync($"{host.BaseAddress}{route}");

        await page.GetByRole(AriaRole.Heading, new() { Name = heading, Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = primaryAction, Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = exportAction, Exact = true }).WaitForAsync();
        await page.GetByTestId(modelerTestId).WaitForAsync();
        await page.GetByText(viewerTab, new() { Exact = true }).WaitForAsync();
        await page.GetByTestId(viewerTestId).WaitForAsync();
    }

}
