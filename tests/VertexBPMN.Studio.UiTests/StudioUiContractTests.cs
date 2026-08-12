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
}
