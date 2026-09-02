using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class Phase0CapabilitiesTests
{
    [Fact]
    public void RoundtripParser_Capabilities_Baseline()
    {
        var caps = BpmnParser.Capabilities;
        Assert.True(caps.SupportsStrictRoundtrip);
        Assert.True(caps.SupportsRuntimeProjection);    // Phase 4 - implemented
        Assert.False(caps.SupportsCollaboration);       // Phase 1 - not yet implemented  
        Assert.True(caps.SupportsVendorNormalization);  // Phase 2 - implemented
        Assert.True(caps.SupportsAdvancedValidation);   // Phase 3 - implemented
    }

    [Fact]
    public async Task RoundtripParser_BasicRegression_ProcessIdPreserved()
    {
        const string xml = "<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'><bpmn:process id='p1'/></bpmn:definitions>";
        var model = await new BpmnParser(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict })
            .ParseAsync(xml, TestContext.Current.CancellationToken);
        Assert.Equal("p1", model.ProcessId);
        // Ensure capability introduction caused no diagnostics regression beyond existing expectations.
        // (If future changes add diagnostics, adapt assertion accordingly.)
        Assert.NotEmpty(model.Diagnostics);
    }
}