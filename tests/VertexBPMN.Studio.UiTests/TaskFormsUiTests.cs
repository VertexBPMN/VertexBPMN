using Microsoft.Playwright;
using Xunit;

namespace VertexBPMN.Studio.UiTests;

public sealed class TaskFormsUiTests : IAsyncLifetime
{
    private readonly StudioUiTestHost _host = new() { TaskFormsEnabled = true };
    public ValueTask InitializeAsync() => _host.InitializeAsync();
    public ValueTask DisposeAsync() => _host.DisposeAsync();

    [Fact]
    public async Task SnapshotForm_PrefillsAuthorizedData_ValidatesAndPostsOnlyEditableOutputs()
    {
        var page = await _host.Browser.NewPageAsync();
        await page.GotoAsync(new Uri(_host.BaseAddress, "tasks").ToString());
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Task Form", Exact = true }).ClickAsync();
        var note = page.GetByLabel("Snapshot note", new() { Exact = false });
        await note.WaitForAsync();
        Assert.Equal("Prefilled note", await note.InputValueAsync());
        var invoice = page.GetByLabel("Invoice context", new() { Exact = true });
        Assert.Equal("INV-001", await invoice.InputValueAsync());
        Assert.NotNull(await invoice.GetAttributeAsync("readonly"));
        await note.FillAsync(string.Empty);
        await note.BlurAsync();
        await page.WaitForTimeoutAsync(400);
        await page.GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true }).ClickAsync();
        await page.GetByText("Error completing task:", new() { Exact = false }).WaitForAsync();
        Assert.Empty(_host.TaskCompletions);
        await note.FillAsync("Approved in browser");
        await note.BlurAsync();
        await page.WaitForTimeoutAsync(400);
        await page.GetByRole(AriaRole.Button, new() { Name = "Complete", Exact = true }).ClickAsync();
        await page.GetByText("Task Approve invoice completed!", new() { Exact = true }).WaitForAsync();
        using var document = System.Text.Json.JsonDocument.Parse(Assert.Single(_host.TaskCompletions));
        Assert.Equal(7L, document.RootElement.GetProperty("expectedRevision").GetInt64());
        Assert.Equal("snapshot-hash-1", document.RootElement.GetProperty("formSchemaHash").GetString());
        Assert.Equal("tenant-a", document.RootElement.GetProperty("tenantId").GetString());
        var output = document.RootElement.GetProperty("variables");
        Assert.Equal("Approved in browser", output.GetProperty("note").GetString());
        Assert.False(output.TryGetProperty("invoice", out _));
        Assert.False(output.TryGetProperty("secret", out _));
        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(_host.TaskCompletionKeys)));
    }

    [Fact]
    public async Task BoundTask_ListAction_OpensFormInsteadOfCompleting()
    {
        var page = await _host.Browser.NewPageAsync();
        await page.GotoAsync(new Uri(_host.BaseAddress, "tasks").ToString());
        await page.GetByText("Approve invoice", new() { Exact = true }).WaitForAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Open Task Form", Exact = true }).ClickAsync();
        await page.GetByText("Task Form", new() { Exact = true }).WaitForAsync();
        Assert.Empty(_host.TaskCompletions);
        Assert.Equal(0, await page.GetByRole(AriaRole.Button, new() { Name = "Complete Task", Exact = true }).CountAsync());
    }
}
