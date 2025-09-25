using VertexBPMN.Parsing;
using VertexBPMN.Domain.Interfaces;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class Phase0CapabilitiesTests
{
    [Fact]
    public void RoundtripParser_Capabilities_Baseline()
    {
        var caps = BpmnParser.Capabilities;
        Assert.True(caps.SupportsStrictRoundtrip);
        Assert.False(caps.SupportsRuntimeProjection);
        Assert.False(caps.SupportsCollaboration);
        Assert.False(caps.SupportsVendorNormalization);
        Assert.False(caps.SupportsAdvancedValidation);
    }

    [Fact]
    public void RoundtripParser_BasicRegression_ProcessIdPreserved()
    {
        const string xml = "<bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'><bpmn:process id='p1'/></bpmn:definitions>";
        var model = new BpmnParser(new BpmnParserOptions { RoundtripMode = BpmnRoundtripMode.Strict })
            .ParseAsync(xml).GetAwaiter().GetResult();
        Assert.Equal("p1", model.ProcessId);
        // Ensure capability introduction caused no diagnostics regression beyond existing expectations.
        // (If future changes add diagnostics, adapt assertion accordingly.)
        Assert.NotEmpty(model.Diagnostics);
    }
}