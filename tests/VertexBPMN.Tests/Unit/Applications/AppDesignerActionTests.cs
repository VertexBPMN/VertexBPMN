using System.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Applications;

/// <summary>
/// LC07 S3: the action (Button-Ausführung) designer. A component can carry one explicit
/// action chosen from the CLOSED action catalog (data-source-read / start-process /
/// claim-task / complete-task, v1.0.0) with a reference-based target and explicit inputs.
/// The produced action JSON matches the Lc06ReferenceApp action shape and passes the
/// released semantic gate: data-source-read must reference a DECLARED data source
/// (UNKNOWN_DATA_SOURCE), start-process must reference a known process key (UNKNOWN_PROCESS).
/// unknown/unsanctioned action types are rejected, never emitted.
/// </summary>
public sealed class AppDesignerActionTests
{
    [Fact]
    public void ActionPalette_IsTheClosedS3Catalog()
    {
        Assert.Equal(
            ["claim-task", "complete-task", "data-source-read", "start-process"],
            AppDesignerDocument.GetActionTypes().ToArray(),
            StringComparer.Ordinal);
    }

    [Fact]
    public void AddAction_OnButton_ProducesSanctionedLc06ShapedAction()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2);
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.SetComponentProperty(page.Id, button.Id, "label", "Refresh");

        document.AddAction(page.Id, button.Id, "data-source-read");
        document.SetActionTarget(page.Id, button.Id, "invoiceList");
        document.AddActionInput(page.Id, button.Id, "maxRecords", "50");

        var json = document.ToJson();
        Assert.Contains("\"action\":{\"type\":\"data-source-read\",\"version\":\"1.0.0\",\"dataSource\":\"invoiceList\",\"inputs\":{\"maxRecords\":\"50\"}}", json, StringComparison.Ordinal);

        // Sanctioned shape: allowlisted and passes the released gate once the source is declared.
        ApplicationComponentCatalog.Validate(json); // must not throw (action type/version allowlisted)
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));
    }

    [Fact]
    public void StartProcess_BoundToKnownProcessKey_IsValidReleasedAction_WithReference()
    {
        var document = AppDesignerDocument.Empty;
        document.SetProcessCatalog(["lc06-reference-flow"]);

        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "start-process");
        document.SetActionTarget(page.Id, button.Id, "lc06-reference-flow");

        var json = document.ToJson();
        // Emits the stable KEY into processDefinitionId and declares a process reference by key.
        Assert.Contains("\"action\":{\"type\":\"start-process\",\"version\":\"1.0.0\",\"processDefinitionId\":\"lc06-reference-flow\"", json, StringComparison.Ordinal);
        Assert.Contains("\"references\":[{\"type\":\"process\",\"key\":\"lc06-reference-flow\"}]", json, StringComparison.Ordinal);
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));
        Assert.Empty(document.Validate());
    }

    [Fact]
    public void StartProcess_BoundToUnknownKey_IsRejected_AndFlaggedUnknownProcess()
    {
        var document = AppDesignerDocument.Empty;
        document.SetProcessCatalog(["lc06-reference-flow"]);
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "start-process");

        // Authoring through the closed reference catalogue rejects an unknown process key.
        var exception = Assert.Throws<InvalidOperationException>(() => document.SetActionTarget(page.Id, button.Id, "does-not-exist"));
        Assert.Contains("known process", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_CraftedStartProcess_ToUnknownKey_IsFlaggedUnknownProcess()
    {
        // Content that arrives from outside the designer (never authored through the reference
        // catalogue) with a start-process to an unknown key must be surfaced as UNKNOWN_PROCESS,
        // mirroring the released validator posture.
        const string crafted = "{\"pages\":[{\"id\":\"home\",\"route\":\"/home\",\"components\":[{\"type\":\"button\",\"version\":\"1.0.0\",\"action\":{\"type\":\"start-process\",\"version\":\"1.0.0\",\"processDefinitionId\":\"unknown-key\"}}]}]}";
        var document = AppDesignerDocument.Parse(crafted);
        Assert.Contains("UNKNOWN_PROCESS", document.Validate(), StringComparer.Ordinal);
        Assert.Contains("UNKNOWN_PROCESS", ApplicationDomainDiagnostics.Validate(crafted), StringComparison.Ordinal);
    }

    [Fact]
    public void DataSourceRead_BoundToDeclaredSource_IsValidReleasedAction()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount"]);
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "data-source-read");
        document.SetActionTarget(page.Id, button.Id, "invoiceList");

        var json = document.ToJson();
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));
        Assert.Empty(document.Validate());
    }

    [Fact]
    public void DataSourceRead_BoundToUndeclaredSource_IsRejected()
    {
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "data-source-read");

        var exception = Assert.Throws<InvalidOperationException>(() => document.SetActionTarget(page.Id, button.Id, "missingSource"));
        Assert.Contains("not declared", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveDataSource_LeavesDataSourceReadAction_FlaggedUndeclared()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number"]);
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "data-source-read");
        document.SetActionTarget(page.Id, button.Id, "invoiceList");
        document.RemoveDataSource("invoiceList");

        Assert.Contains("UNKNOWN_DATA_SOURCE", document.Validate(), StringComparer.Ordinal);
        var json = document.ToJson();
        Assert.Contains("UNKNOWN_DATA_SOURCE", ApplicationDomainDiagnostics.Validate(json), StringComparison.Ordinal);
    }

    [Fact]
    public void AddAction_UnknownUnsanctionedType_IsRejected_NeverEmitted()
    {
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");

        var exception = Assert.Throws<InvalidOperationException>(() => document.AddAction(page.Id, button.Id, "XSS-inject"));
        Assert.Contains("Unsupported action type", exception.Message, StringComparison.Ordinal);
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(document.ToJson()));
        Assert.DoesNotContain("action", document.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Claim_And_CompleteTask_Actions_TargetATask_WithExplicitInput()
    {
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");

        var claim = document.AddAction(page.Id, button.Id, "claim-task");
        document.SetActionTarget(page.Id, button.Id, "approval");
        Assert.Equal("claim-task", claim.Type);
        Assert.Equal("approval", claim.Target);

        // Switching the action type to complete-task keeps the task target but adds inputs.
        document.SetActionType(page.Id, button.Id, "complete-task");
        document.SetActionTarget(page.Id, button.Id, "approval");
        document.AddActionInput(page.Id, button.Id, "approved", "true");

        var json = document.ToJson();
        Assert.Contains("\"action\":{\"type\":\"complete-task\",\"version\":\"1.0.0\",\"taskKey\":\"approval\",\"inputs\":{\"approved\":\"true\"}}", json, StringComparison.Ordinal);
        ApplicationComponentCatalog.Validate(json); // must not throw
    }

    [Fact]
    public void Parse_Edit_Roundtrip_RestoresConfiguredAction()
    {
        const string seeded = "{\"pages\":[{\"id\":\"home\",\"route\":\"/home\",\"components\":[{\"type\":\"button\",\"version\":\"1.0.0\",\"label\":\"Go\",\"action\":{\"type\":\"complete-task\",\"version\":\"1.0.0\",\"taskKey\":\"approval\",\"inputs\":{\"approved\":\"true\"}}}]}]}";
        var document = AppDesignerDocument.Parse(seeded);
        var button = document.Pages[0].Components[0];
        Assert.NotNull(button.Action);
        Assert.Equal("complete-task", button.Action.Type);
        Assert.Equal("approval", button.Action.Target);
        Assert.Equal("true", button.Action.Inputs.Single().Value);

        document.RemoveAction(document.Pages[0].Id, button.Id);
        Assert.Null(button.Action);
        Assert.DoesNotContain("action", document.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Actions_OnlyOn_ButtonComponents_Roundtrip()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2);
        var page = document.AddPage("home");
        var button = document.AddComponent(page.Id, "button");
        document.AddAction(page.Id, button.Id, "data-source-read");
        document.SetActionTarget(page.Id, button.Id, "invoiceList");

        var json = document.ToJson();
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));

        var reloaded = AppDesignerDocument.Parse(json);
        var reloadedButton = reloaded.Pages[0].Components[0];
        Assert.NotNull(reloadedButton.Action);
        Assert.Equal("data-source-read", reloadedButton.Action.Type);
        Assert.Equal("invoiceList", reloadedButton.Action.Target);
    }
}
