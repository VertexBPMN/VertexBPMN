using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces.Repositories;
using VertexBPMN.Domain.Model.Bpmn;
using VertexBPMN.Engine.Parsing;
using VertexBPMN.Infrastructure.Persistence;
using VertexBPMN.Tests.Unit.Infrastructure;

namespace VertexBPMN.Tests.Acceptance;

public sealed class ExternalTaskDeploymentTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("disabled", false)]
    [InlineData("tenant", false)]
    [InlineData("topic", false)]
    [InlineData("mapping", false)]
    [InlineData("invalid-limit", false)]
    [InlineData("duplicate-input", false)]
    public async Task RejectsInvalidContractBeforeRepositoryWrites(string scenario, bool accepted)
    {
        var ct = TestContext.Current.CancellationToken;
        var config = ExternalTaskContractResolverTests.Configuration();
        if (scenario == "disabled") config["ExternalTasks:EnableSchedulingPreview"] = "false";
        var topic = scenario == "topic" ? "unknown" : "test.work";
        var inputName = scenario == "mapping" ? "other" : "text";
        var xml = $$"""
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0" targetNamespace="urn:test">
          <process id="p" isExecutable="true"><startEvent id="s"/><serviceTask id="work"><extensionElements>
            <vertex:externalTask topic="{{topic}}" maxRetries="0" deadlineSeconds="300"/>
            <vertex:ioMapping><vertex:input name="{{inputName}}" expression="document"/><vertex:output name="result" target="contractReview"/></vertex:ioMapping>
          </extensionElements></serviceTask><endEvent id="e"/>
          <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="e"/>
          </process></definitions>
        """;
        if (scenario == "invalid-limit") xml = xml.Replace("maxRetries=\"0\"", "maxRetries=\"-1\"", StringComparison.Ordinal);
        if (scenario == "duplicate-input") xml = xml.Replace("</vertex:ioMapping>", "<vertex:input name=\"text\" expression=\"other\"/></vertex:ioMapping>", StringComparison.Ordinal);
        var repository = new Mock<IProcessDefinitionRepository>();
        var service = new VertexBPMN.Application.RepositoryService(repository.Object, new BpmnParser(), config,
            new ConfiguredExternalTaskContractResolver(config));
        if (accepted)
        {
            var result = await service.DeployAsync(xml, "test", "a", ct);
            Assert.Equal("a", result.TenantId);
            repository.Verify(repo => repo.AddAsync(It.IsAny<ProcessDefinition>(), ct), Times.Once);
        }
        else
        {
            var error = await Assert.ThrowsAsync<BpmnDeploymentValidationException>(async () =>
                await service.DeployAsync(xml, "test", scenario == "tenant" ? "b" : "a", ct));
            Assert.Contains(error.Diagnostics, diagnostic => diagnostic.Code == "VEN-EXTERNAL-TASK-CONTRACT");
            repository.VerifyNoOtherCalls();
        }
    }

    [Fact]
    public async Task ProductionFeatureDeploysExternalTaskWithoutPreviewFlag()
    {
        var ct = TestContext.Current.CancellationToken;
        var config = ExternalTaskContractResolverTests.Configuration();
        config["ExternalTasks:Enabled"] = "true";
        config["ExternalTasks:EnableSchedulingPreview"] = "false";
        var xml = """
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL" xmlns:vertex="https://vertexbpmn.io/schema/bpmn/1.0" targetNamespace="urn:test">
          <process id="production-external" isExecutable="true"><startEvent id="s"/><serviceTask id="work"><extensionElements>
            <vertex:externalTask topic="test.work" maxRetries="0" deadlineSeconds="300"/>
            <vertex:ioMapping><vertex:input name="text" expression="document"/><vertex:output name="result" target="contractReview"/></vertex:ioMapping>
          </extensionElements></serviceTask><endEvent id="e"/>
          <sequenceFlow id="f1" sourceRef="s" targetRef="work"/><sequenceFlow id="f2" sourceRef="work" targetRef="e"/>
          </process></definitions>
        """;
        var repository = new Mock<IProcessDefinitionRepository>();
        var service = new VertexBPMN.Application.RepositoryService(repository.Object, new BpmnParser(), config,
            new ConfiguredExternalTaskContractResolver(config));

        await service.DeployAsync(xml, "production", "a", ct);

        repository.Verify(repo => repo.AddAsync(It.IsAny<ProcessDefinition>(), ct), Times.Once);
    }
}
