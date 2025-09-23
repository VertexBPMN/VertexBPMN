using System.Linq;
using System.Threading.Tasks;
using VertexBPMN.Parsing;
using Xunit;

namespace VertexBPMN.Test.Parsing.Unified;

public class UnifiedValidationAndDataTests
{
    private readonly BpmnParser _parser = new();

    [Fact]
    public async Task Reports_No_Start_Event()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <endEvent id='e1'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        Assert.Contains(model.Diagnostics, d => d.Contains("No startEvent"));
    }

    [Fact]
    public async Task Detects_Default_Flow_With_Condition()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
 <process id='p1'>
   <exclusiveGateway id='g1' default='f2'/>
   <sequenceFlow id='f1' sourceRef='g1' targetRef='t1'>
     <conditionExpression>${x > 5}</conditionExpression>
   </sequenceFlow>
   <sequenceFlow id='f2' sourceRef='g1' targetRef='t2'>
     <conditionExpression>${y > 1}</conditionExpression>
   </sequenceFlow>
   <userTask id='t1'/>
   <userTask id='t2'/>
 </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        Assert.Contains(model.Diagnostics, d => d.Contains("Default flow") && d.Contains("f2"));
    }

    [Fact]
    public async Task Reports_Invalid_Boundary_Attachment()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
 <process id='p1'>
   <startEvent id='s1'/>
   <boundaryEvent id='b1' attachedToRef='missing'>
     <timerEventDefinition />
   </boundaryEvent>
 </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        Assert.Contains(model.Diagnostics, d => d.Contains("attachedToRef") && d.Contains("missing"));
    }

    [Fact]
    public async Task Reports_Unmatched_Link()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
 <process id='p1'>
   <intermediateThrowEvent id='throwLink'>
     <linkEventDefinition name='L_A'/>
   </intermediateThrowEvent>
 </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        Assert.Contains(model.Diagnostics, d => d.Contains("Unmatched link") && d.Contains("L_A"));
    }

    [Fact]
    public async Task Parses_DataObjects_Stores_And_Properties()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <dataObject id='do_invoice' name='Invoice'/>
    <dataObjectReference id='dor_invoice' dataObjectRef='do_invoice'/>
    <dataStore id='ds_erp' name='ERPStore'/>
    <dataStoreReference id='dsr_erp' dataStoreRef='ds_erp'/>
    <property id='prop_customerId' name='customerId'/>
    <property id='prop_status'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        Assert.Single(model.DataObjects);
        Assert.Single(model.DataObjectReferences);
        Assert.Single(model.DataStores);
        Assert.Single(model.DataStoreReferences);
        Assert.Equal(2, model.Properties.Count);
    }

    [Fact]
    public async Task Parses_Task_IO_Spec_With_Associations()
    {
        var xml = """
<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
  <process id='p1'>
    <userTask id='task1'>
      <ioSpecification>
        <dataInput id='task1_in' name='inputVar'/>
        <dataOutput id='task1_out' name='resultVar'/>
      </ioSpecification>
      <dataInputAssociation>
        <sourceRef>prop_customerId</sourceRef>
        <targetRef>task1_in</targetRef>
      </dataInputAssociation>
      <dataOutputAssociation>
        <sourceRef>task1_out</sourceRef>
        <targetRef>prop_status</targetRef>
      </dataOutputAssociation>
    </userTask>
    <property id='prop_customerId'/>
    <property id='prop_status'/>
  </process>
</definitions>
""";
        var model = await _parser.ParseAsync(xml);
        var io = Assert.Single(model.ActivityIo);
        Assert.Single(io.DataInputs);
        Assert.Single(io.DataOutputs);
        Assert.Single(io.InputAssociations);
        Assert.Single(io.OutputAssociations);
    }
}
