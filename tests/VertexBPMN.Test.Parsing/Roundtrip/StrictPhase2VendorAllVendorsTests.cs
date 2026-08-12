using VertexBPMN.Engine.Parsing;
using VertexBPMN.Domain.Model.Bpmn;
using Xunit;

namespace VertexBPMN.Test.Parsing.Roundtrip;

public class StrictPhase2VendorAllVendorsTests
{
    private const string Xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  xmlns:zeebe="http://zeebe.io/schema/zeebe/1.0"
                  xmlns:flowable="http://flowable.org/bpmn"
                  xmlns:activiti="http://activiti.org/bpmn"
                  xmlns:cib="http://cib.de/schema/bpmn"
                  xmlns:jbpm="http://jbpm.org/bpmn"
                  xmlns:osmanthus="http://osmanthus.io/bpmn"
                  xmlns:alfresco="http://alfresco.org/bpmn"
                  xmlns:mcp="http://vertexbpmn.io/mcp"
                  xmlns:my="http://my.vendor/custom">
  <bpmn:process id="pAll">
    <bpmn:userTask id="utAll">
      <bpmn:extensionElements>
        <!-- Camunda -->
        <camunda:assignee value="alice"/>
        <camunda:formField id="cf1" name="CField" type="string"/>
        <camunda:properties>
          <camunda:property name="cProp" value="cVal"/>
        </camunda:properties>
        <camunda:taskListener event="create" class="X.Listener"/>

        <!-- Zeebe -->
        <zeebe:taskDefinition type="workType"/>
        <zeebe:ioMapping>
          <zeebe:input source="=inExpr" target="inVar"/>
          <zeebe:output source="=outExpr" target="outVar"/>
        </zeebe:ioMapping>
        <zeebe:taskHeaders>
          <zeebe:header key="h1" value="v1"/>
        </zeebe:taskHeaders>

        <!-- Flowable -->
        <flowable:assignee value="bob"/>
        <flowable:formField id="ff1" name="FField" type="long"/>
        <flowable:taskListener event="assignment" class="F.Class"/>

        <!-- Activiti -->
        <activiti:formProperty id="ap1" name="AField" type="boolean" required="true"/>
        <activiti:taskListener event="complete" class="A.TaskListener"/>
        <activiti:executionListener event="start" class="A.ExecListener"/>
        <activiti:candidateUsers value="u1,u2"/>
        <activiti:candidateGroups value="g1"/>

        <!-- CIB -->
        <cib:assignee value="carol"/>
        <cib:formField id="cibF1" name="CibField" type="text"/>
        <cib:connector id="con1" type="http" url="http://api/endpoint"/>
        <cib:aiModule type="vision" model="resnet50"/>

        <!-- jBPM -->
        <jbpm:assignment actorId="actorX" groupId="groupY"/>
        <jbpm:workItemHandler name="DoX" class="Do.X.Class"/>

        <!-- Osmanthus -->
        <osmanthus:advance type="jump" target="task2"/>
        <osmanthus:timeout duration="PT10S" action="retry"/>
        <osmanthus:pdfTemplate templateId="tpl1" output="pdfOut"/>

        <!-- Alfresco -->
        <alfresco:formKey value="frmKey"/>
        <alfresco:scriptTask script="print('alfresco');"/>

        <!-- MCP -->
        <mcp:mcpServiceTask mcpServerUrl="http://mcp" mcpMethod="Do" mcpParams="{&quot;a&quot;:1}" />

        <!-- Generic -->
        <my:flag enabled="true" mode="fast"/>
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id="e1"/>
    <bpmn:sequenceFlow id="f1" sourceRef="utAll" targetRef="e1"/>
  </bpmn:process>
</bpmn:definitions>
""";

    [Fact]
    public void AllVendors_Disabled_Default_No_Map()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict
        }).ParseAsync(Xml).GetAwaiter().GetResult();

        Assert.NotNull(model.RawMetadata);
        Assert.Null(model.RawMetadata!.VendorNormalizedExtensions);
    }

    [Fact]
    public void AllVendors_Enabled_NoGenerics_GenericKeysMissing()
    {
        var model = new BpmnParser(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            NormalizeVendorExtensions = true
        }).ParseAsync(Xml).GetAwaiter().GetResult();

        var map = model.RawMetadata!.VendorNormalizedExtensions!;
        Assert.True(map.ContainsKey("utAll"));
        var b = map["utAll"];

        // Representative subset
        Assert.Equal("alice", b["camunda:assignee"]);
        Assert.Equal("string", b["camunda:formField.cf1.type"]);
        Assert.Equal("cVal", b["camunda:property.cProp"]);
        Assert.Equal("workType", b["zeebe:taskDefinition.type"]);
        Assert.Equal("=inExpr", b["zeebe:ioMapping.input.inVar"]);
        Assert.Equal("=outExpr", b["zeebe:ioMapping.output.outVar"]);
        Assert.Equal("v1", b["zeebe:taskHeaders.h1"]);
        Assert.Equal("bob", b["flowable:assignee"]);
        Assert.Equal("boolean", b["activiti:formProperty.ap1.type"]);
        Assert.Equal("carol", b["cib:assignee"]);
        Assert.Equal("http", b["cib:connector.con1.type"]);
        Assert.Equal("Do.X.Class", b["jbpm:workItemHandler.DoX.class"]);
        Assert.Equal("jump", b["osmanthus:advance.type"]);
        Assert.Equal("frmKey", b["alfresco:formKey"]);
        Assert.Equal("http://mcp", b["mcp:mcpServiceTask.mcpServerUrl"]);
        Assert.False(b.Keys.Contains("my:flag.enabled")); // generics OFF
    }

    //[Fact]
    //public void AllVendors_Enabled_WithGenerics_GenericKeysPresent()
    //{
    //    var model = new BpmnParser(new BpmnParserOptions
    //    {
    //        RoundtripMode = BpmnRoundtripMode.Strict,
    //        NormalizeVendorExtensions = true,
    //        NormalizeUnknownVendorExtensions = true
    //    }).ParseAsync(Xml).GetAwaiter().GetResult();

    //    var map = model.RawMetadata!.VendorNormalizedExtensions!;
    //    var b = map["utAll"];

    //    Assert.Equal("true", b["my:flag.enabled"]);
    //    Assert.Equal("fast", b["my:flag.mode"]);
    //}
}