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
            bpmnDb.ProcessDefinitions.Add(new ProcessDefinition
            {
                Id = Guid.NewGuid(),
                Key = "simpleProcess",
                Name = "Simple Test Process",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
                DeploymentId = Guid.NewGuid(),
            });

            bpmnDb.ProcessDefinitions.Add(new ProcessDefinition
            {
                Id = Guid.NewGuid(),
                Key = "advancedProcess",
                Name = "Advanced Test Process",
                Version = 1,
                CreatedAt = DateTime.UtcNow,
            });

            await bpmnDb.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded process definitions");
        }
    }
}