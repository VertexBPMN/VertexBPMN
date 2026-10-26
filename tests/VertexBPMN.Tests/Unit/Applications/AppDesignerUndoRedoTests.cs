using System.Linq;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Applications;

/// <summary>
/// LC07 S4: a bounded command stack over the <see cref="AppDesignerDocument"/> draft.
/// Every authoring mutation (add/remove/reorder page, add/remove component, property
/// change, data-source change, action change, mapping change) is undoable/redoable.
/// Undo/redo restores the exact prior model state (component identities stay stable so
/// the selection does not dangle) and never lets a re-edit resurrect a stale snapshot.
/// The stack is bounded.
/// <para>Because restore re-materialises the tree, consumers must re-resolve pages and
/// components through <see cref="AppDesignerDocument.Pages"/> by stable identity (exactly
/// what the designer surface does via <c>_selectedPageId</c>/<c>_selectedComponentId</c>) —
/// raw object references captured before an undo are not a supported handle.</para>
/// </summary>
public sealed class AppDesignerUndoRedoTests
{
    private static DesignerPage Page(AppDesignerDocument document, string pageId)
        => document.Pages.Single(page => page.Id == pageId);

    private static DesignerComponent Component(AppDesignerDocument document, string pageId, string componentId)
        => Page(document, pageId).Components.Single(component => component.Id == componentId);

    [Fact]
    public void AddComponent_ThenUndo_RemovesIt_AndRedo_RestoresSameIdentity()
    {
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        var added = document.AddComponent(page.Id, "text").Id;

        Assert.True(document.CanUndo);
        Assert.True(document.Undo());
        Assert.Empty(Page(document, page.Id).Components);
        Assert.True(document.CanUndo); // page creation is its own undoable edit
        Assert.True(document.CanRedo);

        Assert.True(document.Redo());
        // The restored component keeps the SAME identity (no dangling selection).
        var restored = Component(document, page.Id, added);
        Assert.Equal(added, restored.Id);
        Assert.Equal("text", restored.Type);
    }

    [Fact]
    public void RemoveComponent_ThenUndo_RestoresIt_WithItsProperties()
    {
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        var text = Component(document, page.Id, document.AddComponent(page.Id, "text").Id);
        document.SetComponentProperty(page.Id, text.Id, "label", "Welcome");
        var id = text.Id;

        document.RemoveComponent(page.Id, id);
        Assert.Empty(Page(document, page.Id).Components);

        Assert.True(document.Undo());
        var restored = Component(document, page.Id, id);
        Assert.Equal(id, restored.Id);
        Assert.Equal("Welcome", restored.Label);
    }

    [Fact]
    public void PropertyChange_ThenUndo_Reverts_AndRedo_Reapplies()
    {
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        var text = Component(document, page.Id, document.AddComponent(page.Id, "text").Id);
        document.SetComponentProperty(page.Id, text.Id, "label", "Welcome");

        document.SetComponentProperty(page.Id, text.Id, "label", "Changed");
        Assert.Equal("Changed", Component(document, page.Id, text.Id).Label);

        Assert.True(document.Undo());
        Assert.Equal("Welcome", Component(document, page.Id, text.Id).Label);
        Assert.True(document.Undo());
        Assert.Null(Component(document, page.Id, text.Id).Label);
        Assert.True(document.Redo());
        Assert.Equal("Welcome", Component(document, page.Id, text.Id).Label);
    }

    [Fact]
    public void MoveComponent_ThenUndo_RestoresOriginalOrder()
    {
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        var text = Component(document, page.Id, document.AddComponent(page.Id, "text").Id);
        var button = Component(document, page.Id, document.AddComponent(page.Id, "button").Id);
        Component(document, page.Id, document.AddComponent(page.Id, "table").Id);

        document.MoveComponent(page.Id, button.Id, 2); // [text, table, button]
        Assert.Equal(["text", "table", "button"], Page(document, page.Id).Components.Select(c => c.Type).ToArray());

        Assert.True(document.Undo());
        Assert.Equal(["text", "button", "table"], Page(document, page.Id).Components.Select(c => c.Type).ToArray());
        Assert.Equal(text.Id, Page(document, page.Id).Components[0].Id);
    }

    [Fact]
    public void AddDataSource_ThenUndo_RemovesIt_AndRedo_RestoresDeclaredSource()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount"]);

        document.AddDataSource("customerList", 2, ["name"]);
        Assert.True(document.IsDataSourceDeclared("customerList"));

        Assert.True(document.Undo());
        Assert.False(document.IsDataSourceDeclared("customerList"));
        Assert.True(document.IsDataSourceDeclared("invoiceList"));

        Assert.True(document.Redo());
        Assert.True(document.IsDataSourceDeclared("customerList"));
    }

    [Fact]
    public void ActionAdd_ThenUndo_RemovesIt_AndRedo_RestoresTargetAndInput()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2);
        var page = Page(document, document.AddPage("home").Id);
        var button = Component(document, page.Id, document.AddComponent(page.Id, "button").Id);
        document.AddAction(page.Id, button.Id, "data-source-read");
        document.SetActionTarget(page.Id, button.Id, "invoiceList");

        document.AddActionInput(page.Id, button.Id, "maxRecords", "50");
        Assert.Equal("50", Component(document, page.Id, button.Id).Action!.Inputs.Single().Value);

        Assert.True(document.Undo()); // remove the input
        Assert.Empty(Component(document, page.Id, button.Id).Action!.Inputs);
        Assert.True(document.Undo()); // remove the target link
        Assert.Null(Component(document, page.Id, button.Id).Action!.Target);
        Assert.True(document.Undo()); // remove the action
        Assert.Null(Component(document, page.Id, button.Id).Action);

        Assert.True(document.Redo()); // restore the action (target cleared)
        Assert.NotNull(Component(document, page.Id, button.Id).Action);
        Assert.Equal("data-source-read", Component(document, page.Id, button.Id).Action!.Type);
        Assert.Null(Component(document, page.Id, button.Id).Action!.Target);
        Assert.True(document.Redo()); // restore the target link
        Assert.Equal("invoiceList", Component(document, page.Id, button.Id).Action!.Target);
    }

    [Fact]
    public void Mapping_ThenUndo_RemovesIt()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number"]);
        var page = Page(document, document.AddPage("home").Id);
        var table = Component(document, page.Id, document.AddComponent(page.Id, "table").Id);
        document.BindComponent(page.Id, table.Id, "invoiceList");
        document.MapField(page.Id, table.Id, "number", "Number");

        Assert.Single(Component(document, page.Id, table.Id).Mappings);
        Assert.True(document.Undo());
        Assert.Empty(Component(document, page.Id, table.Id).Mappings);
    }

    [Fact]
    public void ReorderPage_ThenUndo_RestoresOrder()
    {
        var document = AppDesignerDocument.Empty;
        var home = Page(document, document.AddPage("Home").Id);
        var tasks = Page(document, document.AddPage("Tasks").Id);
        Page(document, document.AddPage("Settings").Id);

        document.MovePage(tasks.Id, 0);
        Assert.Equal([tasks.Id, home.Id, "settings"], document.Pages.Select(p => p.Id).ToArray());
        Assert.True(document.Undo());
        Assert.Equal([home.Id, tasks.Id, "settings"], document.Pages.Select(p => p.Id).ToArray());
    }

    [Fact]
    public void NewEdit_AfterUndo_ClearsRedo()
    {
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        document.AddComponent(page.Id, "text");
        document.AddComponent(page.Id, "button");

        Assert.True(document.Undo());
        Assert.True(document.CanRedo);

        // A fresh edit invalidates the redo branch (no stale snapshot can re-apply).
        document.AddComponent(page.Id, "table");
        Assert.False(document.CanRedo);
        Assert.False(document.Redo());
    }

    [Fact]
    public void UndoRedo_OnEmptyStacks_ReturnFalse()
    {
        var document = AppDesignerDocument.Empty;
        Assert.False(document.CanUndo);
        Assert.False(document.Undo());
        Assert.False(document.CanRedo);
        Assert.False(document.Redo());
    }

    [Fact]
    public void AddPage_ThenUndo_RemovesIt_AndRedo_RestoresIt()
    {
        var document = AppDesignerDocument.Empty;
        var home = Page(document, document.AddPage("Home").Id);
        Assert.Single(document.Pages);
        Assert.True(document.Undo());
        Assert.Empty(document.Pages);
        Assert.True(document.Redo());
        Assert.Equal(home.Id, Assert.Single(document.Pages).Id);
    }

    [Fact]
    public void UndoStack_IsBounded_AfterManyEdits()
    {
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        // Exceed the configured max depth with component additions.
        for (var i = 0; i < 300; i++)
        {
            document.AddComponent(page.Id, "text");
        }

        // The stack is bounded: undoing more than the max depth yields to the base state.
        var undone = 0;
        while (document.Undo())
        {
            undone++;
            Assert.True(undone <= 250, $"undo should be bounded well below 300, reached {undone}");
        }

        // Bound reached: the oldest entries were dropped, the rest restored.
        Assert.True(undone >= 200 && undone < 300, $"expected a bounded undo count, got {undone}");
        Assert.True(document.CanRedo);
    }

    [Fact]
    public void Undo_OfRejectedEdit_IsNotRecorded()
    {
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        document.AddComponent(page.Id, "text"); // a valid undoable edit

        // A rejected edit (unknown component type) must NOT create an extra undo step.
        Assert.Throws<InvalidOperationException>(() => document.AddComponent(page.Id, "evil-widget"));

        Assert.True(document.Undo()); // removes the valid text add
        Assert.Empty(Page(document, page.Id).Components);
        Assert.True(document.Undo()); // removes the page
        Assert.Empty(document.Pages);
        Assert.False(document.Undo()); // nothing left -> the rejected edit added no step
    }

    [Fact]
    public void SaveSerializesCurrentState_AfterUndoAndRedo_NoStaleSnapshot()
    {
        // Workspace-convention hardening (delayed-response safety): save serializes the LIVE
        // document state at call time, so an in-flight/delayed save can never write a stale
        // undo/redo snapshot. Verify the serialized content tracks undo/redo exactly.
        var document = AppDesignerDocument.Empty;
        var page = Page(document, document.AddPage("home").Id);
        document.AddComponent(page.Id, "text");

        // Undo removes the component -> a save here writes a draft WITHOUT it.
        Assert.True(document.Undo());
        Assert.DoesNotContain("\"type\":\"text\"", document.ToJson(), StringComparison.Ordinal);

        // Redo restores it -> a save now writes the component again.
        Assert.True(document.Redo());
        Assert.Contains("\"type\":\"text\"", document.ToJson(), StringComparison.Ordinal);

        // A fresh edit after undo drops the redo branch, so the redo path can never
        // clobber newer edits with stale state (no stale-snapshot re-save).
        Assert.True(document.Undo());
        document.AddComponent(page.Id, "button");
        Assert.False(document.CanRedo);
        Assert.DoesNotContain("\"type\":\"text\"", document.ToJson(), StringComparison.Ordinal);
        Assert.Contains("\"type\":\"button\"", document.ToJson(), StringComparison.Ordinal);
    }
}
