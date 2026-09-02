using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Entities;
using VertexBPMN.Infrastructure.Persistence;

namespace VertexBPMN.Tests.Infrastructure.Seeding;

public sealed class ProcessDefinitionSeeder : TestDataSeederBase
{
    public override int Order => 20;

    public override async Task SeedAsync(IServiceScope scope, CancellationToken cancellationToken = default)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<ProcessDefinitionSeeder>>();
        var bpmnDb = scope.ServiceProvider.GetRequiredService<BpmnDbContext>();

        if (bpmnDb.Model.FindEntityType(typeof(ProcessDefinition)) is null)
            return;

        if (!await bpmnDb.ProcessDefinitions.AnyAsync(cancellationToken))
        {
            var deployment = new EngineDeployment
            {
                Id = Guid.NewGuid(),
                Name = "Integration test definitions",
                CreatedAt = DateTime.UtcNow
            };
            bpmnDb.EngineDeployments.Add(deployment);

            bpmnDb.ProcessDefinitions.Add(new ProcessDefinition
            {
                Id = Guid.NewGuid(),
                Key = "simpleProcess",
                Name = "Simple Test Process",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                DeploymentId = deployment.Id,
                BpmnXml = MinimalProcess("simpleProcess")
            });

            bpmnDb.ProcessDefinitions.Add(new ProcessDefinition
            {
                Id = Guid.NewGuid(),
                Key = "advancedProcess",
                Name = "Advanced Test Process",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                DeploymentId = deployment.Id,
                BpmnXml = MinimalProcess("advancedProcess")
            });

            await bpmnDb.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded process definitions");
        }
    }

    private static string MinimalProcess(string processId) => $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <definitions xmlns="http://www.omg.org/spec/BPMN/20100524/MODEL"
                     targetNamespace="https://vertexbpmn.dev/tests">
          <process id="{processId}" isExecutable="true">
            <startEvent id="start" />
            <endEvent id="end" />
            <sequenceFlow id="start-to-end" sourceRef="start" targetRef="end" />
          </process>
        </definitions>
        """;
}
