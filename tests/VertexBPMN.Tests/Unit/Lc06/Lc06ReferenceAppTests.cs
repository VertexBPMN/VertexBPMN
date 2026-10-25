using System.Xml.Linq;
using VertexBPMN.Application.Lc06;

namespace VertexBPMN.Tests.Unit.Lc06;

public sealed class Lc06ReferenceAppTests
{
    [Fact]
    public void ReferenceProcessBpmn_IsValidParseableXml_WithCandidateUserTask()
    {
        // Must be well-formed XML (throws otherwise).
        var document = XDocument.Parse(Lc06ReferenceApp.ProcessBpmn);

        var userTask = Assert.Single(document.Descendants(), e => e.Name.LocalName == "userTask");
        Assert.Equal("approval", (string?)userTask.Attribute("id"));

        var candidateGroups = Assert.Single(userTask.Descendants(), e => e.Name.LocalName == "candidateGroups");
        Assert.Equal(Lc06ReferenceApp.CandidateRole, (string?)candidateGroups.Attribute("value"));
    }

    [Fact]
    public void ReferenceProcessReferencesSanctionedDataSourceInReleasedContent()
    {
        var content = Lc06ReferenceApp.ContentJson;

        // Declared data source that is referenced by a data-source-read action.
        Assert.Contains("\"key\": \"invoiceList\"", content, StringComparison.Ordinal);
        Assert.Contains("\"dataSource\": \"invoiceList\"", content, StringComparison.Ordinal);

        // Declared but NOT referenced (the narrow gate must reject it).
        Assert.Contains("\"key\": \"claimsOpen\"", content, StringComparison.Ordinal);

        // The start-process action references the stable process KEY the runtime starts by.
        Assert.Contains($"\"processDefinitionId\": \"{Lc06ReferenceApp.StartProcessKey}\"", content, StringComparison.Ordinal);
    }
}
