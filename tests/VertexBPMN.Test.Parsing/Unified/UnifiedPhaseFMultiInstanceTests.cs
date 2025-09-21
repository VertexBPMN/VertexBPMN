using System.Threading.Tasks;
using System.Linq;
using VertexBPMN.Domain.Model.Bpmn.Model;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Unified;

public class UnifiedPhaseFMultiInstanceTests
{
    private readonly UnifiedBpmnParser _parser = new();

    [Fact]
    public async Task Captures_Separate_Input_And_Output_Elements()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:zeebe='http://zeebe.io/schema/zeebe/1.0'>
  <process id='p1'>
    <subProcess id='sp1'>
      <multiInstanceLoopCharacteristics isSequential='false'>
        <zeebe:inputCollection>orders</zeebe:inputCollection>
        <zeebe:inputElement>order</zeebe:inputElement>
        <zeebe:outputElement>result</zeebe:outputElement>
        <completionCondition>nrOfCompletedInstances > 0</completionCondition>
      </multiInstanceLoopCharacteristics>
    </subProcess>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var mi = Assert.IsType<MultiInstanceLoopCharacteristics>(model.Subprocesses.Single().Loop);
        Assert.Equal("orders", mi.Collection);
        Assert.Equal("order", mi.InputElement);
        Assert.Equal("result", mi.OutputElement);
        Assert.Equal("order", mi.ElementVariable); // precedence -> inputElement used when no camunda:elementVariable
    }

    [Fact]
    public async Task Camunda_ElementVariable_Takes_Precedence_Over_Zeebe_InputElement()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn' xmlns:zeebe='http://zeebe.io/schema/zeebe/1.0'>
  <process id='p1'>
    <subProcess id='sp1'>
      <multiInstanceLoopCharacteristics camunda:collection='items' camunda:elementVariable='it'>
        <zeebe:inputElement>ignoredInput</zeebe:inputElement>
        <zeebe:outputElement>outVar</zeebe:outputElement>
      </multiInstanceLoopCharacteristics>
    </subProcess>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var mi = Assert.IsType<MultiInstanceLoopCharacteristics>(model.Subprocesses.Single().Loop);
        Assert.Equal("items", mi.Collection);
        Assert.Equal("it", mi.ElementVariable);
        Assert.Equal("ignoredInput", mi.InputElement); // still captured
        Assert.Equal("outVar", mi.OutputElement);
        Assert.Null(mi.LoopCardinality); // collection overrides cardinality
    }
}
