using Microsoft.Extensions.Logging.Abstractions;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Studio.Services;
using VertexBPMN.Tests.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

[Collection("IntegratedApi")]
[Trait("Category", "FullProductSupportAcceptance")]
public sealed class StudioFullSupportAcceptanceTests
{
    private readonly HttpClient _client;

    public StudioFullSupportAcceptanceTests(
        CustomWebApplicationFactory factory,
        SharedSqliteDbFixture database,
        ITestOutputHelper output) =>
        _client = factory.WithSharedFixture(database).CreateClient(output);

    [Fact]
    public async Task FPS_STUDIO_01_Production_adapters_deploy_and_execute_a_modeler_roundtrip_artifact()
    {
        var processKey = $"fps-studio-{Guid.NewGuid():N}";
        var bpmn = $$"""
            <?xml version="1.0" encoding="UTF-8"?>
            <bpmn:definitions xmlns:bpmn="http://www.omg.org/spec/BPMN/20100524/MODEL"
                              xmlns:bpmndi="http://www.omg.org/spec/BPMN/20100524/DI"
                              xmlns:dc="http://www.omg.org/spec/DD/20100524/DC"
                              xmlns:di="http://www.omg.org/spec/DD/20100524/DI"
                              xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0"
                              id="Definitions_Studio" targetNamespace="https://vertexbpmn.io/full-support">
              <bpmn:process id="{{processKey}}" isExecutable="true">
                <bpmn:startEvent id="start" />
                <bpmn:sequenceFlow id="toApproval" sourceRef="start" targetRef="approval" />
                <bpmn:userTask id="approval" name="Approve">
                  <bpmn:extensionElements>
                    <vertex:form formRef="approval-form" formVersion="2" />
                  </bpmn:extensionElements>
                </bpmn:userTask>
                <bpmn:sequenceFlow id="toEnd" sourceRef="approval" targetRef="end" />
                <bpmn:endEvent id="end" />
              </bpmn:process>
              <bpmndi:BPMNDiagram id="Diagram_Studio">
                <bpmndi:BPMNPlane id="Plane_Studio" bpmnElement="{{processKey}}">
                  <bpmndi:BPMNShape id="start_di" bpmnElement="start"><dc:Bounds x="120" y="120" width="36" height="36" /></bpmndi:BPMNShape>
                  <bpmndi:BPMNShape id="approval_di" bpmnElement="approval"><dc:Bounds x="230" y="98" width="100" height="80" /></bpmndi:BPMNShape>
                  <bpmndi:BPMNShape id="end_di" bpmnElement="end"><dc:Bounds x="410" y="120" width="36" height="36" /></bpmndi:BPMNShape>
                  <bpmndi:BPMNEdge id="toApproval_di" bpmnElement="toApproval"><di:waypoint x="156" y="138" /><di:waypoint x="230" y="138" /></bpmndi:BPMNEdge>
                  <bpmndi:BPMNEdge id="toEnd_di" bpmnElement="toEnd"><di:waypoint x="330" y="138" /><di:waypoint x="410" y="138" /></bpmndi:BPMNEdge>
                </bpmndi:BPMNPlane>
              </bpmndi:BPMNDiagram>
            </bpmn:definitions>
            """;
        var engine = new HttpBpmnEngineService(
            new FixedHttpClientFactory(_client),
            NullLogger<HttpBpmnEngineService>.Instance);
        var repository = new RepositoryService(engine);
        var workflow = new WorkflowService(engine);

        var definition = await repository.DeployXmlAsync(bpmn, "studio-full-support.bpmn");
        var started = await workflow.StartProcessAsync(
            definition.Definition.Key,
            new Dictionary<string, object?> { ["source"] = "studio" },
            $"studio-{Guid.NewGuid():N}");

        Assert.Equal(ProcessInstanceStatus.Running, started.Status);
        Assert.NotEmpty(started.ActiveTasks);
        var task = Assert.Single((await engine.GetTasksAsync())
, candidate => candidate.ProcessInstanceId == started.Id);

        await engine.CompleteTaskAsync(task.Id, new Dictionary<string, object?> { ["approved"] = true });

        var completed = Assert.Single((await workflow.GetProcessInstancesAsync())
, instance => instance.Id == started.Id);
        Assert.Equal(ProcessInstanceStatus.Completed, completed.Status);
        Assert.Empty(completed.ActiveTasks);
    }

    private sealed class FixedHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
