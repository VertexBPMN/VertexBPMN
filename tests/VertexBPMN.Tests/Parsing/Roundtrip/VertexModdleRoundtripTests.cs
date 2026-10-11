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
	private static string SerializeStrict(BpmnModel model) =>
		new BpmnSerializer { RoundtripMode = BpmnRoundtripMode.Strict }.Serialize(model);

	[Fact]
	public async Task Connector_Retry_IoMapping_StrictRoundtrip_UsesCanonicalNamespace()
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
		var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
		var outXml = parser.Serialize(model);

		Assert.Contains(VertexNs, outXml, StringComparison.Ordinal);
		Assert.Contains("vertex:connector", outXml, StringComparison.Ordinal);
		Assert.Contains("type=\"http\"", outXml, StringComparison.Ordinal);
		Assert.Contains("operationId=\"http.request\"", outXml, StringComparison.Ordinal);
		Assert.Contains("credentialRef=\"cred-orders-api\"", outXml, StringComparison.Ordinal);
		Assert.Contains("timeoutMs=\"30000\"", outXml, StringComparison.Ordinal);
		Assert.Contains("vertex:retryPolicy", outXml, StringComparison.Ordinal);
		Assert.Contains("maxAttempts=\"5\"", outXml, StringComparison.Ordinal);
		Assert.Contains("strategy=\"exponential\"", outXml, StringComparison.Ordinal);
		Assert.Contains("baseDelayMs=\"1000\"", outXml, StringComparison.Ordinal);
		Assert.Contains("retryOn=\"429,5xx\"", outXml, StringComparison.Ordinal);
		Assert.Contains("vertex:ioMapping", outXml, StringComparison.Ordinal);
		Assert.Contains("vertex:input", outXml, StringComparison.Ordinal);
		Assert.Contains("name=\"url\"", outXml, StringComparison.Ordinal);
		Assert.Contains("expression=\"${orderApiUrl}\"", outXml, StringComparison.Ordinal);
		Assert.Contains("vertex:output", outXml, StringComparison.Ordinal);
		Assert.Contains("name=\"response\"", outXml, StringComparison.Ordinal);
		Assert.Contains("target=\"httpResponse\"", outXml, StringComparison.Ordinal);

		var task = model.Tasks!.Single(value => value.Id == "Task_CallApi").Attributes!;
		Assert.Equal("http", task["vertex:connector.type"]);
		Assert.Equal("http.request", task["vertex:connector.operationId"]);
		Assert.Equal("vertex:connector", model.Tasks!.Single(value => value.Id == "Task_CallApi").Implementation);

	}

	[Fact]
	public async Task Webhook_OnStartEvent_StrictRoundtrip()
	{
		const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1">
    <bpmn:startEvent id="start">
      <bpmn:extensionElements>
        <vertex:webhook path="/hooks/orders" method="POST" secretRef="cred-webhook" credentialRef="cred-webhook-hmac" secretKey="signing" authMode="hmac-sha256" payloadSchema="{&quot;type&quot;:&quot;object&quot;}" correlationKey="orderId" />
        <vertex:trigger type="webhook" name="orders" processDefinitionKey="order-process" />
      </bpmn:extensionElements>
    </bpmn:startEvent>
    <bpmn:endEvent id="end"/>
    <bpmn:sequenceFlow id="f1" sourceRef="start" targetRef="end"/>
  </bpmn:process>
</bpmn:definitions>
""";
		var model = await StrictParser().ParseAsync(xml, TestContext.Current.CancellationToken);
		var outXml = SerializeStrict(model);
		Assert.Contains("vertex:webhook", outXml, StringComparison.Ordinal);
		Assert.Contains("path=\"/hooks/orders\"", outXml, StringComparison.Ordinal);
		Assert.Contains("method=\"POST\"", outXml, StringComparison.Ordinal);
		Assert.Contains("secretRef=\"cred-webhook\"", outXml, StringComparison.Ordinal);
		Assert.Contains("credentialRef=\"cred-webhook-hmac\"", outXml, StringComparison.Ordinal);
		Assert.Contains("secretKey=\"signing\"", outXml, StringComparison.Ordinal);
		Assert.Contains("authMode=\"hmac-sha256\"", outXml, StringComparison.Ordinal);
		Assert.Contains("correlationKey=\"orderId\"", outXml, StringComparison.Ordinal);
		Assert.Contains("vertex:trigger", outXml, StringComparison.Ordinal);
		Assert.Contains("processDefinitionKey=\"order-process\"", outXml, StringComparison.Ordinal);
		Assert.Contains(VertexNs, outXml, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Decision_AndIoMapping_OnBusinessRuleTask_StrictRoundtrip()
	{
		const string xml = """
<bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                  xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0">
  <bpmn:process id="p1">
    <bpmn:businessRuleTask id="decisionTask">
      <bpmn:extensionElements>
        <vertex:decision decisionRef="risk-assessment" binding="latest" version="3" />
        <vertex:ioMapping>
          <vertex:input name="amount" expression="${orderAmount}" />
          <vertex:output name="risk" target="riskLevel" />
        </vertex:ioMapping>
      </bpmn:extensionElements>
    </bpmn:businessRuleTask>
    <bpmn:endEvent id="end" />
    <bpmn:sequenceFlow id="f1" sourceRef="decisionTask" targetRef="end" />
  </bpmn:process>
</bpmn:definitions>
""";
		var model = await StrictParser(normalizeVendors: true).ParseAsync(xml, TestContext.Current.CancellationToken);
		var outXml = SerializeStrict(model);

		Assert.Contains("vertex:decision", outXml, StringComparison.Ordinal);
		Assert.Contains("decisionRef=\"risk-assessment\"", outXml, StringComparison.Ordinal);
		Assert.Contains("binding=\"latest\"", outXml, StringComparison.Ordinal);
		Assert.Contains("version=\"3\"", outXml, StringComparison.Ordinal);
		var attributes = model.Tasks!.Single(task => task.Id == "decisionTask").Attributes!;
		Assert.Equal("risk-assessment", attributes["vertex:decision.decisionRef"]);
		Assert.Equal("${orderAmount}", attributes["vertex:ioMapping.input.amount"]);
		Assert.Equal("riskLevel", attributes["vertex:ioMapping.output.risk"]);
	}

	[Fact]
	public async Task Credential_Extension_StrictRoundtrip()
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
		var model = await StrictParser().ParseAsync(xml, TestContext.Current.CancellationToken);
		var outXml = SerializeStrict(model);
		Assert.Contains("vertex:credential", outXml, StringComparison.Ordinal);
		Assert.Contains("id=\"cred-orders-api\"", outXml, StringComparison.Ordinal);
		Assert.Contains("kind=\"http-basic\"", outXml, StringComparison.Ordinal);
		Assert.Contains(VertexNs, outXml, StringComparison.Ordinal);
	}

	[Fact]
	public async Task UnknownExtension_NotDropped_BesideVertexConnector()
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
		var model = await StrictParser().ParseAsync(xml, TestContext.Current.CancellationToken);
		var outXml = SerializeStrict(model);
		Assert.Contains("vertex:connector", outXml, StringComparison.Ordinal);
		Assert.Contains("other:foo", outXml, StringComparison.Ordinal);
		Assert.Contains("bar=\"1\"", outXml, StringComparison.Ordinal);
		Assert.Contains("http://example.com/other", outXml, StringComparison.Ordinal);
	}

	[Fact]
	public async Task Connector_MissingTypeAndOperation_ProducesVertexDiagnostics()
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
		var model = await StrictParser(advancedValidation: true).ParseAsync(xml, TestContext.Current.CancellationToken);
		Assert.NotNull(model.ValidationDiagnostics);
		var codes = model.ValidationDiagnostics.Select(d => d.Code).ToList();
		Assert.Contains("VEN-VERTEX-CONNECTOR-TYPE", codes, StringComparer.Ordinal);
		Assert.Contains("VEN-VERTEX-CONNECTOR-OPERATION", codes, StringComparer.Ordinal);
		Assert.All(
			model.ValidationDiagnostics.Where(d => d.Code.StartsWith("VEN-VERTEX-", StringComparison.Ordinal)),
			d =>
			{
				Assert.Equal(ValidationSeverity.Error, d.Severity);
				Assert.Equal("Vertex", d.Category);
				Assert.Equal("t1", d.ElementId);
			});
	}

	[Fact]
	public async Task CamundaAssignee_And_VertexConnector_BothSurvive()
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
		var model = await StrictParser(normalizeVendors: true).ParseAsync(xml, TestContext.Current.CancellationToken);
		var outXml = SerializeStrict(model);
		Assert.Contains("camunda:assignee", outXml, StringComparison.Ordinal);
		Assert.Contains("value=\"alice\"", outXml, StringComparison.Ordinal);
		Assert.Contains("vertex:connector", outXml, StringComparison.Ordinal);
		Assert.Contains("type=\"http\"", outXml, StringComparison.Ordinal);
		Assert.Contains("operationId=\"http.request\"", outXml, StringComparison.Ordinal);

		var ut = model.Tasks!.Single(value => value.Id == "ut1").Attributes!;
		Assert.Equal("http", ut["vertex:connector.type"]);
	}
}
