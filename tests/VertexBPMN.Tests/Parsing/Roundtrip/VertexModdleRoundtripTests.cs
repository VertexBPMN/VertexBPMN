using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Engine.Serialization;

namespace VertexBPMN.Tests.Parsing.Roundtrip;

public class VertexModdleRoundtripTests
{
    private const string VertexNs = "https://vertexbpmn.io/schema/bpmn/1.0";

    private static BpmnParser StrictParser(bool advancedValidation = false, bool normalizeVendors = false) =>
        new(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Strict,
            PreserveUnknownExtensions = true,
            EnableAdvancedValidation = advancedValidation,
            NormalizeVendorExtensions = normalizeVendors
        });
    private static BpmnParser NormalizedParser(bool advancedValidation = false, bool normalizeVendors = true) =>
        new(new BpmnParserOptions
        {
            RoundtripMode = BpmnRoundtripMode.Normalized,
            PreserveUnknownExtensions = true,
            EnableAdvancedValidation = advancedValidation,
            NormalizeVendorExtensions = normalizeVendors
        });
    private static string SerializeStrict(BpmnModel model) =>
        new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

    [Fact]
    public void Connector_Retry_IoMapping_StrictRoundtrip_UsesCanonicalNamespace()
    {
        const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1">
   <bpmn:startEvent id="start" />
     <bpmn:serviceTask id="Task_CallApi" name="Call API">
      <bpmn:extensionElements>
        <vertex:connector type="http" operationId="http.request" credentialRef="cred-orders-api" timeoutMs="30000" />
        <vertex:retryPolicy maxAttempts="5" strategy="exponential" baseDelayMs="1000" retryOn="429,5xx" />
        <vertex:ioMapping>
          <vertex:input name="url" expression="${orderApiUrl}" />
          <vertex:output name="response" target="httpResponse" />
        </vertex:ioMapping>
      </bpmn:extensionElements>
     </bpmn:serviceTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f0" sourceRef="start" targetRef="Task_CallApi"/>
    <bpmn:sequenceFlow id="f1" sourceRef="Task_CallApi" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var parser = StrictParser(normalizeVendors: true);
        var model = parser.ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = parser.Serialize(model);

        Assert.Contains(VertexNs, outXml);
        Assert.Contains("vertex:connector", outXml);
        Assert.Contains("type=\"http\"", outXml);
        Assert.Contains("operationId=\"http.request\"", outXml);
        Assert.Contains("credentialRef=\"cred-orders-api\"", outXml);
        Assert.Contains("timeoutMs=\"30000\"", outXml);
        Assert.Contains("vertex:retryPolicy", outXml);
        Assert.Contains("maxAttempts=\"5\"", outXml);
        Assert.Contains("strategy=\"exponential\"", outXml);
        Assert.Contains("baseDelayMs=\"1000\"", outXml);
        Assert.Contains("retryOn=\"429,5xx\"", outXml);
        Assert.Contains("vertex:ioMapping", outXml);
        Assert.Contains("vertex:input", outXml);
        Assert.Contains("name=\"url\"", outXml);
        Assert.Contains("expression=\"${orderApiUrl}\"", outXml);
        Assert.Contains("vertex:output", outXml);
        Assert.Contains("name=\"response\"", outXml);
        Assert.Contains("target=\"httpResponse\"", outXml);

        var task = model.Tasks!.Single(value => value.Id == "Task_CallApi").Attributes!;
        Assert.Equal("http", task["vertex:connector.type"]);
        Assert.Equal("http.request", task["vertex:connector.operationId"]);
        Assert.Equal("vertex:connector", model.Tasks!.Single(value => value.Id == "Task_CallApi").Implementation);

    }

    [Fact]
    public void Webhook_OnStartEvent_StrictRoundtrip()
    {
        const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start">
      <bpmn:extensionElements>
        <vertex:webhook path="/hooks/orders" method="POST" secretRef="cred-webhook" />
        <vertex:trigger type="webhook" name="orders" processDefinitionKey="order-process" />
      </bpmn:extensionElements>
    </bpmn:startEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = SerializeStrict(model);
        Assert.Contains("vertex:webhook", outXml);
        Assert.Contains("path=\"/hooks/orders\"", outXml);
        Assert.Contains("method=\"POST\"", outXml);
        Assert.Contains("secretRef=\"cred-webhook\"", outXml);
        Assert.Contains("vertex:trigger", outXml);
        Assert.Contains("processDefinitionKey=\"order-process\"", outXml);
        Assert.Contains(VertexNs, outXml);
    }

    [Fact]
    public void Credential_Extension_StrictRoundtrip()
    {
        const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1">
    <bpmn:serviceTask id="t1">
      <bpmn:extensionElements>
        <vertex:credential id="cred-orders-api" kind="http-basic" />
      </bpmn:extensionElements>
    </bpmn:serviceTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="t1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = SerializeStrict(model);
        Assert.Contains("vertex:credential", outXml);
        Assert.Contains("id=\"cred-orders-api\"", outXml);
        Assert.Contains("kind=\"http-basic\"", outXml);
        Assert.Contains(VertexNs, outXml);
    }

    [Fact]
    public void UnknownExtension_NotDropped_BesideVertexConnector()
    {
        const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"
                  xmlns:other="http://example.com/other">
  <bpmn:process id="p1">
    <bpmn:serviceTask id="t1">
      <bpmn:extensionElements>
        <vertex:connector type="http" operationId="http.request" />
        <other:foo bar="1"/>
      </bpmn:extensionElements>
    </bpmn:serviceTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="t1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var model = StrictParser().ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = SerializeStrict(model);
        Assert.Contains("vertex:connector", outXml);
        Assert.Contains("other:foo", outXml);
        Assert.Contains("bar=\"1\"", outXml);
        Assert.Contains("http://example.com/other", outXml);
    }

    [Fact]
    public void Connector_MissingTypeAndOperation_ProducesVertexDiagnostics()
    {
        const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1">
    <bpmn:serviceTask id="t1">
      <bpmn:extensionElements>
        <vertex:connector timeoutMs="1000" />
      </bpmn:extensionElements>
    </bpmn:serviceTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="t1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var model = StrictParser(advancedValidation: true).ParseAsync(xml).GetAwaiter().GetResult();
        Assert.NotNull(model.ValidationDiagnostics);
        var codes = model.ValidationDiagnostics!.Select(d => d.Code).ToList();
        Assert.Contains("VEN-VERTEX-CONNECTOR-TYPE", codes);
        Assert.Contains("VEN-VERTEX-CONNECTOR-OPERATION", codes);
        Assert.All(
            model.ValidationDiagnostics!.Where(d => d.Code.StartsWith("VEN-VERTEX-", StringComparison.Ordinal)),
            d =>
            {
                Assert.Equal(ValidationSeverity.Error, d.Severity);
                Assert.Equal("Vertex", d.Category);
                Assert.Equal("t1", d.ElementId);
            });
    }

    [Fact]
    public void CamundaAssignee_And_VertexConnector_BothSurvive()
    {
        const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:camunda="http://camunda.org/schema/1.0/bpmn"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1">
    <bpmn:userTask id="ut1" name="Work">
      <bpmn:extensionElements>
        <camunda:assignee value="alice"/>
        <vertex:connector type="http" operationId="http.request" />
      </bpmn:extensionElements>
    </bpmn:userTask>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="ut1" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
        var model = StrictParser(normalizeVendors: true).ParseAsync(xml).GetAwaiter().GetResult();
        var outXml = SerializeStrict(model);
        Assert.Contains("camunda:assignee", outXml);
        Assert.Contains("value=\"alice\"", outXml);
        Assert.Contains("vertex:connector", outXml);
        Assert.Contains("type=\"http\"", outXml);
        Assert.Contains("operationId=\"http.request\"", outXml);

        var ut = model.Tasks!.Single(value => value.Id == "ut1").Attributes!;
        Assert.Equal("http", ut["vertex:connector.type"]);
    }
}
