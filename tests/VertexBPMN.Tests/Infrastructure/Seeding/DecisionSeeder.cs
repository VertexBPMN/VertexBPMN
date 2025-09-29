using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VertexBPMN.Domain.Model.Dmn;
using VertexBPMN.Infrastructure.Persistence;
using DecisionDefinition = VertexBPMN.Domain.Entities.DecisionDefinition;

namespace VertexBPMN.Tests.Infrastructure.Seeding;

public sealed class DecisionSeeder : TestDataSeederBase
{
    public override int Order => 30;

    private const string SampleDmn = """
        <definitions xmlns="http://www.omg.org/spec/DMN/20191111/MODEL/">
          <decision id="d1" name="TestDecision">
            <decisionTable hitPolicy="UNIQUE">
              <input id="i1"><inputExpression>val</inputExpression></input>
              <output id="o1" name="result"/>
              <rule><inputEntry>42</inputEntry><outputEntry>"ok"</outputEntry></rule>
            </decisionTable>
          </decision>
        </definitions>
        """;

    public override async Task SeedAsync(IServiceScope scope, CancellationToken cancellationToken = default)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<DecisionSeeder>>();
        var decisionDb = scope.ServiceProvider.GetRequiredService<DecisionDbContext>();

        var set = decisionDb.DecisionDefinitions;
        var model = DmnDecisionTable.Parse(SampleDmn);
        // Idempotent check via reflection (fallback)
        var any = await set
            .AsQueryable()
            .Cast<object>()
            .AnyAsync(cancellationToken);

        if (!any)
        {
            var decision = new DecisionDefinition
            {
                Id = Guid.NewGuid().ToString(),
                Key = "d1",
                Name = "TestDecision",
                DmnXml = SampleDmn,
                DecisionTable = model,
                TenantId = null
            };
            set.Add(decision);
            await decisionDb.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Seeded DMN decision");
        }
    }

}