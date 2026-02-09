using VertexBPMN.Domain.Model.Validation;
using Xunit;

namespace VertexBPMN.Test.Parsing.Validation;

public class ValidationRuleRegistryTests
{
    [Fact]
    public void AllCurrentRuleCodes_AreRegistered()
    {
        // Codes seen in RunAdvancedValidation (update here if new rules added)
        string[] expected =
        {
            "STR-DUP-ID","STR-MISSING-PROCESS","STR-MISSING-ID",
            "REF-SEQUENCE-ENDPOINT","REF-BOUNDARY-ATTACHED-MISSING",
            "REF-GLOBAL-MESSAGE-MISSING","REF-GLOBAL-SIGNAL-MISSING",
            "REF-GLOBAL-ERROR-MISSING","REF-GLOBAL-ESCALATION-MISSING",
            "REF-LANE-FLOWNODE-MISSING","REF-DATAOBJECTREF-TARGET-MISSING",
            "REF-ASSOCIATION-ENDPOINT-MISSING",
            "SEM-DEFAULT-WITH-CONDITION","SEM-MI-CONFLICT",
            "SEM-LINK-UNMATCHED","SEM-LINK-MULTIPLE-THROW",
            "SEM-CANCEL-OUTSIDE-TX","SEM-TERMINATE-OUTSIDE-TX",
            "SEM-BOUNDARY-COMPENSATION-CANCELACTIVITY",
            "SEM-EVENTGW-INVALID-OUTGOING","SEM-EVENTSUBPROCESS-START-TYPE",
            "ADV-UNREACHABLE-NODE","ADV-ORPHANED-END","ADV-DEAD-SEQUENCE-FLOW"
        };

        var missing = expected.Where(c => !ValidationRules.ByCode.ContainsKey(c)).ToList();
        Assert.True(missing.Count == 0, "Missing descriptors for: " + string.Join(", ", missing));
    }

    [Fact]
    public void DescriptorMetadata_IsConsistent()
    {
        foreach (var d in ValidationRules.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(d.Code));
            Assert.False(string.IsNullOrWhiteSpace(d.Category));
            Assert.False(string.IsNullOrWhiteSpace(d.Title));
            Assert.False(string.IsNullOrWhiteSpace(d.Description));
        }
    }
}