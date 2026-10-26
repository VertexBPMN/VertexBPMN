using System.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Portal.Client.Rendering;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Applications;

/// <summary>
/// LC07 S4 preview: renders the CURRENT DRAFT (only allowlist components) against an
/// EXPLICITLY selected test target using REDACTED TEST DATA. The preview is strictly
/// read-only and never executes a task/process/external action — it carries the rendered
/// tree + a redacted data snapshot as pure data and nothing that could fire a productive
/// write, start, claim, complete or external action. This mirrors the LC06 released
/// renderer allowlist (same closed set, same UnsupportedComponent posture) rather than
/// introducing an ad-hoc renderer.
/// </summary>
public sealed class AppDesignerPreviewTests
{
    private static string DraftWithDataSourceAndAction()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount"]);
        document.AddDataSource("customerList", 2, ["name"]);
        var page = document.AddPage("home");
        document.AddComponent(page.Id, "text");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "data-source-read");
        document.AddActionInput(page.Id, button.Id, "maxRecords", "50");
        document.AddComponent(page.Id, "table");
        return document.ToJson();
    }

    [Fact]
    public void Preview_RendersAllowlistComponents_FromDraft()
    {
        var json = DraftWithDataSourceAndAction();
        var preview = AppDesignerPreview.Build(json, new PreviewTestTarget("t1", "invoiceList"));

        Assert.True(preview.ReadOnly);
        Assert.Equal(0, preview.UnsupportedCount);
        Assert.Single(preview.Pages);

        var page = preview.Pages[0];
        Assert.Equal(["text", "button", "table"], page.Components.Select(n => n.Type).ToArray());
        Assert.All(page.Components, n => Assert.Equal(PreviewKind.Known, n.Kind));
    }

    [Fact]
    public void Preview_Allowlist_MatchesReleasedRenderer()
    {
        // The preview must reuse the SAME closed allowlist as the LC06 released renderer —
        // no second, divergent rendering mechanism.
        Assert.True(AppDesignerPreview.AllowlistForTest.SetEquals(ReleasedRenderer.Allowlist));

        var json = DraftWithDataSourceAndAction();
        var preview = AppDesignerPreview.Build(json, new PreviewTestTarget("t1", "invoiceList"));
        Assert.Equal(ReleasedRenderer.Render(json).UnsupportedCount, preview.UnsupportedCount);
        Assert.Equal(0, preview.UnsupportedCount);
    }

    [Fact]
    public void Preview_UsesRedactedTestData_ForBoundDataSources()
    {
        var json = DraftWithDataSourceAndAction();
        var preview = AppDesignerPreview.Build(json, new PreviewTestTarget("t1", "invoiceList"));

        Assert.Equal(2, preview.DataSets.Count);
        var invoice = preview.DataSets.Single(ds => ds.Key == "invoiceList");
        Assert.Equal(["number", "amount"], invoice.Fields);

        // Rows carry clearly-redacted placeholder values, never real data.
        Assert.NotEmpty(invoice.RedactedRows);
        foreach (var row in invoice.RedactedRows)
        {
            Assert.Equal(2, row.Count);
            foreach (var cell in row)
            {
                Assert.StartsWith(AppDesignerPreview.RedactionPrefix, cell, StringComparison.Ordinal);
                Assert.EndsWith("]", cell, StringComparison.Ordinal);
                Assert.DoesNotContain("secret", cell, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void Preview_RequiresExplicitTestTarget()
    {
        var json = DraftWithDataSourceAndAction();
        Assert.Throws<InvalidOperationException>(() => AppDesignerPreview.Build(json, null));
        Assert.Throws<InvalidOperationException>(() => AppDesignerPreview.Build(json, new PreviewTestTarget("t1", "")));
    }

    [Fact]
    public void Preview_IsReadOnly_AndSideEffectingActionsAreMarkedNonExecuting()
    {
        var document = AppDesignerDocument.Empty;
        document.SetProcessCatalog(["lc07-s4-flow"]);
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "start-process");
        document.SetActionTarget(page.Id, button.Id, "lc07-s4-flow");
        var json = document.ToJson();

        var preview = AppDesignerPreview.Build(json, new PreviewTestTarget("t1", "standard"));

        Assert.True(preview.ReadOnly);
        var node = Assert.Single(preview.Pages).Components.Single();
        Assert.True(node.HasAction);
        Assert.True(node.ActionIsSideEffecting); // would start a process in production
        // The preview carries the action purely as inert metadata: nothing to execute.
        Assert.Empty(preview.ExecutableActions);
    }

    [Fact]
    public void Preview_UnknownComponent_IsUnsupportedPlaceholder()
    {
        const string json = """
            {
              "pages": [
                { "id": "home", "route": "/home", "components": [
                    { "type": "text", "version": "1.0.0", "label": "OK" },
                    { "type": "evil-widget", "version": "9.9.9", "payload": "x" }
                ] }
              ]
            }
            """;

        var preview = AppDesignerPreview.Build(json, new PreviewTestTarget("t1", "standard"));

        Assert.Equal(1, preview.UnsupportedCount);
        var unsupported = Assert.Single(preview.Pages).Components.Single(n => n.Kind == PreviewKind.Unsupported);
        Assert.Equal("evil-widget", unsupported.Type);
        // The redacted data snapshot is always empty without a declared data source: nothing leaks.
        Assert.Empty(preview.DataSets);
    }

    [Fact]
    public void StartProcessDraft_SerializesReferences_AndPassesDomainGate()
    {
        const string key = "lc07s4flow-repro";
        var document = AppDesignerDocument.Empty.SetProcessCatalog(new[] { key });
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddDataSource("invoiceList", fields: new List<string> { "number", "amount" });
        document.AddAction(page.Id, button.Id, "start-process");
        document.SetActionTarget(page.Id, button.Id, key);
        var content = document.ToJson();
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(content));
        Assert.Contains("\"type\":\"start-process\",\"version\":\"1.0.0\",\"processDefinitionId\":\"lc07s4flow-repro\"", content, StringComparison.Ordinal);
    }
}
