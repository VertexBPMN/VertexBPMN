using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Unified;

public class UnifiedPhaseDExtensionTests
{
    private readonly BpmnParser _parser = new(new BpmnParserOptions { EnableNormalizedProjectionSerializer = true, NormalizeVendorExtensions = true} );
    //private readonly BpmnSerializer _serializer = new();
    private readonly NormalizedProjectionSerializer  _serializer = new(new BpmnParserOptions());

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
        var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var task = Assert.Single(model.Tasks);
        Assert.NotNull(task.Attributes);
        Assert.Contains(task.Attributes!, kv => kv.Value == "field1");
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
        var model = await _parser.ParseAsync(xml, TestContext.Current.CancellationToken);
        var task = Assert.Single(model.Tasks);
        Assert.NotNull(task.Attributes);
        Assert.Contains(task.Attributes!, kv => kv.Value == "orderVar");
        Assert.Contains(task.Attributes!, kv => kv.Value == "resultVar");
        var serialized = _serializer.Serialize(model);
        Assert.Contains("zeebe:ioMapping", serialized);
        Assert.Contains("zeebe:input", serialized);
        Assert.Contains("zeebe:output", serialized);
    }
}
