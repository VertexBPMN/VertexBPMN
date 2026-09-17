using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Domain.Interfaces;
using VertexBPMN.Engine.Execution;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Acceptance;

public sealed class PersistentVertexConnectorExecutionTests
{
    [Theory]
    [InlineData("https://vertexbpmn.io/schema/bpmn/1.0")]
    [InlineData("http://vertexbpmn.io/schema/1.0")]
    public async Task Runtime_ResolvesVertexConnectorServiceTask(string vertexNamespace)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using var db = new BpmnDbContext(new DbContextOptionsBuilder<BpmnDbContext>()
            .UseSqlite(connection).Options);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        var deployment = new EngineDeployment { Id = Guid.NewGuid(), Name = "connector-runtime" };
        var definition = new ProcessDefinition
        {
            Id = Guid.NewGuid(),
            Key = "connector-runtime",
            Name = "Connector runtime",
            Version = 1,
            Deployment = deployment,
            DeploymentId = deployment.Id,
            CreatedAt = DateTime.UtcNow,
            BpmnXml = $$"""
                <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                             xmlns:vertex="{{vertexNamespace}}">
                  <process id="connector-runtime" isExecutable="true">
                    <startEvent id="start" />
                    <sequenceFlow id="to-connector" sourceRef="start" targetRef="connector" />
                    <serviceTask id="connector">
                      <extensionElements>
                        <vertex:connector type="http" operationId="http.request" timeoutMs="5000" />
                      </extensionElements>
                    </serviceTask>
                    <sequenceFlow id="to-end" sourceRef="connector" targetRef="end" />
                    <endEvent id="end" />
                  </process>
                </definitions>
                """
        };
        db.ProcessDefinitions.Add(definition);
        await db.SaveChangesAsync(cancellationToken);

        IDictionary<string, string>? executedAttributes = null;
        var handler = new Mock<IServiceTaskHandler>(MockBehavior.Strict);
        handler.Setup(candidate => candidate.ExecuteAsync(
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .Callback((IDictionary<string, string> attributes, IDictionary<string, object> _, CancellationToken _) =>
                executedAttributes = new Dictionary<string, string>(attributes))
            .Returns(Task.CompletedTask);
        IServiceTaskHandler? resolvedHandler = handler.Object;
        var registry = new Mock<IServiceTaskRegistry>(MockBehavior.Strict);
        registry.Setup(candidate => candidate.TryResolve("vertex:connector", out resolvedHandler)).Returns(true);

        var runtime = new PersistentProcessExecutionRuntime(
            db,
            registry.Object,
            Mock.Of<IDecisionService>(),
            NullLogger<PersistentProcessExecutionRuntime>.Instance);

        var instance = await runtime.StartAsync(
            definition,
            variables: null,
            businessKey: null,
            tenantId: null,
            cancellationToken: cancellationToken);

        Assert.Equal(ProcessInstanceStatus.Completed, instance.Status);
        Assert.NotNull(executedAttributes);
        Assert.Equal("http", executedAttributes["vertex:connector.type"]);
        Assert.Equal("http.request", executedAttributes["vertex:connector.operationId"]);
        Assert.Equal("5000", executedAttributes["vertex:connector.timeoutMs"]);
        handler.VerifyAll();
        registry.VerifyAll();
    }
}
