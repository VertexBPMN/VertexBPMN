using System.Linq;
using System.Threading.Tasks;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Unified;

public class UnifiedPhaseDExtensionTests
{
    private readonly UnifiedBpmnParser _parser = new();
    private readonly UnifiedBpmnSerializer _serializer = new();

    [Fact]
    public async Task Parses_Camunda_FormFields_Extensions()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'
             xmlns:camunda='http://camunda.org/schema/1.0/bpmn'>
  <process id='p1'>
    <userTask id='task_form'>
      <extensionElements>
        <camunda:formData>
          <camunda:formField id='field1' label='Name' type='string'/>
        </camunda:formData>
      </extensionElements>
    </userTask>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var task = Assert.Single(model.Tasks);
        Assert.NotNull(task.ExtensionAttributes);
        Assert.Contains(task.ExtensionAttributes!, kv => kv.Value == "field1");
        var serialized = _serializer.Serialize(model);
        Assert.Contains("camunda:formData", serialized);
        Assert.Contains("camunda:formField", serialized);
    }

    [Fact]
    public async Task Parses_Zeebe_IoMapping_Extensions()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'
             xmlns:zeebe='http://zeebe.io/schema/zeebe/1.0'>
  <process id='p1'>
    <serviceTask id='service_io'>
      <extensionElements>
        <zeebe:ioMapping>
          <zeebe:input source='=order' target='orderVar'/>
          <zeebe:output source='=result' target='resultVar'/>
        </zeebe:ioMapping>
      </extensionElements>
    </serviceTask>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var task = Assert.Single(model.Tasks);
        Assert.NotNull(task.ExtensionAttributes);
        Assert.Contains(task.ExtensionAttributes!, kv => kv.Value == "orderVar");
        Assert.Contains(task.ExtensionAttributes!, kv => kv.Value == "resultVar");
        var serialized = _serializer.Serialize(model);
        Assert.Contains("zeebe:ioMapping", serialized);
        Assert.Contains("zeebe:input", serialized);
        Assert.Contains("zeebe:output", serialized);
    }
}
