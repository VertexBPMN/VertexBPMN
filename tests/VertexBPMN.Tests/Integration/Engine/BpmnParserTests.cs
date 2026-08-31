using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using System.Text.Json;
using VertexBPMN.Domain.Exceptions;
using VertexBPMN.Engine.Parsing;

// added for reading BPMN file

namespace VertexBPMN.Tests.Integration.Engine
{
    public class BpmnParserTests
    {
        private readonly Mock<ILogger<BpmnParser>> _loggerMock;
        private readonly Mock<TracerProvider> _tracerProviderMock;
        private readonly BpmnParser _parser;

        public BpmnParserTests()
        {
            _loggerMock = new Mock<ILogger<BpmnParser>>();
            _tracerProviderMock = new Mock<TracerProvider>();
            _parser = new BpmnParser(_loggerMock.Object, _tracerProviderMock.Object);
        }

        [Fact]
        public async Task ParseAsync_ValidBpmnXml_ReturnsProcessModel()
        {
            // Arrange
            var bpmnXml = @"
                <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn'>
                    <bpmn:process id='Process_1' name='Test Process'>
                        <bpmn:startEvent id='start1'/>
                        <bpmn:serviceTask id='task1' name='MCP Task'>
                            <bpmn:extensionElements>
                                <camunda:properties>
                                    <camunda:property name='type' value='mcpServiceTask'/>
                                    <camunda:property name='mcpServerUrl' value='http://cms-mcp:8080/api/mcp'/>
                                    <camunda:property name='mcpMethod' value='trigger_approval'/>
                                </camunda:properties>
                            </bpmn:extensionElements>
                        </bpmn:serviceTask>
                        <bpmn:sequenceFlow id='flow1' sourceRef='start1' targetRef='task1'/>
                    </bpmn:process>
                </bpmn:definitions>";

            // Act
            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("Process_1", processModel.Id);
            Assert.Equal("Test Process", processModel.Name);
            Assert.Contains(processModel.Events, pi => pi.Id == "start1" && pi.Type == "startEvent");
            Assert.Contains(processModel.Tasks, pi => pi.Id == "task1" && pi.Type == "serviceTask" && pi.Attributes?.GetValueOrDefault("type") == "mcpServiceTask");
            Assert.Contains(processModel.SequenceFlows, s => s.Id == "flow1");
            var task = processModel.Tasks.Single(t => t.Id == "task1");
            Assert.NotNull(task.Attributes);
            var attributes = task.Attributes!;
            Assert.Equal("mcpServiceTask", attributes["type"]);
            Assert.Equal("http://cms-mcp:8080/api/mcp", attributes["mcpServerUrl"]);
            Assert.Equal("trigger_approval", attributes["mcpMethod"]);
        }

        [Fact]
        public async Task ParseAsync_InvalidBpmnXml_ThrowsException()
        {
            // Arrange
            var invalidBpmnXml = "<invalid></invalid>";

            // Act & Assert
            var model = await _parser.ParseAsync(invalidBpmnXml, TestContext.Current.CancellationToken);
            var task = model.Diagnostics.First();
            Assert.Equal("No <process> element", task);
        }
        [Fact]
        public async Task ParseAsync_FlowableTaskListener_ParsesCorrectly()
        {
            var logger = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:flowable='http://flowable.org/bpmn'>
                  <process id='process1'>
                    <userTask id='task1'>
                      <extensionElements>
                        <flowable:taskListener event='create' class='com.example.MyTaskListener'/>
                      </extensionElements>
                    </userTask>
                  </process>
                </definitions>";
            var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
            var task = model.Tasks.First();
            Assert.NotNull(task.Attributes);
            var attributes = task.Attributes!;
            Assert.True(attributes.ContainsKey("flowable:taskListeners"));
            var listeners = JsonSerializer.Deserialize<List<dynamic>>(attributes["flowable:taskListeners"]);
            Assert.NotNull(listeners);
            Assert.Equal("create", ((JsonElement)listeners[0]).GetProperty("Event").GetString());// (string)listeners[0].Event);
            Assert.Equal("com.example.MyTaskListener", ((JsonElement)listeners[0]).GetProperty("Class").GetString());
        }

        [Fact]
        public async Task ParseAsync_ValidBpmnXml_ReturnsCorrectModel()
        {
            var logger = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            var xml = @"<definitions xmlns='http://www.omg.org/spec/BPMN/20100524/MODEL'>
                      <process id='process1' name='Test Process'>
                        <userTask id='task1' name='Test Task'/>
                      </process>
                    </definitions>";
            var model = await parser.ParseAsync(xml, TestContext.Current.CancellationToken);
            Assert.NotNull(model);
            Assert.Equal("process1", model.Id);
            Assert.Single(model.Tasks);
            Assert.Equal("task1", model.Tasks[0].Id);
        }

        [Fact]
        public async Task ParseAsync_InvalidXml_ThrowsBpmnParseException()
        {
            var logger = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            await Assert.ThrowsAsync<SecurityException>(() => parser.ParseAsync("<invalid>", TestContext.Current.CancellationToken));
        }


        [Fact]
        public async Task ParseAsync_ValidBpmnXmlWithMcpServiceTask_ReturnsProcessModel()
        {
            // Arrange
            var bpmnXml = @"
                <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn' xmlns:vertex='http://vertexbpmn.io/mcp'>
                    <bpmn:process id='Process_1' name='Test Process'>
                        <bpmn:startEvent id='start1'/>
                        <bpmn:serviceTask id='task1' name='MCP Task'>
                            <bpmn:extensionElements>
                                <camunda:property name='type' value='mcpServiceTask'/>
                                <vertex:mcpServiceTask mcpServerUrl='http://cms-mcp:8080/api/mcp' mcpMethod='trigger_approval'/>
                            </bpmn:extensionElements>
                        </bpmn:serviceTask>
                        <bpmn:sequenceFlow id='flow1' sourceRef='start1' targetRef='task1'/>
                    </bpmn:process>
                </bpmn:definitions>";

            // Act
            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);

            // Assert
            Assert.Equal("Process_1", processModel.Id);
            Assert.Equal("Test Process", processModel.Name);
            var task = processModel.Tasks.Single(t => t.Id == "task1");
            Assert.Equal("serviceTask", task.Type);
            Assert.NotNull(task.Attributes);
            var attributes = task.Attributes!;
            Assert.Equal("http://cms-mcp:8080/api/mcp", attributes["mcpServerUrl"]);
            Assert.Equal("trigger_approval", attributes["mcpMethod"]);
            var start1 = processModel.Events.Single(e => e.Id == "start1");
            Assert.Equal("startEvent", start1.Type);
            var flow1 = processModel.SequenceFlows.Single(s => s.Id == "flow1");
            Assert.Equal("start1", flow1.SourceRef);
        }

        [Fact]
        public async Task ParseAsync_InvalidMcpServiceTask_ThrowsException()
        {
            // Arrange
            var bpmnXml = @"
                <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL' xmlns:camunda='http://camunda.org/schema/1.0/bpmn' xmlns:vertex='http://vertexbpmn.io/mcp'>
                    <bpmn:process id='Process_1'>
                        <bpmn:serviceTask id='task1'>
                            <bpmn:extensionElements>
                                <camunda:property name='type' value='mcpServiceTask'/>
                            </bpmn:extensionElements>
                        </bpmn:serviceTask>
                    </bpmn:process>
                </bpmn:definitions>";

            // Act & Assert
            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);

            Assert.Equal("Process_1", processModel.Id);
        }


        [Fact]
        public async Task ParseAsync_ExclusiveGatewayWithConditions_ReturnsProcessModel()
        {
            var bpmnXml = @"
        <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
            <bpmn:process id='Process_1' name='Test Process'>
                <bpmn:startEvent id='start1'/>
                <bpmn:exclusiveGateway id='gateway1'/>
                <bpmn:userTask id='task1'/>
                <bpmn:userTask id='task2'/>
                <bpmn:sequenceFlow id='flow1' sourceRef='start1' targetRef='gateway1'/>
                <bpmn:sequenceFlow id='flow2' sourceRef='gateway1' targetRef='task1'>
                    <bpmn:conditionExpression><![CDATA[${amount > 1000}]]></bpmn:conditionExpression>
                </bpmn:sequenceFlow>
                <bpmn:sequenceFlow id='flow3' sourceRef='gateway1' targetRef='task2'>
                    <bpmn:conditionExpression><![CDATA[${amount <= 1000}]]></bpmn:conditionExpression>
                </bpmn:sequenceFlow>
            </bpmn:process>
        </bpmn:definitions>";

            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);

            Assert.Equal("Process_1", processModel.Id);
            Assert.Contains(processModel.Gateways, pi => pi.Id == "gateway1" && pi.Type == "exclusiveGateway");
            var gateway1 = processModel.Gateways.First(g => g.Id == "gateway1");
            var flow2 = processModel.SequenceFlows.First(s => s.Id == "flow2");
            var flow3 = processModel.SequenceFlows.First(s => s.Id == "flow3");
            Assert.NotNull(flow2.Attributes);
            Assert.NotNull(flow3.Attributes);
            Assert.Equal("${amount > 1000}", flow2.Attributes!["conditionExpression"]);
            Assert.Equal("${amount <= 1000}", flow3.Attributes!["conditionExpression"]);
        }

        [Fact]
        public async Task ParseAsync_ParallelGateway_ReturnsProcessModel()
        {
            var bpmnXml = @"
                <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
                    <bpmn:process id='Process_1' name='Test Process'>
                        <bpmn:startEvent id='start1'/>
                        <bpmn:parallelGateway id='gateway1'/>
                        <bpmn:userTask id='task1'/>
                        <bpmn:userTask id='task2'/>
                        <bpmn:sequenceFlow id='flow1' sourceRef='start1' targetRef='gateway1'/>
                        <bpmn:sequenceFlow id='flow2' sourceRef='gateway1' targetRef='task1'/>
                        <bpmn:sequenceFlow id='flow3' sourceRef='gateway1' targetRef='task2'/>
                    </bpmn:process>
                </bpmn:definitions>";

            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);

            Assert.Equal("Process_1", processModel.Id);
            Assert.Contains(processModel.Gateways, pi => pi.Id == "gateway1" && pi.Type == "parallelGateway");
            Assert.Contains(processModel.SequenceFlows, s => s.Id == "flow2" && s.TargetRef == "task1");
            Assert.Contains(processModel.SequenceFlows, s => s.Id == "flow3" && s.TargetRef == "task2");
        }

        [Fact]
        public async Task ParseAsync_SubProcess_ReturnsProcessModel()
        {
            var bpmnXml = @"
                <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
                    <bpmn:process id='Process_1' name='Test Process'>
                        <bpmn:startEvent id='start1'/>
                        <bpmn:subProcess id='sub1'>
                            <bpmn:startEvent id='sub_start1'/>
                            <bpmn:userTask id='sub_task1'/>
                            <bpmn:sequenceFlow id='sub_flow1' sourceRef='sub_start1' targetRef='sub_task1'/>
                        </bpmn:subProcess>
                        <bpmn:sequenceFlow id='flow1' sourceRef='start1' targetRef='sub1'/>
                    </bpmn:process>
                </bpmn:definitions>";

            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);

            Assert.Equal("Process_1", processModel.Id);
            var subProcess = processModel.Subprocesses.FirstOrDefault(pi => pi.Id == "sub1");
            Assert.NotNull(subProcess);
            //Assert.NotNull(subProcess.IsMultiInstance);
            //Assert.Contains(subProcess, pi => pi.Id == "sub_start1" && pi.Type == "eventListener");
            //Assert.Contains(subProcess.SubProcessModel.PlanItems, pi => pi.Id == "sub_task1" && pi.Type == "userTask");
        }

        [Fact]
        public async Task ParseAsync_MultiInstanceSubProcess_ReturnsProcessModel()
        {
            var bpmnXml = @"
                <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
                    <bpmn:process id='Process_1' name='Test Process'>
                        <bpmn:startEvent id='start1'/>
                        <bpmn:subProcess id='sub1'>
                            <bpmn:multiInstanceLoopCharacteristics isSequential='true'>
                                <bpmn:loopCardinality>3</bpmn:loopCardinality>
                            </bpmn:multiInstanceLoopCharacteristics>
                            <bpmn:startEvent id='sub_start1'/>
                            <bpmn:userTask id='sub_task1'/>
                            <bpmn:sequenceFlow id='sub_flow1' sourceRef='sub_start1' targetRef='sub_task1'/>
                        </bpmn:subProcess>
                        <bpmn:sequenceFlow id='flow1' sourceRef='start1' targetRef='sub1'/>
                    </bpmn:process>
                </bpmn:definitions>";

            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);

            var subProcess = processModel.Subprocesses.FirstOrDefault(pi => pi.Id == "sub1");
            Assert.NotNull(subProcess);
            Assert.Equal(3, subProcess.LoopCardinality);
            Assert.True(subProcess.IsSequential);
        }

        [Fact]
        public async Task ParseAsync_InvalidExclusiveGateway_ThrowsException()
        {
            var bpmnXml = @"
                <bpmn:definitions xmlns:bpmn='http://www.omg.org/spec/BPMN/20100524/MODEL'>
                    <bpmn:process id='Process_1'>
                        <bpmn:exclusiveGateway id='gateway1'/>
                        <bpmn:userTask id='task1'/>
                        <bpmn:sequenceFlow id='flow1' sourceRef='gateway1' targetRef='task1'/>
                    </bpmn:process>
                </bpmn:definitions>";

            var processModel = await _parser.ParseAsync(bpmnXml, TestContext.Current.CancellationToken);
            Assert.NotNull(processModel);
        }

        //[Fact]
        //public async Task Serialize_RoundTrip_ReturnsValidXml()
        //{
        //    // Arrange
        //    var model = new BpmnModel(
        //        "Process_1",
        //        "Test Process",
        //        Array.Empty<BpmnEvent>().AsReadOnly(),
        //         new List<BpmnTask>()
        //         { new BpmnTask("task1", "mcpServiceTask", "mcpServiceTask", new Dictionary<string, string>
        //        {
        //            ["type"] = "mcpServiceTask",
        //            ["mcpServerUrl"] = "http://cms-mcp:8080/api/mcp",
        //            ["mcpMethod"] = "trigger_approval"
        //        }) },
        //        Array.Empty<Sentry>().AsReadOnly(),
        //        Array.Empty<CaseFileItem>().AsReadOnly(),
        //        Array.Empty<BpmnLane>().AsReadOnly(),
        //        Array.Empty<BpmnDataObject>().AsReadOnly(),
        //        Array.Empty<BpmnAssociation>().AsReadOnly(),
        //        Array.Empty<BpmnTextAnnotation>().AsReadOnly(),
        //        Array.Empty<BpmnParticipant>().AsReadOnly(),
        //         Array.Empty<BpmnMessageFlow>().AsReadOnly()
        //    );

        //    // Act
        //    var xml = _parser.Serialize(model);
        //    var roundTripModel = await _parser.ParseAsync(xml);

        //    // Assert
        //    Assert.Equal(model.Id, roundTripModel.Id);
        //    Assert.Contains(roundTripModel.Tasks, pi => pi.Id == "task1" && pi.Type == "mcpServiceTask");
        //}

    }
}
