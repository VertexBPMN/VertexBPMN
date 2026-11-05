using System.Linq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Studio.Services;

namespace VertexBPMN.Tests.Unit.Applications;

/// <summary>
/// LC07 S2: data sources, data binding and schema-validated field mapping in the low-code
/// app designer. The declared data source (key + bindingVersion) matches the Lc06ReferenceApp
/// dataSources shape; a component may only bind to a DECLARED source (undeclared -> rejected /
/// flagged UNKNOWN_DATA_SOURCE, mirroring ApplicationDomainDiagnostics), and field mappings may
/// only reference fields that exist on the bound source's authored schema.
/// </summary>
public sealed class AppDesignerDataBindingTests
{
    [Fact]
    public void DeclareDataSource_AndBindTable_ProducesValidLc06ShapedContent()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount", "customer"]);

        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");
        document.BindComponent(page.Id, table.Id, "invoiceList");

        var json = document.ToJson();

        // dataSources declaration matches the LC06 shape (key + bindingVersion).
        Assert.Contains("\"dataSources\":[{\"key\":\"invoiceList\",\"bindingVersion\":2,\"fields\"", json, StringComparison.Ordinal);
        Assert.Contains("\"dataSource\":\"invoiceList\"", json, StringComparison.Ordinal);

        // Declared + referenced => no validation diagnostics, still allowlisted/released-valid.
        ApplicationComponentCatalog.Validate(json); // must not throw
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));
        Assert.Empty(document.Validate());
    }

    [Fact]
    public void BindComponent_ToUndeclaredDataSource_IsRejected()
    {
        var document = AppDesignerDocument.Empty;
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");

        var exception = Assert.Throws<InvalidOperationException>(() => document.BindComponent(page.Id, table.Id, "missing"));
        Assert.Contains("not declared", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RemoveDataSource_FlagsReferencedComponent_AsUndeclared()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number"]);
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");
        document.BindComponent(page.Id, table.Id, "invoiceList");

        document.RemoveDataSource("invoiceList");

        Assert.Contains("UNKNOWN_DATA_SOURCE", document.Validate(), StringComparer.Ordinal);
        var json = document.ToJson();
        Assert.Contains("UNKNOWN_DATA_SOURCE", ApplicationDomainDiagnostics.Validate(json), StringComparison.Ordinal);
    }

    [Fact]
    public void AddDataSource_DuplicateKey_IsRejected()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2);
        var exception = Assert.Throws<InvalidOperationException>(() => document.AddDataSource("invoiceList", 3));
        Assert.Contains("already declared", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapField_ToKnownSchemaField_Roundtrips()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount", "customer"]);
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");
        document.BindComponent(page.Id, table.Id, "invoiceList");

        document.MapField(page.Id, table.Id, "number", "Number", "Copy", false);

        var json = document.ToJson();
        Assert.Contains("\"sourcePath\":\"number\",\"targetKey\":\"Number\"", json, StringComparison.Ordinal);
        Assert.Empty(document.Validate());
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));

        // Round-trip: parsing the saved content restores the declared source, binding and mapping.
        var reloaded = AppDesignerDocument.Parse(json);
        Assert.Single(reloaded.DataSources);
        Assert.Equal("invoiceList", reloaded.DataSources[0].Key);
        var reloadedTable = reloaded.Pages[0].Components.Single(component => component.Type == "table");
        Assert.Equal("invoiceList", reloadedTable.DataSource);
        var mapping = Assert.Single(reloadedTable.Mappings);
        Assert.Equal("number", mapping.SourcePath);
        Assert.Equal("Number", mapping.TargetKey);
        Assert.Empty(reloaded.Validate());
    }

    [Fact]
    public void MapField_UnknownSchemaField_IsRejected()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number", "amount", "customer"]);
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");
        document.BindComponent(page.Id, table.Id, "invoiceList");

        var exception = Assert.Throws<InvalidOperationException>(() => document.MapField(page.Id, table.Id, "doesNotExist", "X"));
        Assert.Contains("Unknown field 'doesNotExist'", exception.Message, StringComparison.Ordinal);
        Assert.Empty(table.Mappings); // nothing silently accepted
    }

    [Fact]
    public void MapField_OnUnboundComponent_IsRejected()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number"]);
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");

        var exception = Assert.Throws<InvalidOperationException>(() => document.MapField(page.Id, table.Id, "number", "Number"));
        Assert.Contains("Bind the component", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rebind_ToDifferentSource_ClearsMappingsForOldSchema()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("invoiceList", 2, ["number"]);
        document.AddDataSource("claims", 2, ["claimId"]);
        var page = document.AddPage("home");
        var table = document.AddComponent(page.Id, "table");
        document.BindComponent(page.Id, table.Id, "invoiceList");
        document.MapField(page.Id, table.Id, "number", "Number");

        document.BindComponent(page.Id, table.Id, "claims");
        Assert.Equal("claims", table.DataSource);
        Assert.Empty(table.Mappings); // stale invoiceList field mapping removed on rebind
    }

    [Fact]
    public void DeclareDataSource_WithFields_ThenRemove_And_NoReferences_IsClean()
    {
        var document = AppDesignerDocument.Empty;
        document.AddDataSource("orphan", 2, ["a", "b"]);
        var json = document.ToJson();
        Assert.Contains("\"key\":\"orphan\",\"bindingVersion\":2,\"fields\"", json, StringComparison.Ordinal);
        ApplicationComponentCatalog.Validate(json); // must not throw
        Assert.Equal("[]", ApplicationDomainDiagnostics.Validate(json));

        document.RemoveDataSource("orphan");
        Assert.Empty(document.DataSources);
        Assert.Empty(document.Validate());
    }
}
