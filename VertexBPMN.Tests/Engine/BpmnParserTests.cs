using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using VertexBPMN.Core.Bpmn;
using VertexBPMN.Core.Cmmn;
using VertexBPMN.Core.Dmn;
using VertexBPMN.Core.Domain;
using VertexBPMN.Core.Engine;
using VertexBPMN.Core.Exceptions;
using VertexBPMN.Core.Messaging;
using VertexBPMN.Core.Services;

// added for reading BPMN file

namespace VertexBPMN.Tests.Engine
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
            var processModel = await _parser.ParseAsync(bpmnXml);

            // Assert
            Assert.Equal("Process_1", processModel.Id);
            Assert.Equal("Test Process", processModel.Name);
            Assert.Contains(processModel.Events, pi => pi.Id == "start1" && pi.Type == "eventListener");
            Assert.Contains(processModel.Tasks, pi => pi.Id == "task1" && pi.Type == "serviceTask" && pi.Attributes["type"] == "mcpServiceTask");
            Assert.Contains(processModel.SequenceFlows, s => s.Id == "flow1");
        }

        [Fact]
        public async Task ParseAsync_InvalidBpmnXml_ThrowsException()
        {
            // Arrange
            var invalidBpmnXml = "<invalid></invalid>";

            // Act & Assert
            await Assert.ThrowsAsync<DistributedTokenException>(() => _parser.ParseAsync(invalidBpmnXml));
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
            var model = await parser.ParseAsync(xml);
            var task = model.Tasks.First();
            Assert.True(task.Attributes.ContainsKey("flowable:taskListeners"));
            var listeners = JsonSerializer.Deserialize<List<dynamic>>(task.Attributes["flowable:taskListeners"]);
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
            var model = await parser.ParseAsync(xml);
            Assert.NotNull(model);
            Assert.Equal("process1", model.Id);
            Assert.Single(model.Tasks);
            Assert.Equal("task1", model.Tasks[0].Id);
        }

        [Fact]
        public async Task ParseAsync_InvalidXml_ThrowsBpmnParseException()
        {
            var logger = new Mock<ILogger<BpmnParser>>(); var parser = new BpmnParser(logger.Object, TracerProvider.Default);
            await Assert.ThrowsAsync<BpmnParseException>(() => parser.ParseAsync("<invalid>"));
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
            var processModel = await _parser.ParseAsync(bpmnXml);

            // Assert
            Assert.Equal("Process_1", processModel.Id);
            Assert.Equal("Test Process", processModel.Name);
            Assert.Contains(processModel.Events, pi => pi.Id == "start1" && pi.Type == "eventListener" && pi.AttachedToRef == "startEvent");
            Assert.Contains(processModel.Tasks, pi => pi.Id == "task1" && pi.Type == "mcpServiceTask" && pi.Attributes["mcpServerUrl"] == "http://cms-mcp:8080/api/mcp");
            Assert.Contains(processModel.SequenceFlows, s => s.Id == "flow1");
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
            await Assert.ThrowsAsync<BpmnParseException>(() => _parser.ParseAsync(bpmnXml));
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