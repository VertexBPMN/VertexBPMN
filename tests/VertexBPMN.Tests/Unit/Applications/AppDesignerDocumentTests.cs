using System.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Portal.Client.Rendering;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Applications;

/// <summary>LC07 S1: the low-code designer authoring model enforces the CLOSED renderer allowlist.</summary>
public sealed class AppDesignerDocumentTests
{
    private static readonly string[] ReleasedAllowlist = ["table", "detail-card", "text", "form-js", "button", "status-diagram", "page-container"];

    [Fact]
    public void Empty_Document_IsValidReleasedContent()
    {
        var json = AppDesignerDocument.Empty.ToJson();
        ApplicationComponentCatalog.Validate(json); // must not throw for a designable draft
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));
        var rendered = ReleasedRenderer.Render(json);
        Assert.Equal(0, rendered.UnsupportedCount);
        Assert.Empty(rendered.Pages);
    }

    [Fact]
    public void Palette_Matches_The_Closed_Renderer_Allowlist()
    {
        Assert.Equal(ReleasedAllowlist.OrderBy(x => x, StringComparer.Ordinal).ToArray(), AppDesignerDocument.GetPaletteTypes().ToArray(), StringComparer.Ordinal);
    }

    [Fact]
    public void AddPage_AddComponent_EditProperty_ProducesValidLc06ShapedContent()
    {
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("Home");
        var text = document.AddComponent(page.Id, "text");
        document.SetComponentProperty(page.Id, text.Id, "label", "Welcome");
        var button = document.AddComponent(page.Id, "button");
        document.SetComponentProperty(page.Id, button.Id, "label", "Claim task");

        var json = document.ToJson();
        Assert.Contains("\"route\":\"/home\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"text\",\"version\":\"1.0.0\",\"label\":\"Welcome\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"button\",\"version\":\"1.0.0\",\"label\":\"Claim task\"", json, StringComparison.Ordinal);

        // The produced ContentJson must pass the released validation semantics and render cleanly.
        ApplicationComponentCatalog.Validate(json); // must not throw
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));
        var rendered = ReleasedRenderer.Render(json);
        Assert.Equal(0, rendered.UnsupportedCount);
        Assert.Single(rendered.Pages);
        Assert.Equal(2, rendered.Pages[0].Components.Count);
    }

    [Fact]
    public void DataSourceProperty_IsEmitted_And_FlaggedUntilDeclared()
    {
        // S1 scaffold: a table can be authored with its sanctioned renderer 'dataSource'
        // prop, but the released validator flags it UNKNOWN_DATA_SOURCE until the source
        // is declared under dataSources - that declaration is a later slice (out of S1 scope).
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");
        document.SetComponentProperty(page.Id, table.Id, "dataSource", "invoiceList");

        var json = document.ToJson();
        Assert.Contains("\"dataSource\":\"invoiceList\"", json, StringComparison.Ordinal);
        ApplicationComponentCatalog.Validate(json); // must not throw (component is allowlisted)
        Assert.Contains("UNKNOWN_DATA_SOURCE", ApplicationDomainDiagnostics.Validate(json), StringComparison.Ordinal);
    }

    [Fact]
    public void AddComponent_UnknownType_IsRejected()
    {
        var page = AppDesignerDocument.Empty.AddPage("home");
        var exception = Assert.Throws<InvalidOperationException>(() => AppDesignerDocument.Empty.AddComponent(page.Id, "evil-widget"));
        Assert.Contains("Unsupported component type", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetComponentProperty_UnknownProperty_IsRejected()
    {
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("home");
        var text = document.AddComponent(page.Id, "text");
        var exception = Assert.Throws<InvalidOperationException>(() => document.SetComponentProperty(page.Id, text.Id, "onClick", "alert(1)"));
        Assert.Contains("Unsupported property", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SetComponentProperty_UnsupportedVersion_IsRejected()
    {
        // A component authored at an unsupported version is flagged, never emitted.
        var document = AppDesignerDocument.Parse("{\"pages\":[{\"id\":\"home\",\"route\":\"/home\",\"components\":[{\"type\":\"text\",\"version\":\"9.9.9\"}]}]}");
        var page = document.Pages[0];
        var component = page.Components[0];
        var exception = Assert.Throws<InvalidOperationException>(() => document.SetComponentProperty(page.Id, component.Id, "label", "x"));
        Assert.Contains("Unsupported component version", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemovePage_And_ReorderPages_Persist()
    {
        var document = AppDesignerDocument.Empty;
        var home = document.AddPage("Home");
        document.AddPage("Tasks");
        var settings = document.AddPage("Settings");

        document.MovePage(settings.Id, 0);
        Assert.Equal(["settings", "home", "tasks"], document.Pages.Select(page => page.Id).ToArray());

        document.RemovePage(home.Id);
        Assert.Equal(["settings", "tasks"], document.Pages.Select(page => page.Id).ToArray());
        var json = document.ToJson();
        ApplicationComponentCatalog.Validate(json); // must not throw
    }

    [Fact]
    public void RemoveAndReorderComponents_Persist()
    {
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("home");
        var text = document.AddComponent(page.Id, "text");
        document.AddComponent(page.Id, "button");
        var table = document.AddComponent(page.Id, "table");

        document.MoveComponent(page.Id, table.Id, 0);
        Assert.Equal(["table", "text", "button"], page.Components.Select(component => component.Type).ToArray());

        document.RemoveComponent(page.Id, text.Id);
        Assert.Equal(["table", "button"], page.Components.Select(component => component.Type).ToArray());
        var json = document.ToJson();
        ApplicationComponentCatalog.Validate(json); // must not throw
    }

    [Fact]
    public void Parse_Edit_Roundtrip_PreservesAllowlistShape()
    {
        const string seeded = "{\"pages\":[{\"id\":\"tasks\",\"route\":\"/tasks\",\"components\":[{\"type\":\"button\",\"version\":\"1.0.0\",\"label\":\"Claim\"}]}]}";
        var document = AppDesignerDocument.Parse(seeded);
        var page = document.Pages[0];
        Assert.Equal("/tasks", page.Route);
        var button = page.Components[0];
        Assert.Equal("button", button.Type);
        Assert.Equal("Claim", button.Label);

        document.SetComponentProperty(page.Id, button.Id, "label", "Complete");
        var json = document.ToJson();
        Assert.Contains("\"label\":\"Complete\"", json, StringComparison.Ordinal);
        ApplicationComponentCatalog.Validate(json); // must not throw
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));
    }
}
